namespace Collectify.Domain.Enums;

/// <summary>
/// Curated list of game platforms (one per entry). Free-text platform
/// strings are migrated into this enum on a best-effort basis at the EF
/// migration step; anything we can't recognise lands on <see cref="Other"/>
/// and the original free-text is preserved in <c>Game.PlatformLegacy</c>
/// so users can fix unmapped entries by hand.
///
/// Add new platforms at the end -- the integer values are persisted and
/// reordering would silently mis-tag existing rows.
/// </summary>
public enum GamePlatform
{
    Other = 0,

    // Pc is the Windows-PC / generic desktop catch-all (per IGDB's "PC
    // (Microsoft Windows)" id 6). Linux (3) was folded into Pc (#102): a
    // Linux/Steam-Deck title is the same desktop library, and the "how you
    // play" dimension is IsDigital + DigitalStore, not the platform enum.
    // 3 is reserved — DO NOT REUSE. Mac (2) stays its own platform.
    Pc = 1,
    Mac = 2,
    Mobile = 4,

    // Xbox
    XboxOriginal = 10,
    Xbox360 = 11,
    XboxOne = 12,
    XboxSeriesXS = 13,

    // PlayStation
    Ps1 = 20,
    Ps2 = 21,
    Ps3 = 22,
    Ps4 = 23,
    Ps5 = 24,
    Psp = 25,
    PsVita = 26,

    // Nintendo home consoles
    Nes = 30,
    Snes = 31,
    N64 = 32,
    GameCube = 33,
    Wii = 34,
    WiiU = 35,
    Switch = 36,
    Switch2 = 37,

    // Nintendo handhelds
    GameBoy = 40,
    GameBoyColor = 41,
    GameBoyAdvance = 42,
    NintendoDs = 43,
    Nintendo3Ds = 44,

    // Sega
    SegaGenesis = 50,
    SegaSaturn = 51,
    SegaDreamcast = 52,

    // 60 = retired (was SteamDeck, #103). DO NOT REUSE. GamePlatformBackfill
    // rewrites any row still holding 60 to Pc on every startup; a new member
    // assigned 60 would be silently clobbered back to Pc forever. The retired
    // set is pinned in EnumParityTests.ReservedValues.
}
