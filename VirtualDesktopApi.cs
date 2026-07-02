// 虚拟桌面 COM 封装（内嵌，无外部 NuGet 依赖）。
// 来源：Markus Scholtes / VirtualDesktop（MIT），VirtualDesktop11.cs，v1.21。
// 适用于 Windows 11 23H2（build 22631，本机 22631.6199）。
// 注意：本 build 的 IVirtualDesktopManagerInternal 布局与 24H2（build 26100）不同——
// 24H2 在 SwitchDesktop 之后多了 SwitchDesktopAndMoveForegroundView，23H2 没有。
// 若误用 24H2 布局，FindDesktop 等靠后方法会 vtable 错位并触发 AccessViolationException。
// 相比动态解析 COM 接口的库（如 Grabacr07/VirtualDesktop 5.x），这里的接口 GUID 为该
// build 精确硬编码，规避了 "IApplicationView not present" 之类的兼容性异常。
#nullable disable
using System;
using System.Runtime.InteropServices;

namespace VirtualDesktop
{
    #region COM API
    internal static class Guids
    {
        public static readonly Guid CLSID_ImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        public static readonly Guid CLSID_VirtualDesktopManagerInternal = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
        public static readonly Guid CLSID_VirtualDesktopManager = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
        public static readonly Guid CLSID_VirtualDesktopPinnedApps = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Size { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    internal enum APPLICATION_VIEW_CLOAK_TYPE : int
    {
        AVCT_NONE = 0,
        AVCT_DEFAULT = 1,
        AVCT_VIRTUAL_DESKTOP = 2
    }

    internal enum APPLICATION_VIEW_COMPATIBILITY_POLICY : int
    {
        AVCP_NONE = 0,
        AVCP_SMALL_SCREEN = 1,
        AVCP_TABLET_SMALL_SCREEN = 2,
        AVCP_VERY_SMALL_SCREEN = 3,
        AVCP_HIGH_SCALE_FACTOR = 4
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
    internal interface IApplicationView
    {
        // 原始接口是 IInspectable，但 .NET Core/.NET 8 内置 COM 不支持 IInspectable 编组
        // （会抛 PlatformNotSupportedException）。故声明为 IUnknown，并在最前面手动补上
        // IInspectable 的 3 个方法占位，以保持后续方法的 vtable 槽位对齐。
        int GetIids(out uint iidCount, out IntPtr iids);
        int GetRuntimeClassName(out IntPtr className);
        int GetTrustLevel(out IntPtr trustLevel);
        int SetFocus();
        int SwitchTo();
        int TryInvokeBack(IntPtr callback);
        int GetThumbnailWindow(out IntPtr hwnd);
        int GetMonitor(out IntPtr immersiveMonitor);
        int GetVisibility(out int visibility);
        int SetCloak(APPLICATION_VIEW_CLOAK_TYPE cloakType, int unknown);
        int GetPosition(ref Guid guid, out IntPtr position);
        int SetPosition(ref IntPtr position);
        int InsertAfterWindow(IntPtr hwnd);
        int GetExtendedFramePosition(out Rect rect);
        int GetAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int SetAppUserModelId(string id);
        int IsEqualByAppUserModelId(string id, out int result);
        int GetViewState(out uint state);
        int SetViewState(uint state);
        int GetNeediness(out int neediness);
        int GetLastActivationTimestamp(out ulong timestamp);
        int SetLastActivationTimestamp(ulong timestamp);
        int GetVirtualDesktopId(out Guid guid);
        int SetVirtualDesktopId(ref Guid guid);
        int GetShowInSwitchers(out int flag);
        int SetShowInSwitchers(int flag);
        int GetScaleFactor(out int factor);
        int CanReceiveInput(out bool canReceiveInput);
        int GetCompatibilityPolicyType(out APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
        int SetCompatibilityPolicyType(APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
        int GetSizeConstraints(IntPtr monitor, out Size size1, out Size size2);
        int GetSizeConstraintsForDpi(uint uint1, out Size size1, out Size size2);
        int SetSizeConstraintsForDpi(ref uint uint1, ref Size size1, ref Size size2);
        int OnMinSizePreferencesUpdated(IntPtr hwnd);
        int ApplyOperation(IntPtr operation);
        int IsTray(out bool isTray);
        int IsInHighZOrderBand(out bool isInHighZOrderBand);
        int IsSplashScreenPresented(out bool isSplashScreenPresented);
        int Flash();
        int GetRootSwitchableOwner(out IApplicationView rootSwitchableOwner);
        int EnumerateOwnershipTree(out IObjectArray ownershipTree);
        int GetEnterpriseId([MarshalAs(UnmanagedType.LPWStr)] out string enterpriseId);
        int IsMirrored(out bool isMirrored);
        int Unknown1(out int unknown);
        int Unknown2(out int unknown);
        int Unknown3(out int unknown);
        int Unknown4(out int unknown);
        int Unknown5(out int unknown);
        int Unknown6(int unknown);
        int Unknown7();
        int Unknown8(out int unknown);
        int Unknown9(int unknown);
        int Unknown10(int unknownX, int unknownY);
        int Unknown11(int unknown);
        int Unknown12(out Size size1);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
    internal interface IApplicationViewCollection
    {
        int GetViews(out IObjectArray array);
        int GetViewsByZOrder(out IObjectArray array);
        int GetViewsByAppUserModelId(string id, out IObjectArray array);
        int GetViewForHwnd(IntPtr hwnd, out IApplicationView view);
        // 注意：.NET 8（Core）默认不支持裸 object（VARIANT/IDispatch）COM 编组，
        // 若这里用 object 会导致整个接口调用抛 PlatformNotSupportedException。
        // 本程序不使用下面两个方法，参数改为 IntPtr 以避免编组失败。
        int GetViewForApplication(IntPtr application, out IApplicationView view);
        int GetViewForAppUserModelId(string id, out IApplicationView view);
        int GetViewInFocus(out IntPtr view);
        int Unknown1(out IntPtr view);
        void RefreshCollection();
        int RegisterForApplicationViewChanges(IntPtr listener, out int cookie);
        int UnregisterForApplicationViewChanges(int cookie);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
    internal interface IVirtualDesktop
    {
        bool IsViewVisible(IApplicationView view);
        Guid GetId();
        [return: MarshalAs(UnmanagedType.HString)]
        string GetName();
        [return: MarshalAs(UnmanagedType.HString)]
        string GetWallpaperPath();
        bool IsRemote();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("53F5CA0B-158F-4124-900C-057158060B27")]
    internal interface IVirtualDesktopManagerInternal
    {
        int GetCount();
        void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
        bool CanViewMoveDesktops(IApplicationView view);
        IVirtualDesktop GetCurrentDesktop();
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
        void SwitchDesktop(IVirtualDesktop desktop);
        // 注意：Windows 11 23H2（build 22631）此接口中【没有】SwitchDesktopAndMoveForegroundView，
        // 该方法是 24H2（build 26100）才新增的。若在 23H2 上保留它，会导致其后所有方法
        // （CreateDesktop / RemoveDesktop / FindDesktop 等）vtable 槽位整体错位一格，
        // 调用即触发 AccessViolationException 崩溃。切勿在 23H2 布局中加回此行。
        IVirtualDesktop CreateDesktop();
        void MoveDesktop(IVirtualDesktop desktop, int nIndex);
        void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
        IVirtualDesktop FindDesktop(ref Guid desktopid);
        void GetDesktopSwitchIncludeExcludeViews(IVirtualDesktop desktop, out IObjectArray unknown1, out IObjectArray unknown2);
        void SetDesktopName(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string name);
        void SetDesktopWallpaper(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string path);
        void UpdateWallpaperPathForAllDesktops([MarshalAs(UnmanagedType.HString)] string path);
        void CopyDesktopState(IApplicationView pView0, IApplicationView pView1);
        void CreateRemoteDesktop([MarshalAs(UnmanagedType.HString)] string path, out IVirtualDesktop desktop);
        void SwitchRemoteDesktop(IVirtualDesktop desktop, IntPtr switchtype);
        void SwitchDesktopWithAnimation(IVirtualDesktop desktop);
        void GetLastActiveDesktop(out IVirtualDesktop desktop);
        void WaitForAnimationToComplete();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    internal interface IVirtualDesktopManager
    {
        bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
        Guid GetWindowDesktopId(IntPtr topLevelWindow);
        void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
    internal interface IVirtualDesktopPinnedApps
    {
        bool IsAppIdPinned(string appId);
        void PinAppID(string appId);
        void UnpinAppID(string appId);
        bool IsViewPinned(IApplicationView applicationView);
        void PinView(IApplicationView applicationView);
        void UnpinView(IApplicationView applicationView);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    internal interface IObjectArray
    {
        void GetCount(out int count);
        void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    internal interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }
    #endregion

    #region COM wrapper
    internal static class DesktopManager
    {
        static DesktopManager()
        {
            var shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell));
            VirtualDesktopManagerInternal = (IVirtualDesktopManagerInternal)shell.QueryService(Guids.CLSID_VirtualDesktopManagerInternal, typeof(IVirtualDesktopManagerInternal).GUID);
            VirtualDesktopManager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_VirtualDesktopManager));
            ApplicationViewCollection = (IApplicationViewCollection)shell.QueryService(typeof(IApplicationViewCollection).GUID, typeof(IApplicationViewCollection).GUID);
            VirtualDesktopPinnedApps = (IVirtualDesktopPinnedApps)shell.QueryService(Guids.CLSID_VirtualDesktopPinnedApps, typeof(IVirtualDesktopPinnedApps).GUID);
        }

        internal static IVirtualDesktopManagerInternal VirtualDesktopManagerInternal;
        internal static IVirtualDesktopManager VirtualDesktopManager;
        internal static IApplicationViewCollection ApplicationViewCollection;
        internal static IVirtualDesktopPinnedApps VirtualDesktopPinnedApps;

        internal static IVirtualDesktop GetDesktop(int index)
        {
            int count = VirtualDesktopManagerInternal.GetCount();
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException("index");
            IObjectArray desktops;
            VirtualDesktopManagerInternal.GetDesktops(out desktops);
            object objdesktop;
            desktops.GetAt(index, typeof(IVirtualDesktop).GUID, out objdesktop);
            Marshal.ReleaseComObject(desktops);
            return (IVirtualDesktop)objdesktop;
        }

        internal static int GetDesktopIndex(IVirtualDesktop desktop)
        {
            int index = -1;
            Guid IdSearch = desktop.GetId();
            IObjectArray desktops;
            VirtualDesktopManagerInternal.GetDesktops(out desktops);
            object objdesktop;
            for (int i = 0; i < VirtualDesktopManagerInternal.GetCount(); i++)
            {
                desktops.GetAt(i, typeof(IVirtualDesktop).GUID, out objdesktop);
                if (IdSearch.CompareTo(((IVirtualDesktop)objdesktop).GetId()) == 0)
                {
                    index = i;
                    break;
                }
            }
            Marshal.ReleaseComObject(desktops);
            return index;
        }

        internal static IApplicationView GetApplicationView(this IntPtr hWnd)
        {
            IApplicationView view;
            ApplicationViewCollection.GetViewForHwnd(hWnd, out view);
            return view;
        }

        internal static string GetAppId(IntPtr hWnd)
        {
            string appId;
            hWnd.GetApplicationView().GetAppUserModelId(out appId);
            return appId;
        }
    }
    #endregion

    #region public interface
    public class Desktop
    {
        [DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_MINIMIZE = 6;
        private static readonly Guid AppOnAllDesktops = new Guid("BB64D5B7-4DE3-4AB2-A87C-DB7601AEA7DC");
        private static readonly Guid WindowOnAllDesktops = new Guid("C2DDEA68-66F2-4CF9-8264-1BFD00FBBBAC");

        private IVirtualDesktop ivd;
        private Desktop(IVirtualDesktop desktop) { this.ivd = desktop; }

        public override int GetHashCode() => ivd.GetHashCode();

        public override bool Equals(object obj)
        {
            var desk = obj as Desktop;
            return desk != null && object.ReferenceEquals(this.ivd, desk.ivd);
        }

        public static int Count => DesktopManager.VirtualDesktopManagerInternal.GetCount();

        public static Desktop Current => new Desktop(DesktopManager.VirtualDesktopManagerInternal.GetCurrentDesktop());

        public static Desktop FromIndex(int index) => new Desktop(DesktopManager.GetDesktop(index));

        public static Desktop FromWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
            Guid id = DesktopManager.VirtualDesktopManager.GetWindowDesktopId(hWnd);
            if ((id.CompareTo(AppOnAllDesktops) == 0) || (id.CompareTo(WindowOnAllDesktops) == 0))
                return new Desktop(DesktopManager.VirtualDesktopManagerInternal.GetCurrentDesktop());
            else
                return new Desktop(DesktopManager.VirtualDesktopManagerInternal.FindDesktop(ref id));
        }

        public static int FromDesktop(Desktop desktop) => DesktopManager.GetDesktopIndex(desktop.ivd);

        public static Desktop Create() => new Desktop(DesktopManager.VirtualDesktopManagerInternal.CreateDesktop());

        public void Remove(Desktop fallback = null)
        {
            IVirtualDesktop fallbackdesktop;
            if (fallback == null)
            {
                Desktop dtToCheck = new Desktop(DesktopManager.GetDesktop(0));
                if (this.Equals(dtToCheck))
                    DesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 4, out fallbackdesktop); // 4 = Right
                else
                    DesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 3, out fallbackdesktop); // 3 = Left
            }
            else
                fallbackdesktop = fallback.ivd;
            DesktopManager.VirtualDesktopManagerInternal.RemoveDesktop(ivd, fallbackdesktop);
        }

        public bool IsVisible => object.ReferenceEquals(ivd, DesktopManager.VirtualDesktopManagerInternal.GetCurrentDesktop());

        private static bool AnimateDesktopSwitch = true;
        public static void SetAnimation(bool OnOff) => AnimateDesktopSwitch = OnOff;

        public void MakeVisible()
        {
            IntPtr hWnd;
            if (AnimateDesktopSwitch)
                hWnd = FindWindow("Shell_TrayWnd", "");
            else
                hWnd = FindWindow("XamlExplorerHostIslandWindow", null);

            if (hWnd != (IntPtr)0)
            {
                int dummy;
                uint DesktopThreadId = GetWindowThreadProcessId(hWnd, out dummy);
                uint ForegroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out dummy);
                uint CurrentThreadId = GetCurrentThreadId();
                if ((DesktopThreadId != 0) && (ForegroundThreadId != 0) && (ForegroundThreadId != CurrentThreadId))
                {
                    AttachThreadInput(DesktopThreadId, CurrentThreadId, true);
                    AttachThreadInput(ForegroundThreadId, CurrentThreadId, true);
                    SetForegroundWindow(hWnd);
                    AttachThreadInput(ForegroundThreadId, CurrentThreadId, false);
                    AttachThreadInput(DesktopThreadId, CurrentThreadId, false);
                }
            }

            DesktopManager.VirtualDesktopManagerInternal.WaitForAnimationToComplete();
            if (AnimateDesktopSwitch)
            {
                DesktopManager.VirtualDesktopManagerInternal.SwitchDesktopWithAnimation(ivd);
                DesktopManager.VirtualDesktopManagerInternal.WaitForAnimationToComplete();
            }
            else
            {
                DesktopManager.VirtualDesktopManagerInternal.SwitchDesktop(ivd);
            }

            if (hWnd != (IntPtr)0)
                ShowWindow(hWnd, SW_MINIMIZE);
        }

        public Desktop Left
        {
            get
            {
                IVirtualDesktop desktop;
                int hr = DesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 3, out desktop); // 3 = Left
                return hr == 0 ? new Desktop(desktop) : null;
            }
        }

        public Desktop Right
        {
            get
            {
                IVirtualDesktop desktop;
                int hr = DesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 4, out desktop); // 4 = Right
                return hr == 0 ? new Desktop(desktop) : null;
            }
        }

        public void MoveWindow(IntPtr hWnd)
        {
            int processId;
            if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
            GetWindowThreadProcessId(hWnd, out processId);

            if (System.Diagnostics.Process.GetCurrentProcess().Id == processId)
            {
                try // 本进程窗口且我们是所有者：走简单路径
                {
                    DesktopManager.VirtualDesktopManager.MoveWindowToDesktop(hWnd, ivd.GetId());
                }
                catch
                {
                    IApplicationView view;
                    DesktopManager.ApplicationViewCollection.GetViewForHwnd(hWnd, out view);
                    DesktopManager.VirtualDesktopManagerInternal.MoveViewToDesktop(view, ivd);
                }
            }
            else
            {
                IApplicationView view;
                DesktopManager.ApplicationViewCollection.GetViewForHwnd(hWnd, out view);
                try
                {
                    DesktopManager.VirtualDesktopManagerInternal.MoveViewToDesktop(view, ivd);
                }
                catch
                {
                    DesktopManager.ApplicationViewCollection.GetViewForHwnd(
                        System.Diagnostics.Process.GetProcessById(processId).MainWindowHandle, out view);
                    DesktopManager.VirtualDesktopManagerInternal.MoveViewToDesktop(view, ivd);
                }
            }
        }

        public bool HasWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
            Guid id = DesktopManager.VirtualDesktopManager.GetWindowDesktopId(hWnd);
            if ((id.CompareTo(AppOnAllDesktops) == 0) || (id.CompareTo(WindowOnAllDesktops) == 0))
                return true;
            else
                return ivd.GetId() == id;
        }
    }
    #endregion
}
