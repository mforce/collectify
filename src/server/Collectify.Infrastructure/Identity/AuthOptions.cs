namespace Collectify.Infrastructure.Identity;

/// <summary>
/// Bound from the <c>Collectify:Auth</c> config section. Single knob
/// today: whether the public registration endpoint and matching UI
/// affordance are exposed. Default is <c>false</c> so single-user
/// installs aren't accidentally opened up.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Collectify:Auth";

    /// <summary>
    /// When false: <c>POST /api/auth/register</c> returns 404 and the
    /// client hides the "Create an account" link. The first-run
    /// <c>/setup</c> flow is unaffected -- it stays the admin
    /// bootstrap.
    /// </summary>
    public bool AllowRegistration { get; set; }
}
