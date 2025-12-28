using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace GSRP.Daemon.Services
{
    public class ClipboardMonitor : IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
        private const uint CF_UNICODETEXT = 13;

        [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr CreateWindowEx(int e, string c, string n, int s, int x, int y, int w, int h, IntPtr p, IntPtr m, IntPtr i, IntPtr lp);
        [DllImport("user32.dll")] private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMin, uint wMax);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);

        [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int pt_x; public int pt_y; }

        public event Action<string>? ClipboardChanged;
        private Thread? _thread;
        private volatile bool _running;

        public void Start() {
            _running = true;
            _thread = new Thread(Run) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        private void Run() {
            IntPtr hwnd = CreateWindowEx(0, "Static", "GSRP_Mon", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) return;
            AddClipboardFormatListener(hwnd);
            MSG msg;
            while (_running && GetMessage(out msg, IntPtr.Zero, 0, 0) != 0) {
                if (msg.message == WM_CLIPBOARDUPDATE) {
                    string t = GetText();
                    if (!string.IsNullOrEmpty(t)) ClipboardChanged?.Invoke(t);
                }
                DispatchMessage(ref msg);
            }
            RemoveClipboardFormatListener(hwnd);
        }

        private string GetText() {
            if (!OpenClipboard(IntPtr.Zero)) return "";
            try {
                IntPtr h = GetClipboardData(CF_UNICODETEXT);
                if (h == IntPtr.Zero) return "";
                IntPtr p = GlobalLock(h);
                try { return Marshal.PtrToStringUni(p) ?? ""; }
                finally { GlobalUnlock(h); }
            } finally { CloseClipboard(); }
        }

        public void Dispose() => _running = false;
    }
}
