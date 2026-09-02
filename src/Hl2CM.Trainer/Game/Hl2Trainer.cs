using System.Diagnostics;
using Hl2CM.Trainer.Memory;

namespace Hl2CM.Trainer.Game;

/// <summary>
/// High-level C# port of the hl2.CT Cheat Engine table for Half-Life 2 (server.dll,
/// v24 build 2257546). Everything here mirrors one entry from the original table:
///
///  - PlayerStruct / AmmoClip: the two "pointer capture" hooks the table installs so
///    memrec.Child[0].Address can be re-read every tick (here we just re-read the
///    4-byte cell the hook writes into, on demand).
///  - InfAmmoPrimary/Secondary/Health/SuitArmor: the four "force a constant value"
///    code-cave patches.
/// </summary>
public sealed class Hl2Trainer : IDisposable
{
    private const string ModuleName = "server.dll";

    private readonly ProcessMemory _pm;
    private readonly IntPtr _serverDll;

    private readonly IntPtr _playerStructCell; // holds the captured EBX (player struct pointer)
    private readonly IntPtr _ammoClipCell;     // holds the captured ESI (current weapon's ammo struct pointer)

    private readonly Dictionary<string, CodeCave> _hooks = new();

    private Hl2Trainer(ProcessMemory pm)
    {
        _pm = pm;
        _serverDll = pm.GetModuleBase(ModuleName);

        _playerStructCell = _pm.Allocate(4);
        _ammoClipCell = _pm.Allocate(4);

        PrepareHooks();
    }

    /// <summary>Attaches to a specific, already-picked process. Returns null if the OS denies the handle.</summary>
    public static Hl2Trainer? TryAttach(Process process)
    {
        var pm = ProcessMemory.TryAttach(process);
        return pm is null ? null : new Hl2Trainer(pm);
    }

    public string ProcessDescription => $"{_pm.Process.ProcessName} (pid {_pm.Process.Id})";

    private IntPtr Server(int fileOffset) => IntPtr.Add(_serverDll, fileOffset);

    private void PrepareHooks()
    {
        // --- Pointer captures (must be enabled before anything else works) ---

        // server.dll+1FE8DE: cmp dword ptr [ebx+0E0],00  (7 bytes)
        _hooks["PlayerStruct"] = CodeCave.Prepare(
            _pm, "PlayerStruct",
            hookAddress: Server(0x1FE8DE),
            hookLength: 7,
            expectedOriginalBytes: new byte[] { 0x83, 0xBB, 0xE0, 0x00, 0x00, 0x00, 0x00 },
            caveBody: X86Asm.Concat(
                X86Asm.MovAbsFromReg(Reg32.Ebx, _playerStructCell),
                new byte[] { 0x83, 0xBB, 0xE0, 0x00, 0x00, 0x00, 0x00 })); // re-run the original cmp

        // server.dll+F5901: cmp dword ptr [esi+4AC],00  (7 bytes)
        _hooks["AmmoClip"] = CodeCave.Prepare(
            _pm, "AmmoClip",
            hookAddress: Server(0xF5901),
            hookLength: 7,
            expectedOriginalBytes: new byte[] { 0x83, 0xBE, 0xAC, 0x04, 0x00, 0x00, 0x00 },
            caveBody: X86Asm.Concat(
                X86Asm.MovAbsFromReg(Reg32.Esi, _ammoClipCell),
                new byte[] { 0x83, 0xBE, 0xAC, 0x04, 0x00, 0x00, 0x00 }));

        // --- Constant-value cheats ---

        // server.dll+E71CA: mov [esi],edi / pop edi / mov eax,esi  (5 bytes)
        _hooks["InfAmmoPrimary"] = CodeCave.Prepare(
            _pm, "InfAmmoPrimary",
            hookAddress: Server(0xE71CA),
            hookLength: 5,
            expectedOriginalBytes: new byte[] { 0x89, 0x3E, 0x5F, 0x8B, 0xC6 },
            caveBody: X86Asm.Concat(
                X86Asm.MovRegImm32(Reg32.Edi, 99),
                new byte[] { 0x89, 0x3E, 0x5F, 0x8B, 0xC6 }));

        // server.dll+F094E: call dword ptr [eax+4F8]  (6 bytes)
        _hooks["InfAmmoSecondary"] = CodeCave.Prepare(
            _pm, "InfAmmoSecondary",
            hookAddress: Server(0xF094E),
            hookLength: 6,
            expectedOriginalBytes: new byte[] { 0xFF, 0x90, 0xF8, 0x04, 0x00, 0x00 },
            caveBody: X86Asm.Concat(
                new byte[] { 0xFF, 0x90, 0xF8, 0x04, 0x00, 0x00 },
                X86Asm.MovRegImm32(Reg32.Ebx, 99)));

        // server.dll+EB8AE: call dword ptr [eax+1E4]  (6 bytes)
        _hooks["InfHealth"] = CodeCave.Prepare(
            _pm, "InfHealth",
            hookAddress: Server(0xEB8AE),
            hookLength: 6,
            expectedOriginalBytes: new byte[] { 0xFF, 0x90, 0xE4, 0x01, 0x00, 0x00 },
            caveBody: X86Asm.Concat(
                new byte[] { 0xFF, 0x90, 0xE4, 0x01, 0x00, 0x00 },
                X86Asm.MovRegImm32(Reg32.Edi, 999)));

        // server.dll+1EC1AB: call dword ptr [eax+6F4]  (6 bytes)
        _hooks["InfSuitArmor"] = CodeCave.Prepare(
            _pm, "InfSuitArmor",
            hookAddress: Server(0x1EC1AB),
            hookLength: 6,
            expectedOriginalBytes: new byte[] { 0xFF, 0x90, 0xF4, 0x06, 0x00, 0x00 },
            caveBody: X86Asm.Concat(
                new byte[] { 0xFF, 0x90, 0xF4, 0x06, 0x00, 0x00 },
                X86Asm.MovRegImm32(Reg32.Edi, 999)));
    }

