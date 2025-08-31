using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace GSRP.Services
{
    public class ClipboardService : IClipboardService
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int DebounceMilliseconds = 300; // Wait 300ms after the last clipboard change
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        private HwndSource? _hwndSource;
        private bool _isListening;
        private readonly object _lock = new object();
        private Timer? _debounceTimer;

        private volatile bool _isProcessingClipboard;

        public event EventHandler<string>? ClipboardChanged;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public void StartListening()
        {
            lock (_lock)
            {
                if (_isListening) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var hwndSourceParameters = new HwndSourceParameters("ClipboardMonitor")
                        {
                            HwndSourceHook = WndProc,
                            ParentWindow = HWND_MESSAGE
                        };

                        _hwndSource = new HwndSource(hwndSourceParameters);

                        if (AddClipboardFormatListener(_hwndSource.Handle))
                        {
                            _debounceTimer = new Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
                            _isListening = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ClipboardService] Failed to start: {ex.Message}");
                        _isListening = false;
                    }
                });
            }
        }

        public void StopListening()
        {
            lock (_lock)
            {
                if (!_isListening) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (_hwndSource != null)
                        {
                            RemoveClipboardFormatListener(_hwndSource.Handle);
                            _hwndSource.Dispose();
                            _hwndSource = null;
                        }
                        _debounceTimer?.Dispose();
                        _debounceTimer = null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ClipboardService] Error during cleanup: {ex.Message}");
                    }
                    finally
                    {
                        _isListening = false;
                    }
                });
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                // On each clipboard update, just reset the timer to fire after the debounce period.
                _debounceTimer?.Change(DebounceMilliseconds, Timeout.Infinite);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnTimerElapsed(object? state)
        {
            // Don't process if a long-running operation is already in progress.
            if (_isProcessingClipboard) return;

            try
            {
                _isProcessingClipboard = true;

                // The timer callback runs on a background thread, so we get the text
                // and then invoke the event for the MainViewModel to handle.
                var clipboardText = GetClipboardText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    ClipboardChanged?.Invoke(this, clipboardText);
                }
            }
            catch (Exception ex)
            {
                // Log and ignore to prevent the service from crashing.
                System.Diagnostics.Debug.WriteLine($"[ClipboardService] Error processing clipboard: {ex.Message}");
            }
            finally
            {
                // Allow the next event to be processed.
                _isProcessingClipboard = false;
            }
        }

        public static string GetClipboardText()
        {
            string text = string.Empty;
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        text = Clipboard.GetText() ?? string.Empty;
                    }
                }
                catch (Exception)
                {
                    text = string.Empty;
                }
            });
            return text;
        }

        public static void SetClipboardText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception)
                {
                    // Error setting clipboard
                }
            });
        }

        public bool IsListening => _isListening;

        public void Dispose()
        {
            StopListening();
        }
    }

    // This utility class seems redundant when DI is used, but we'll leave it for now.
    public class ClipboardUtils
    {
        public static string GetClipboardText() => ClipboardService.GetClipboardText();
        public static void SetClipboardText(string text) => ClipboardService.SetClipboardText(text);
    }
}