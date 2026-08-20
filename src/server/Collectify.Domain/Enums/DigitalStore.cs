namespace Collectify.Domain.Enums;

/// <summary>
/// Digital storefront(s) a digital copy is owned on. A <c>[Flags]</c> enum so
/// one game can be owned on multiple stores (e.g. Steam + Epic). Stored as a
/// single int bitmask on <see cref="Collectify.Domain.Entities.Game.DigitalStores"/>,
/// mirroring <see cref="MovieFormat"/>. Values are powers of two; do not
/// renumber (persisted as ints, pinned by <c>EnumParityTests</c>).
/// </summary>
[Flags]
public enum DigitalStore
{
    None = 0,
    Steam = 1,
    Gog = 2,
    Epic = 4,
    Xbox = 8,
    Psn = 16,
    Nintendo = 32,
    Other = 64,
}
