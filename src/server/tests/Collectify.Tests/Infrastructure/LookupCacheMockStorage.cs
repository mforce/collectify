using Collectify.Infrastructure.Lookup;
using Moq;

namespace Collectify.Tests.Infrastructure;

/// <summary>
/// Strict Moq <see cref="ILookupCache"/> storage helper for provider tests.
///
/// Backs every cache operation with an in-memory (provider, key) object
/// dictionary, records every new-shape <c>SetAsync&lt;T&gt;</c> write with its
/// TTL, and fails any write whose TTL does not match the expected configured
/// TTL. This keeps cache-hit tests honest (a value must actually be stored and
/// served, never a loose mock returning default) and verifies the caller
/// forwards the correct TTL on every write.
///
/// Every payload type used by a test must be registered via
/// <see cref="SetupStorage{T}"/>, because the mock is strict.
/// </summary>
public sealed class LookupCacheMockStorage
{
    private readonly Dictionary<(string Provider, string Key), object> _store = new();

    public Mock<ILookupCache> Mock { get; } = new(MockBehavior.Strict);
    public List<(string Provider, string Key, TimeSpan Ttl)> Writes { get; } = new();

    /// <summary>
    /// Registers new-shape (final) Get/Set storage for payload type
    /// <typeparamref name="T"/>, asserting every new write's TTL equals
    /// <paramref name="expectedTtl"/> and storing the value for later Gets.
    /// </summary>
    public void SetupStorage<T>(TimeSpan expectedTtl)
    {
        Mock.Setup(c => c.GetAsync<T>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((provider, key, _) =>
                _store.TryGetValue((provider, key), out var value)
                    ? Task.FromResult((T?)value)
                    : Task.FromResult((T?)default));

        Mock.Setup(c => c.SetAsync<T>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<T>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, T, TimeSpan, CancellationToken>((provider, key, value, ttl, _) =>
            {
                if (ttl != expectedTtl)
                    throw new InvalidOperationException(
                        $"Unexpected TTL {ttl} for provider {provider} key {key}; expected {expectedTtl}.");
                Writes.Add((provider, key, ttl));
                _store[(provider, key)] = value!;
            })
            .Returns(Task.CompletedTask);
    }
}
