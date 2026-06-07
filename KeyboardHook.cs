using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Blitztext;

/// <summary>
/// Globaler Low-Level-Tastatur-Haken (WH_KEYBOARD_LL) für Push-to-talk. Meldet KeyDown/KeyUp als
/// Windows Virtual-Key-Codes (int) – ohne WinForms. Der Callback muss schnell zurückkehren,
/// deshalb wird hier nur gemeldet/geschluckt; schwere Arbeit läuft danach.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int LLKHF_INJECTED = 0x10;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    public event Action<int>? KeyPressed;
    public event Action<int>? KeyReleased;

    /// <summary>true = Taste NICHT an andere Programme weiterreichen (eigene Hotkeys schlucken).</summary>
    public Func<int, bool>? SwallowKey;

    public KeyboardHook()
    {
        _proc = HookCallback;
        _hookId = SetHook(_proc);
        if (_hookId == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Tastatur-Hook konnte nicht installiert werden (Win32 {error}).");
        }
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                int flags = Marshal.ReadInt32(lParam, 8); // x64: vkCode(4)+scanCode(4) -> flags bei Offset 8
                if ((flags & LLKHF_INJECTED) == 0) // selbst erzeugte Tasten (eigenes Strg+V) ignorieren
                {
                    int message = (int)wParam;
                    int vkCode = Marshal.ReadInt32(lParam);

                    bool isDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
                    bool isUp = message == WM_KEYUP || message == WM_SYSKEYUP;

                    if (isDown) KeyPressed?.Invoke(vkCode);
                    else if (isUp) KeyReleased?.Invoke(vkCode);

                    if ((isDown || isUp) && SwallowKey != null && SwallowKey(vkCode))
                        return (IntPtr)1;
                }
            }
            catch
            {
                // Im Hook-Callback NIE eine Ausnahme über die native Grenze lassen.
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
