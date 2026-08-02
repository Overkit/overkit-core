using System.Runtime.InteropServices;

namespace Overkit.Host.Ui;

public static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;

    public const int WS_EX_TOOLWINDOW = 0x0000_0080;
    public const int WS_EX_LAYERED = 0x0008_0000;
    public const int WS_EX_TRANSPARENT = 0x0000_0020;
    public const int WS_EX_NOACTIVATE = 0x0800_0000;

    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_NONE = 0x0000;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>ClipCursor(IntPtr.Zero) libère le curseur d'un éventuel confinement posé par le jeu.</summary>
    [DllImport("user32.dll")]
    public static extern bool ClipCursor(IntPtr lpRect);
}
