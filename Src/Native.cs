using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Ludens
{
    public static class Native
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 2;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int command);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OsVersionInfo info);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OsVersionInfo
        {
            public int Size;
            public int Major;
            public int Minor;
            public int Build;
            public int Platform;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ServicePack;
        }

        private static int _build = -1;

        public static int WindowsBuild
        {
            get
            {
                if (_build < 0)
                {
                    try
                    {
                        OsVersionInfo info = new OsVersionInfo();
                        info.Size = Marshal.SizeOf(typeof(OsVersionInfo));
                        RtlGetVersion(ref info);
                        _build = info.Build;
                    }
                    catch { _build = 0; }
                }
                return _build;
            }
        }

        public static bool IsWindows11
        {
            get { return WindowsBuild >= 22000; }
        }

        public static IntPtr Handle(Window window)
        {
            return new WindowInteropHelper(window).Handle;
        }

        public static void DragWindow(Window window)
        {
            IntPtr hwnd = Handle(window);
            if (hwnd == IntPtr.Zero) return;
            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        public static void ApplyWindowStyle(Window window, bool dark)
        {
            try
            {
                IntPtr hwnd = Handle(window);
                if (hwnd == IntPtr.Zero) return;
                int corner = 2;
                DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
                int darkMode = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
                int caption = dark ? 0x00303030 : 0x00F5F5F5;
                DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
            }
            catch { }
        }

        public static void BringToForeground(Window window)
        {
            try
            {
                IntPtr hwnd = Handle(window);
                if (hwnd == IntPtr.Zero) return;
                if (IsIconic(hwnd)) ShowWindowAsync(hwnd, 9);
                SetForegroundWindow(hwnd);
            }
            catch { }
        }

        public static uint WindowProcessId(IntPtr hwnd)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }
    }
}
