namespace Hl2CM.Trainer.Memory;

/// <summary>Minimal hand-assembler for the handful of x86 instructions our hooks need.</summary>
public enum Reg32
{
    Eax = 0, Ecx = 1, Edx = 2, Ebx = 3, Esp = 4, Ebp = 5, Esi = 6, Edi = 7,
}

public static class X86Asm
{
    /// <summary>`E9 rel32` — jmp from (address of this instruction) to <paramref name="target"/>.</summary>
    public static byte[] JmpRel32(IntPtr instructionAddress, IntPtr target)
    {
        int rel = (int)((long)target - ((long)instructionAddress + 5));
        var bytes = new byte[5];
        bytes[0] = 0xE9;
        BitConverter.GetBytes(rel).CopyTo(bytes, 1);
        return bytes;
    }

    /// <summary>`89 /r disp32` — mov [disp32], reg32 (absolute address, no base register).</summary>
    public static byte[] MovAbsFromReg(Reg32 reg, IntPtr disp32)
    {
        var bytes = new byte[6];
        bytes[0] = 0x89;
        bytes[1] = (byte)(0b00_000_101 | ((int)reg << 3)); // mod=00, rm=101 (disp32 only)
        BitConverter.GetBytes((int)disp32).CopyTo(bytes, 2);
        return bytes;
    }

    /// <summary>`BB imm32` family — mov reg32, imm32.</summary>
    public static byte[] MovRegImm32(Reg32 reg, int imm32)
    {
        var bytes = new byte[5];
        bytes[0] = (byte)(0xB8 + (int)reg);
        BitConverter.GetBytes(imm32).CopyTo(bytes, 1);
        return bytes;
    }

    public static byte[] Concat(params byte[][] chunks)
    {
        var total = chunks.Sum(c => c.Length);
        var result = new byte[total];
        var pos = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(result, pos);
            pos += chunk.Length;
        }
        return result;
    }

    public static readonly byte[] Nop = { 0x90 };
}
