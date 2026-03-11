using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace TimeTracker.App;

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int HotkeyId = 0x1000;
    private const int WmHotkey = 0x0312;

    private readonly Action _callback;
    private readonly HwndSource _source;
    private bool _registered;

    public GlobalHotkeyManager(Action callback)
    {
        _callback = callback;

        var parameters = new HwndSourceParameters("CopilotTimeTrackerHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public void Register(string gestureText)
    {
        Unregister();

        if (!TryParse(gestureText, out var modifiers, out var key))
        {
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        _registered = RegisterHotKey(_source.Handle, HotkeyId, modifiers, virtualKey);
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _callback();
        }

        return IntPtr.Zero;
    }

    private static bool TryParse(string gestureText, out uint modifiers, out Key key)
    {
        modifiers = 0;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(gestureText))
        {
            return false;
        }

        var segments = gestureText.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || segment.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0002;
            }
            else if (segment.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0001;
            }
            else if (segment.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0004;
            }
            else if (segment.Equals("Win", StringComparison.OrdinalIgnoreCase) || segment.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= 0x0008;
            }
            else if (!Enum.TryParse(segment, ignoreCase: true, out key))
            {
                return false;
            }
        }

        return key != Key.None;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
