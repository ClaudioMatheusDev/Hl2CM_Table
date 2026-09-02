using System.Diagnostics;
using System.Runtime.InteropServices;
using Hl2CM.Trainer.Native;

namespace Hl2CM.Trainer.Memory;

/// <summary>
/// Wraps a handle to an external process and provides typed read/write helpers.
/// Mirrors what Cheat Engine does under the hood via ReadProcessMemory/WriteProcessMemory.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    public IntPtr Handle { get; }
    public Process Process { get; }

    private ProcessMemory(Process process, IntPtr handle)
    {
        Process = process;
        Handle = handle;
    }

    public static ProcessMemory? TryAttach(Process process)
    {
        var handle = NativeMethods.OpenProcess(ProcessAccess.All, false, process.Id);
        return handle == IntPtr.Zero ? null : new ProcessMemory(process, handle);
    }

    /// <summary>Base address of a loaded module (e.g. "server.dll") inside the target process.</summary>
    public IntPtr GetModuleBase(string moduleName)
    {
        Process.Refresh();
        foreach (ProcessModule module in Process.Modules)
        {
            if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                return module.BaseAddress;
        }
        throw new InvalidOperationException($"Module '{moduleName}' not found in process {Process.ProcessName} (pid {Process.Id}).");
    }

    public byte[] ReadBytes(IntPtr address, int length)
    {
        var buffer = new byte[length];
        if (!NativeMethods.ReadProcessMemory(Handle, address, buffer, length, out _))
            throw new InvalidOperationException($"ReadProcessMemory failed at 0x{address:X} (Win32 error {Marshal.GetLastWin32Error()}).");
        return buffer;
    }

    public bool TryReadBytes(IntPtr address, int length, out byte[] buffer)
    {
        buffer = new byte[length];
        return NativeMethods.ReadProcessMemory(Handle, address, buffer, length, out _);
    }

    public void WriteBytes(IntPtr address, byte[] data)
    {
        if (!NativeMethods.WriteProcessMemory(Handle, address, data, data.Length, out _))
            throw new InvalidOperationException($"WriteProcessMemory failed at 0x{address:X} (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public int ReadInt32(IntPtr address) => BitConverter.ToInt32(ReadBytes(address, 4));
    public uint ReadUInt32(IntPtr address) => BitConverter.ToUInt32(ReadBytes(address, 4));
    public float ReadFloat(IntPtr address) => BitConverter.ToSingle(ReadBytes(address, 4));

    /// <summary>Reads a 32-bit pointer value stored at <paramref name="address"/> (the game is a 32-bit process).</summary>
    public IntPtr ReadPointer(IntPtr address) => new(ReadUInt32(address));

    public void WriteInt32(IntPtr address, int value) => WriteBytes(address, BitConverter.GetBytes(value));
    public void WriteFloat(IntPtr address, float value) => WriteBytes(address, BitConverter.GetBytes(value));

    public IntPtr Allocate(uint size) =>
        NativeMethods.VirtualAllocEx(Handle, IntPtr.Zero, size, AllocationType.Commit | AllocationType.Reserve, MemoryProtection.ExecuteReadWrite);

    public void Free(IntPtr address) => NativeMethods.VirtualFreeEx(Handle, address, 0, AllocationType.Release);

    public void Dispose() => NativeMethods.CloseHandle(Handle);
}