    // --- Pointer capture control (needed before reading/writing any player stat) ---

    public bool PointersActive => _hooks["PlayerStruct"].IsEnabled && _hooks["AmmoClip"].IsEnabled;

    public void ActivatePointers()
    {
        _hooks["PlayerStruct"].Enable();
        _hooks["AmmoClip"].Enable();
    }

    private IntPtr PlayerBase => _pm.ReadPointer(_playerStructCell);
    private IntPtr AmmoClipBase => _pm.ReadPointer(_ammoClipCell);

    public bool HasValidPlayer => PlayerBase != IntPtr.Zero;
    public bool HasValidAmmoClip => AmmoClipBase != IntPtr.Zero;

    // --- Toggleable code-cave cheats ---

    public bool InfiniteAmmoPrimary { get => _hooks["InfAmmoPrimary"].IsEnabled; set => SetHook("InfAmmoPrimary", value); }
    public bool InfiniteAmmoSecondary { get => _hooks["InfAmmoSecondary"].IsEnabled; set => SetHook("InfAmmoSecondary", value); }
    public bool InfiniteHealth { get => _hooks["InfHealth"].IsEnabled; set => SetHook("InfHealth", value); }
    public bool InfiniteSuitArmor { get => _hooks["InfSuitArmor"].IsEnabled; set => SetHook("InfSuitArmor", value); }

    private void SetHook(string name, bool enabled)
    {
        if (enabled) _hooks[name].Enable();
        else _hooks[name].Disable();
    }

    // --- Direct stat reads/writes (require ActivatePointers() + HasValidPlayer) ---

    public int GetHealthCurrent() => _pm.ReadInt32(IntPtr.Add(PlayerBase, PlayerOffsets.HealthCurrent));
    public void SetHealthCurrent(int value) => _pm.WriteInt32(IntPtr.Add(PlayerBase, PlayerOffsets.HealthCurrent), value);

    public int GetHealthMax() => _pm.ReadInt32(IntPtr.Add(PlayerBase, PlayerOffsets.HealthMax));

    public int GetSuitCurrent() => _pm.ReadInt32(IntPtr.Add(PlayerBase, PlayerOffsets.SuitCurrent));
    public void SetSuitCurrent(int value) => _pm.WriteInt32(IntPtr.Add(PlayerBase, PlayerOffsets.SuitCurrent), value);

    public int GetAmmo(int weaponOffset) => _pm.ReadInt32(IntPtr.Add(PlayerBase, weaponOffset));
    public void SetAmmo(int weaponOffset, int value) => _pm.WriteInt32(IntPtr.Add(PlayerBase, weaponOffset), value);

    public int GetAmmoClipCurrent() => _pm.ReadInt32(IntPtr.Add(AmmoClipBase, AmmoClipOffsets.Current));
    public void SetAmmoClipCurrent(int value) => _pm.WriteInt32(IntPtr.Add(AmmoClipBase, AmmoClipOffsets.Current), value);

    public float GetFloat(int playerOffset) => _pm.ReadFloat(IntPtr.Add(PlayerBase, playerOffset));
    public void SetFloat(int playerOffset, float value) => _pm.WriteFloat(IntPtr.Add(PlayerBase, playerOffset), value);

    public void Dispose()
    {
        foreach (var hook in _hooks.Values)
            hook.Disable();

        if (_playerStructCell != IntPtr.Zero) _pm.Free(_playerStructCell);
        if (_ammoClipCell != IntPtr.Zero) _pm.Free(_ammoClipCell);

        _pm.Dispose();
    }
}
