using System;
using System.Runtime.InteropServices;

namespace SplitExplorer
{
    internal static class ShellIds
    {
        public static readonly Guid ExplorerBrowserClsid =
            new Guid("71F96385-DDD6-48D3-A0C1-AE06E8B055FB");

        public static readonly Guid IID_IExplorerBrowser =
            new Guid("DFD3B6B5-C10C-4BE9-85F6-A66969F402F6");

        public static readonly Guid IID_IShellItem =
            new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");

        public static readonly Guid IID_IShellView =
            new Guid("000214E3-0000-0000-C000-000000000046");
    }

    [ComImport]
    [Guid("DFD3B6B5-C10C-4BE9-85F6-A66969F402F6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerBrowser
    {
        void Initialize(IntPtr hwndParent, ref Native.RECT prc, ref Native.FOLDERSETTINGS pfs);
        void Destroy();
        void SetRect(ref IntPtr phdwp, Native.RECT rcBrowser);
        void SetPropertyBag([MarshalAs(UnmanagedType.LPWStr)] string pszPropertyBag);
        void SetEmptyText([MarshalAs(UnmanagedType.LPWStr)] string pszEmptyText);
        void SetFolderSettings(ref Native.FOLDERSETTINGS pfs);
        void Advise(IExplorerBrowserEvents psbe, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint dwFlag);
        void GetOptions(out uint pdwFlag);
        void BrowseToIDList(IntPtr pidl, uint uFlags);
        void BrowseToObject([MarshalAs(UnmanagedType.IUnknown)] object punk, uint uFlags);
        void FillFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, int dwFlags);
        void RemoveAll();
        void GetCurrentView(ref Guid riid, out IntPtr ppv);
    }

    [ComImport]
    [Guid("000214E3-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellView
    {
        [PreserveSig]
        int GetWindow(out IntPtr hwnd);

        [PreserveSig]
        int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enterMode);

        [PreserveSig]
        int TranslateAccelerator(ref Native.MSG msg);

        [PreserveSig]
        int EnableModeless([MarshalAs(UnmanagedType.Bool)] bool enable);

        [PreserveSig]
        int UIActivate(uint state);

        [PreserveSig]
        int Refresh();
    }

    [ComImport]
    [Guid("361BBDC7-E6EE-4E13-BE58-58E2240C810F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerBrowserEvents
    {
        void OnNavigationPending(IntPtr pidlFolder);
        void OnViewCreated([MarshalAs(UnmanagedType.IUnknown)] object psv);
        void OnNavigationComplete(IntPtr pidlFolder);
        void OnNavigationFailed(IntPtr pidlFolder);
    }

    [ComImport]
    [Guid("68284FAA-6A48-11D0-8C78-00C04FD918B4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInputObject
    {
        [PreserveSig]
        int UIActivateIO(int fActivate, ref Native.MSG pMsg);

        [PreserveSig]
        int HasFocusIO();

        [PreserveSig]
        int TranslateAcceleratorIO(ref Native.MSG pMsg);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class ExplorerBrowserEvents : IExplorerBrowserEvents
    {
        private ExplorerHost _owner;

        internal ExplorerBrowserEvents(ExplorerHost owner)
        {
            _owner = owner;
        }

        internal void Detach()
        {
            _owner = null;
        }

        public void OnNavigationPending(IntPtr pidlFolder)
        {
        }

        public void OnViewCreated(object psv)
        {
        }

        public void OnNavigationComplete(IntPtr pidlFolder)
        {
            try
            {
                ExplorerHost owner = _owner;
                if (owner != null)
                {
                    owner.HandleNavigationComplete(pidlFolder);
                }
            }
            catch (Exception)
            {
            }
        }

        public void OnNavigationFailed(IntPtr pidlFolder)
        {
            try
            {
                ExplorerHost owner = _owner;
                if (owner != null)
                {
                    owner.HandleNavigationFailed(pidlFolder);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    internal static class ComUtil
    {
        public static void Release(object comObject)
        {
            if (comObject == null)
            {
                return;
            }
            try
            {
                int count;
                do
                {
                    count = Marshal.ReleaseComObject(comObject);
                }
                while (count > 0);
            }
            catch (Exception)
            {
            }
        }
    }
}