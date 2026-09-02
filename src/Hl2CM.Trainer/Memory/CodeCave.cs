namespace Hl2CM.Trainer.Memory;

/// <summary>
/// C# equivalent of a Cheat Engine "Auto Assembler" [ENABLE]/[DISABLE] block:
/// backs up the original bytes at <paramref name="hookAddress"/>, and when enabled,
/// overwrites them with a jmp into an allocated "code cave" that runs <c>caveBody</c>
/// and then jumps back to right after the hook. Disable restores the original bytes
/// and frees the cave — exactly like the CT table's [DISABLE] section.
/// </summary>
public sealed class CodeCave : IDisposable
{
    private readonly ProcessMemory _pm;
    private readonly IntPtr _hookAddress;
    private readonly int _hookLength;
    private readonly byte[] _originalBytes;
    private readonly byte[] _caveBody;
    private IntPtr _caveAddress;

    public bool IsEnabled { get; private set; }
    public string Name { get; }

    private CodeCave(ProcessMemory pm, string name, IntPtr hookAddress, int hookLength, byte[] originalBytes, byte[] caveBody)
    {
        _pm = pm;
        Name = name;
        _hookAddress = hookAddress;
        _hookLength = hookLength;
        _originalBytes = originalBytes;
        _caveBody = caveBody;
    }

    /// <summary>
    /// Validates the bytes currently at <paramref name="hookAddress"/> match what the cheat
    /// table expects (same game build) before allowing Enable() — refuses to patch blindly.
    /// </summary>
    public static CodeCave Prepare(ProcessMemory pm, string name, IntPtr hookAddress, int hookLength, byte[] expectedOriginalBytes, byte[] caveBody)
    {
        if (expectedOriginalBytes.Length != hookLength)
            throw new ArgumentException("expectedOriginalBytes length must equal hookLength.");

        if (hookLength < 5)
            throw new ArgumentException("hookLength must be at least 5 bytes (size of a jmp rel32).");

        if (!PatternScanner.VerifyOriginalBytes(pm, hookAddress, expectedOriginalBytes))
        {
            throw new InvalidOperationException(
                $"[{name}] Bytes at 0x{hookAddress:X} don't match the expected 'v24 build 2257546' signature. " +
                "The game version differs from the one this cheat table targets — refusing to patch to avoid a crash.");
        }

        return new CodeCave(pm, name, hookAddress, hookLength, expectedOriginalBytes, caveBody);
    }

    public void Enable()
    {
        if (IsEnabled) return;

        var returnAddress = IntPtr.Add(_hookAddress, _hookLength);

        // cave = [caveBody] + [jmp back to returnAddress]
        _caveAddress = _pm.Allocate((uint)(_caveBody.Length + 5));
        var jmpBack = X86Asm.JmpRel32(IntPtr.Add(_caveAddress, _caveBody.Length), returnAddress);
        _pm.WriteBytes(_caveAddress, X86Asm.Concat(_caveBody, jmpBack));

        // hook site = jmp to cave, NOP-padded to hookLength
        var jmpToCave = X86Asm.JmpRel32(_hookAddress, _caveAddress);
        var patch = new byte[_hookLength];
        jmpToCave.CopyTo(patch, 0);
        for (int i = 5; i < _hookLength; i++) patch[i] = 0x90;
        _pm.WriteBytes(_hookAddress, patch);

        IsEnabled = true;
    }

    public void Disable()
    {
        if (!IsEnabled) return;

        _pm.WriteBytes(_hookAddress, _originalBytes);
        _pm.Free(_caveAddress);
        _caveAddress = IntPtr.Zero;

        IsEnabled = false;
    }

    public void Dispose() => Disable();
}
