using System.Text;
using VirtualDesktop;
using static MacDesktop.NativeMethods;

namespace MacDesktop;

/// <summary>
/// 监听窗口的最大化/全屏状态变化，实现类 macOS Spaces 的体验：
/// 窗口进入最大化/全屏时，新建一个虚拟桌面并把窗口移过去再切换；
/// 退出最大化时，把窗口移回原桌面、切回并删除临时桌面。
/// </summary>
internal sealed class MaximizeWatcher : IDisposable
{
    // ---------- 可调整的行为开关 ----------

    /// <summary>是否处理“无边框全屏”（覆盖整个显示器）而不仅仅是标准最大化。</summary>
    public bool HandleFullscreen { get; set; } = true;

    /// <summary>仅处理拥有标题栏(WS_CAPTION)的窗口，可过滤掉多数系统/工具窗口。</summary>
    public bool RequireCaption { get; set; } = true;

    /// <summary>恢复窗口后删除临时创建的虚拟桌面。</summary>
    public bool RemoveDesktopOnRestore { get; set; } = true;

    // ---------- 内部状态 ----------

    private sealed record ManagedWindow(Desktop Origin, Desktop Created);

    private readonly Dictionary<IntPtr, ManagedWindow> _managed = new();
    private readonly Dictionary<IntPtr, bool> _lastMaximized = new();

    private WinEventDelegate? _procRef; // 保存委托引用，避免被 GC 回收
    private IntPtr _hook = IntPtr.Zero;
    private IntPtr _minHook = IntPtr.Zero; // 单独监听最小化事件（不在主区间内）

