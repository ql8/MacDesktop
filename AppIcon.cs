using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MacDesktop;

/// <summary>
/// 运行时绘制的 MacDesktop 图标：macOS 风格圆角蓝色背景 + 白色 “MD” 字样。
/// 用于托盘图标与窗口图标，无需外部资源文件。
/// </summary>
internal static class AppIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>创建指定边长（像素）的图标；返回托管副本，调用者负责 Dispose。</summary>
    public static Icon Create(int size = 32)
    {
        using var bmp = RenderBitmap(size);
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone(); // 转成托管副本，随后可安全销毁原生句柄
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>把图标绘制成 PNG 编码的 .ico 文件（支持透明、任意尺寸），用于 exe 应用图标。</summary>
    public static void SaveIcoFile(string path, int size = 256)
    {
        using var bmp = RenderBitmap(size);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        byte[] png = ms.ToArray();

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);
        // ICONDIR
        w.Write((short)0);            // reserved
        w.Write((short)1);            // type: icon
        w.Write((short)1);            // image count
        // ICONDIRENTRY
        w.Write((byte)(size >= 256 ? 0 : size)); // width（256 记为 0）
        w.Write((byte)(size >= 256 ? 0 : size)); // height
        w.Write((byte)0);             // color count
        w.Write((byte)0);             // reserved
        w.Write((short)1);            // color planes
        w.Write((short)32);           // bits per pixel
        w.Write(png.Length);          // 图像数据字节数
        w.Write(6 + 16);              // 图像数据偏移
        w.Write(png);
    }

    /// <summary>把图标绘制成 PNG 文件，便于预览设计效果。</summary>
    public static void SavePngFile(string path, int size = 256)
    {
        using var bmp = RenderBitmap(size);
        bmp.Save(path, ImageFormat.Png);
    }

    private static Bitmap RenderBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        float pad = size * 0.06f;
        var rect = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
        float radius = size * 0.22f;

        using (var path = RoundedRect(rect, radius))
        using (var bg = new LinearGradientBrush(rect,
                   Color.FromArgb(64, 142, 255),   // macOS 亮蓝
                   Color.FromArgb(20, 84, 214),    // 深蓝
                   LinearGradientMode.Vertical))
        {
            g.FillPath(bg, path);
        }

        using var font = FindFont(size);
        using var fg = new SolidBrush(Color.White);
        using var fmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("MD", font, fg, rect, fmt);

        return bmp;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Font FindFont(int size)
    {
        float em = size * 0.38f;
        foreach (var name in new[] { "Segoe UI Semibold", "Segoe UI", "Arial" })
        {
            try
            {
                var f = new Font(name, em, FontStyle.Bold, GraphicsUnit.Pixel);
                if (name == "Arial" || string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return f;
                f.Dispose();
            }
            catch { /* 换下一个字体 */ }
        }
        return new Font(FontFamily.GenericSansSerif, em, FontStyle.Bold, GraphicsUnit.Pixel);
    }
}
