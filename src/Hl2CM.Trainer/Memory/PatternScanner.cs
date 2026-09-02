namespace Hl2CM.Trainer.Memory;

public static class PatternScanner
{
    /// <summary>
    /// Compares the bytes currently in the target process at <paramref name="address"/>
    /// against the bytes the cheat table was built against. If they don't match, the game
    /// version differs from "v24, build 2257546" and patching would corrupt/crash it.
    /// </summary>
    public static bool VerifyOriginalBytes(ProcessMemory pm, IntPtr address, byte[] expected)
    {
        if (!pm.TryReadBytes(address, expected.Length, out var actual))
            return false;

        return actual.AsSpan().SequenceEqual(expected);
    }

    /// <summary>Simple wildcard AOB scan (IDA-style "AA ?? BB" pattern) over a memory region already read into <paramref name="haystack"/>.</summary>
    public static int? Find(byte[] haystack, string pattern)
    {
        var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var needle = new byte?[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            needle[i] = parts[i] == "??" || parts[i] == "?" ? null : Convert.ToByte(parts[i], 16);

        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (needle[j].HasValue && haystack[i + j] != needle[j]!.Value)
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return null;
    }
}
