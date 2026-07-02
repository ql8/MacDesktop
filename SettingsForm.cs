using System.Drawing;
using System.Windows.Forms;

namespace MacDesktop;

/// <summary>
/// 基本设置窗口：以复选框呈现各行为开关，点击「保存」后回写 <see cref="AppSettings"/>。
/// 全部用代码构建，无需 designer 文件。
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    private readonly CheckBox _enabled;
    private readonly CheckBox _fullscreen;
    private readonly CheckBox _requireCaption;
    private readonly CheckBox _removeDesktop;

    public SettingsForm(AppSettings settings, Icon? icon = null)
    {
        _settings = settings;

        Text = "Mac Desktop - 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(360, 220);
        Icon = icon ?? SystemIcons.Application;

        _enabled = MakeCheckBox("启用（最大化即新建虚拟桌面）", 20, _settings.Enabled);
        _fullscreen = MakeCheckBox("处理无边框全屏（不仅标准最大化）", 52, _settings.HandleFullscreen);
        _requireCaption = MakeCheckBox("仅处理有标题栏的窗口", 84, _settings.RequireCaption);
        _removeDesktop = MakeCheckBox("窗口返回后删除临时虚拟桌面", 116, _settings.RemoveDesktopOnRestore);

        var ok = new Button
        {
            Text = "保存",
            DialogResult = DialogResult.OK,
            Size = new Size(80, 30),
            Location = new Point(ClientSize.Width - 180, 170)
        };
        ok.Click += OnSave;

        var cancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Size = new Size(80, 30),
            Location = new Point(ClientSize.Width - 90, 170)
        };

        Controls.Add(_enabled);
        Controls.Add(_fullscreen);
        Controls.Add(_requireCaption);
        Controls.Add(_removeDesktop);
        Controls.Add(ok);
        Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static CheckBox MakeCheckBox(string text, int top, bool @checked) => new()
    {
        Text = text,
        Checked = @checked,
        AutoSize = true,
        Location = new Point(20, top)
    };

    private void OnSave(object? sender, EventArgs e)
    {
        _settings.Enabled = _enabled.Checked;
        _settings.HandleFullscreen = _fullscreen.Checked;
        _settings.RequireCaption = _requireCaption.Checked;
        _settings.RemoveDesktopOnRestore = _removeDesktop.Checked;
        _settings.Save();
    }
}
