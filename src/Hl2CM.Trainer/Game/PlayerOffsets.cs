namespace Hl2CM.Trainer.Game;

/// <summary>
/// Offsets copied straight from the "Player Data Structure" / CheatEntries in the
/// original hl2.CT table, relative to the captured player-struct pointer
/// (equivalent to the table's "[pPlayerStructAddr]" base address).
/// </summary>
public static class PlayerOffsets
{
    public const int HealthMax = 0xDC;
    public const int HealthCurrent = 0xE0;

    public const int SuitCurrent = 0xD30;

    public const int TimerInGame = 0xF8;
    public const int TimerGlobal = 0x90;
    public const int TimerGlobal2 = 0xD4C;

    // Weapon ammo reserves (not the current clip — that comes from the ammo-clip pointer).
    public const int AmmoPistol = 0x6DC;
    public const int AmmoMagnum = 0x6E4;
    public const int AmmoSmg1 = 0x6E0;
    public const int AmmoSmg1Alt = 0x6F4;
    public const int AmmoImpulseRifle = 0x6D4;
    public const int AmmoImpulseRifleAlt = 0x728;
    public const int AmmoShotgun = 0x6EC;
    public const int AmmoCrossbow = 0x6E8;
    public const int AmmoGrenades = 0x700;
    public const int AmmoRpg = 0x6F0;

    // Position / view (floats). "A/B/Vert" is one triplet, "editable" a second live-writable one.
    public const int PositionA = 0x238;
    public const int PositionB = 0x248;
    public const int PositionVert = 0x258;

    public const int PositionAEditable = 0x27C;
    public const int PositionBEditable = 0x280;
    public const int PositionVertEditable = 0x284;

    public const int CameraVert = 0x310;
    public const int CameraHoriz = 0x314;

    public const int Speed1DirA = 0x214;
    public const int Speed1DirB = 0x218;
    public const int Speed1Vert = 0x21C;

    public const int Speed2DirA = 0x288;
    public const int Speed2DirB = 0x28C;
    public const int Speed2Vert = 0x290;
}

/// <summary>Offset of the current ammo-in-clip value relative to the captured ammo-clip pointer.</summary>
public static class AmmoClipOffsets
{
    public const int Current = 0x4AC;
}
