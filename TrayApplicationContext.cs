using System.Drawing;
using System.Windows.Forms;

namespace MacDesktop;

/// <summary>
/// 托盘应用上下文：提供托盘图标、设置入口与退出菜单，
/// 并托管 <see cref="MaximizeWatcher"/> 的生命周期。设置持久化到 <see cref="AppSettings"/>。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly MaximizeWatcher _watcher = new();
    private readonly AppSettings _settings;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly Icon _appIcon = AppIcon.Create(32);

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        ApplyToWatcher();

        _enabledItem = new ToolStripMenuItem("已启用", null, OnToggleEnabled)
        {
            Checked = _settings.Enabled,
            CheckOnClick = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("设置…", null, OnOpenSettings));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, OnExit));

        _tray = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "Mac Desktop - 最大化即新建虚拟桌面",
            Visible = true,
            ContextMenuStrip = menu
        };
        // 双击托盘图标打开设置
        _tray.DoubleClick += OnOpenSettings;

        if (_settings.Enabled)
            TryStart();
    }

    /// <summary>把设置里的行为开关同步到 watcher（不含启用/停止）。</summary>
    private void ApplyToWatcher()
    {
        _watcher.HandleFullscreen = _settings.HandleFullscreen;
        _watcher.RequireCaption = _settings.RequireCaption;
        _watcher.RemoveDesktopOnRestore = _settings.RemoveDesktopOnRestore;
    }

    private void TryStart()
    {
        try
        {
            _watcher.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Mac Desktop 启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _settings.Enabled = false;
            _enabledItem.Checked = false;
        }
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _settings.Enabled = _enabledItem.Checked;
        _settings.Save();

        if (_settings.Enabled)
            TryStart();
        else
            _watcher.Stop();
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_settings, _appIcon);
        if (form.ShowDialog() != DialogResult.OK)
            return;

        // 应用运行时开关
        ApplyToWatcher();

        // 同步「启用」状态（菜单勾选 + 启停 watcher）
        _enabledItem.Checked = _settings.Enabled;
        if (_settings.Enabled && !_watcher.IsRunning)
            TryStart();
        else if (!_settings.Enabled && _watcher.IsRunning)
            _watcher.Stop();
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _watcher.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _appIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcher.Dispose();
            _tray.Dispose();
            _appIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
