namespace Collectify.Infrastructure.Store;

/// <summary>Steam OpenID assertion verifier (testable seam).</summary>
public interface ISteamOpenIdVerifier
{
    bool IsConfigured { get; }

    /// <summary>Returns the verified SteamID64, or null if any check fails.</summary>
    Task<string?> VerifyAsync(
        IReadOnlyDictionary<string, string> openIdParams,
        string expectedReturnTo,
        CancellationToken ct = default);
}
