using System.Text.Json;

namespace MacDesktop;

/// <summary>
/// 应用设置，持久化到 %APPDATA%\MacDesktop\settings.json。
/// 字段与 <see cref="MaximizeWatcher"/> 的行为开关一一对应。
/// </summary>
internal sealed class AppSettings
{
    /// <summary>是否启用「最大化即新建虚拟桌面」的监听。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>是否处理无边框全屏（覆盖整个显示器），而不仅仅是标准最大化。</summary>
    public bool HandleFullscreen { get; set; } = true;

    /// <summary>是否仅处理拥有标题栏的窗口，可过滤掉多数系统/工具窗口。</summary>
    public bool RequireCaption { get; set; } = true;

    /// <summary>恢复/最小化/关闭窗口后是否删除临时创建的虚拟桌面。</summary>
    public bool RemoveDesktopOnRestore { get; set; } = true;

    // ---------- 持久化 ----------

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacDesktop",
        "settings.json");

    /// <summary>从磁盘读取设置；文件不存在或损坏时返回默认设置。</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Log.Write($"设置已加载: {FilePath}");
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("加载设置失败，使用默认值", ex);
        }

        return new AppSettings();
    }

    /// <summary>把当前设置写入磁盘。</summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            Log.Write($"设置已保存: {FilePath}");
        }
        catch (Exception ex)
        {
            Log.Error("保存设置失败", ex);
        }
    }
}
