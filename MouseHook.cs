using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Blitztext;

internal sealed class MouseHook : IDisposable
{
    private readonly HookProc _proc;
    private readonly Func<int, bool, bool> _input;
    private IntPtr _handle;
    internal const int MiddleDown = 0x0207, MiddleUp = 0x0208;

    public MouseHook(Func<int, bool, bool> input)
    {
        _input = input;
        _proc = Callback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _handle = SetWindowsHookEx(14, _proc, GetModuleHandle(module.ModuleName), 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException($"Maus-Hook konnte nicht installiert werden (Win32 {Marshal.GetLastWin32Error()}).");
    }

    internal static bool IsPhysicalMiddle(int message, uint flags) =>
        (message == MiddleDown || message == MiddleUp) && (flags & 1) == 0;

    private IntPtr Callback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && ((int)message == MiddleDown || (int)message == MiddleUp))
        {
            try
            {
                var info = Marshal.PtrToStructure<MouseData>(data);
                if (IsPhysicalMiddle((int)message, info.Flags) &&
                    _input(DictationShortcut.MiddleButton, (int)message == MiddleDown)) return (IntPtr)1;
            }
            catch { /* Never propagate managed exceptions across the native boundary. */ }
        }
        return CallNextHookEx(_handle, code, message, data);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
        GC.KeepAlive(_proc);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData { public int X, Y; public uint MouseDataValue, Flags, Time; public UIntPtr ExtraInfo; }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string name);
}
