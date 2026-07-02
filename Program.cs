using System.Windows.Forms;

namespace MacDesktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // 一次性维护入口：--cleanup [保留桌面数]，删除多余（如异常残留的空）虚拟桌面后退出。
        if (args.Length > 0 && args[0] == "--cleanup")
        {
            RunCleanup(args);
            return;
        }

        // 一次性工具：--genicon [输出路径]，把内置 MD 图标导出为 .ico（构建 exe 图标用）或 .png（预览用）。
        if (args.Length > 0 && args[0] == "--genicon")
        {
            var path = args.Length > 1 ? args[1] : "icon.ico";
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                AppIcon.SavePngFile(path, 256);
            else
                AppIcon.SaveIcoFile(path, 256);
            return;
        }

        // 单实例保护，避免多个进程同时抢占钩子
        using var mutex = new System.Threading.Mutex(true, "MacDesktop.SingleInstance", out bool isNew);
        if (!isNew)
            return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Log.Write("=== MacDesktop 启动 ===");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Write($"未捕获异常: {e.ExceptionObject}");
        Application.ThreadException += (_, e) =>
            Log.Error("UI 线程异常", e.Exception);

        Application.Run(new TrayApplicationContext());
    }

    /// <summary>删除多余虚拟桌面，仅保留指定数量（默认 2）。桌面上的窗口会被移到第 0 号桌面，不会丢失。</summary>
    private static void RunCleanup(string[] args)
    {
        int keep = 2;
        if (args.Length > 1 && int.TryParse(args[1], out int k) && k >= 1)
            keep = k;

        try
        {
            var fallback = VirtualDesktop.Desktop.FromIndex(0);
            int removed = 0;
            while (VirtualDesktop.Desktop.Count > keep)
            {
                int idx = VirtualDesktop.Desktop.Count - 1;
                VirtualDesktop.Desktop.FromIndex(idx).Remove(fallback);
                removed++;
            }
            Log.Write($"cleanup 完成: 删除 {removed} 个桌面, 现有桌面数={VirtualDesktop.Desktop.Count}");
        }
        catch (Exception ex)
        {
            Log.Error("cleanup 失败", ex);
        }
    }
}