    /// <summary>是否正在由本程序触发桌面/窗口操作，用于忽略自身引发的事件。</summary>
    private bool _suppress;

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning) return;

        // 启动自检：直接调用一次虚拟桌面 API，验证 COM 接口在当前系统可用
        try
        {
            int count = Desktop.Count;
            int currentIndex = Desktop.FromDesktop(Desktop.Current);
            Log.Write($"self-check OK: 虚拟桌面数量={count}, 当前桌面索引={currentIndex}");
        }
        catch (Exception ex)
        {
            Log.Error("self-check 失败（VirtualDesktop 库与当前 Windows 版本不兼容？）", ex);
            throw new InvalidOperationException(
                "虚拟桌面接口初始化失败，可能是 VirtualDesktop 库与当前 Windows 版本不兼容。\n" +
                $"详情见日志: {Log.FilePath}\n\n{ex.Message}", ex);
        }

        _procRef = OnWinEvent;
        _hook = SetWinEventHook(
            EVENT_OBJECT_DESTROY,           // eventMin
            EVENT_OBJECT_LOCATIONCHANGE,    // eventMax（区间涵盖两个所需事件）
            IntPtr.Zero,
            _procRef,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("SetWinEventHook 失败，无法监听窗口事件。");

        // 单独安装最小化事件钩子（EVENT_SYSTEM_MINIMIZESTART 不在主区间 0x8001~0x800B 内，
        // 若并入主钩子会把中间上百种事件全部纳入，得不偿失）。
        _minHook = SetWinEventHook(
            EVENT_SYSTEM_MINIMIZESTART,
            EVENT_SYSTEM_MINIMIZESTART,
            IntPtr.Zero,
            _procRef,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        Log.Write("事件钩子已安装，开始监听窗口最大化/最小化。");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        // 退出前把仍在临时桌面上的窗口恢复回去
        foreach (var hwnd in _managed.Keys.ToArray())
            RestoreWindow(hwnd);

        UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;

        if (_minHook != IntPtr.Zero)
        {
            UnhookWinEvent(_minHook);
            _minHook = IntPtr.Zero;
        }

        _procRef = null;
    }

    public void Dispose() => Stop();

    // ---------- 事件回调 ----------

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // 只关心顶层窗口对象本身
        if (hwnd == IntPtr.Zero || idObject != OBJID_WINDOW || idChild != CHILDID_SELF)
            return;

        if (_suppress)
            return;

        try
        {
            switch (eventType)
            {
                case EVENT_OBJECT_LOCATIONCHANGE:
                    HandleLocationChange(hwnd);
                    break;
                case EVENT_OBJECT_DESTROY:
                    HandleDestroy(hwnd);
                    break;
                case EVENT_SYSTEM_MINIMIZESTART:
                    HandleMinimize(hwnd);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("处理窗口事件出错", ex);
        }
    }

    private void HandleLocationChange(IntPtr hwnd)
    {
        if (!ShouldConsider(hwnd))
        {
            _lastMaximized.Remove(hwnd);
            return;
        }

        bool isMax = IsMaximizedOrFullscreen(hwnd);
        bool was = _lastMaximized.TryGetValue(hwnd, out var v) && v;

        if (isMax == was)
            return; // 无状态变化

        _lastMaximized[hwnd] = isMax;
        Log.Write($"状态跳变 -> {(isMax ? "最大化/全屏" : "还原")}: {DescribeWindow(hwnd)}");

        if (isMax)
            MaximizeWindow(hwnd);
        else
            RestoreWindow(hwnd);
    }

    private void HandleDestroy(IntPtr hwnd)
    {
        _lastMaximized.Remove(hwnd);

        // 窗口在临时桌面上被关闭：切回原桌面并清理临时桌面
        if (_managed.TryGetValue(hwnd, out var info))
        {
            _managed.Remove(hwnd);
            RunSuppressed(() =>
            {
                info.Origin.MakeVisible();
                if (RemoveDesktopOnRestore)
                    SafeRemoveDesktop(info.Created, info.Origin);
            });
        }
    }

    /// <summary>受管窗口被最小化：把窗口移回原桌面、切回原桌面并删除临时桌面（窗口保持最小化）。</summary>
    private void HandleMinimize(IntPtr hwnd)
    {
        _lastMaximized.Remove(hwnd);

        if (!_managed.TryGetValue(hwnd, out var info))
            return;

        _managed.Remove(hwnd);
        RunSuppressed(() =>
        {
            try
            {
                // 把已最小化的窗口归还到原桌面，避免它随临时桌面一起消失
                if (IsWindow(hwnd))
                    info.Origin.MoveWindow(hwnd);

                info.Origin.MakeVisible();

                if (RemoveDesktopOnRestore)
                    SafeRemoveDesktop(info.Created, info.Origin);

                Log.Write($"窗口最小化，返回原桌面: {DescribeWindow(hwnd)}");
            }
            catch (Exception ex)
            {
                Log.Error("最小化返回原桌面失败", ex);
            }
        });
    }

    // ---------- 核心动作 ----------

    private void MaximizeWindow(IntPtr hwnd)
    {
        if (_managed.ContainsKey(hwnd))
            return;

        Desktop origin;
        try { origin = Desktop.FromWindow(hwnd); }
        catch { origin = Desktop.Current; }

        RunSuppressed(() =>
        {
            try
            {
                var created = Desktop.Create();
                created.MoveWindow(hwnd);
                created.MakeVisible();
                SetForegroundWindow(hwnd);

                _managed[hwnd] = new ManagedWindow(origin, created);
                Log.Write($"进入新桌面成功: {DescribeWindow(hwnd)}");
            }
            catch (Exception ex)
            {
                Log.Error("进入新桌面失败", ex);
            }
        });
    }

    private void RestoreWindow(IntPtr hwnd)
    {
        if (!_managed.TryGetValue(hwnd, out var info))
            return;

        _managed.Remove(hwnd);

        RunSuppressed(() =>
        {
            try
            {
                var created = info.Created;

                // 目标桌面：临时桌面的前一个（左侧）桌面；取不到则退回记录的原桌面
                Desktop target;
                try { target = created.Left ?? info.Origin; }
                catch { target = info.Origin; }

                // 移走本窗口前，先判断临时桌面上是否还有其它窗口
                bool hasOthers = DesktopHasOtherWindows(created, hwnd);

                if (IsWindow(hwnd))
                    target.MoveWindow(hwnd);

                target.MakeVisible();

                if (IsWindow(hwnd))
                    SetForegroundWindow(hwnd);

                // 仅当临时桌面已无其它窗口时才关闭它
                if (RemoveDesktopOnRestore && !hasOthers)
                    SafeRemoveDesktop(created, target);

                Log.Write($"缩小返回前一桌面{(hasOthers ? "（保留临时桌面，仍有其它窗口）" : "并关闭临时桌面")}: {DescribeWindow(hwnd)}");
            }
            catch (Exception ex)
            {
                Log.Error("缩小返回失败", ex);
            }
        });
    }

    /// <summary>枚举顶层窗口，判断指定桌面上除 <paramref name="except"/> 外是否还有其它可见的应用窗口。</summary>
    private static bool DesktopHasOtherWindows(Desktop desk, IntPtr except)
    {
        bool found = false;
        EnumWindows((h, _) =>
        {
            if (h == except || !IsCountableWindow(h))
                return true; // 继续枚举

            try
            {
                if (desk.HasWindow(h))
                {
                    found = true;
                    return false; // 找到一个即可停止
                }
            }
            catch { /* 个别窗口查询失败忽略 */ }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>用于“桌面是否还有其它窗口”统计的窗口过滤：可见、顶层、非工具窗口、未被隐藏、含标题栏。</summary>
    private static bool IsCountableWindow(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd))
            return false;
        if (GetAncestor(hwnd, GA_ROOT) != hwnd)
            return false;
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        long exStyle = GetWindowStyle(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return false;

        long style = GetWindowStyle(hwnd, GWL_STYLE);
        if ((style & WS_CAPTION) != WS_CAPTION)
            return false;

        return true;
    }

    private void SafeRemoveDesktop(Desktop target, Desktop fallback)
    {
        try
        {
            // 用 fallback 作为兜底目标，防止临时桌面上仍残留其它窗口
            target.Remove(fallback);
        }
        catch (Exception ex)
        {
            Log.Write($"删除临时桌面失败: {ex.Message}");
        }
    }

    /// <summary>执行由本程序发起的桌面操作，期间忽略自身触发的窗口事件。</summary>
    private void RunSuppressed(Action action)
    {
        _suppress = true;
        try { action(); }
        finally { _suppress = false; }
    }

    // ---------- 判定逻辑 ----------

    private bool ShouldConsider(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd))
            return false;

        // 必须是顶层窗口
        if (GetAncestor(hwnd, GA_ROOT) != hwnd)
            return false;

        // 被 DWM 隐藏(cloaked)的窗口忽略（例如后台 UWP、其它桌面上的窗口）
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        long exStyle = GetWindowStyle(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return false;

        if (RequireCaption)
        {
            long style = GetWindowStyle(hwnd, GWL_STYLE);
            if ((style & WS_CAPTION) != WS_CAPTION)
                return false;
        }

        return true;
    }

    private bool IsMaximizedOrFullscreen(IntPtr hwnd)
    {
        if (IsZoomed(hwnd))
            return true;

        if (HandleFullscreen && IsCoveringMonitor(hwnd))
            return true;

        return false;
    }

    /// <summary>窗口矩形是否完整覆盖了所在显示器（无边框全屏）。</summary>
    private static bool IsCoveringMonitor(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out var wr))
            return false;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref mi))
            return false;

        var m = mi.rcMonitor;
        return wr.Left <= m.Left && wr.Top <= m.Top && wr.Right >= m.Right && wr.Bottom >= m.Bottom;
    }

    private static string DescribeWindow(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetWindowText(hwnd, sb, sb.Capacity);
        return $"0x{hwnd.ToInt64():X} \"{sb}\"";
    }
}
