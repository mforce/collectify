namespace Collectify.Domain.Enums;

[Flags]
public enum MovieFormat
{
    None = 0,
    Dvd = 1,
    BluRay = 2,
    UhdBluRay = 4,
}
