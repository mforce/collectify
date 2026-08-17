using Collectify.Infrastructure.Store;
using Xunit;

namespace Collectify.Tests.Unit.Store;

/// <summary>
/// Unit tests for cover-URL resolution on the Steam import path. Guards the
/// regression where hardcoding <c>steam/apps/{appid}/library_600x900_2x.jpg</c>
/// 404s for newer apps whose art lives under a hashed directory — the URL must
/// come from the GetItems <c>assets</c> metadata instead.
/// </summary>
public class SteamStoreCoverUrlTests
{
    [Fact]
    public void StoreAssetUrl_UsesHashedLibraryAsset_NotAppidPath()
    {
        // A newer app (like Nioh 3) returns a content-hashed asset directory.
        var meta = new SteamStoreBrowseItem
        {
            AppId = 3681010,
            Assets = new SteamStoreAssets
            {
                AssetUrlFormat = "steam/apps/3681010/${FILENAME}?t=1772090941",
                LibraryCapsule2x = "a6c07532fbcfa8c7aeecddc251aa4a6a7156323c/library_capsule_2x.jpg",
            },
        };

        var url = SteamStoreImportService.StoreAssetUrl(meta);

        Assert.NotNull(url);
        // Must use the hashed path AND the store_item_assets host that serves it.
        Assert.Contains("store_item_assets", url);
        Assert.Contains("a6c07532fbcfa8c7aeecddc251aa4a6a7156323c/library_capsule_2x.jpg", url);
        // The old hardcoded appid-based filename 404s for this app — the URL must
        // NOT fall back to it.
        Assert.DoesNotContain("library_600x900_2x.jpg", url);
        Assert.EndsWith(".jpg?t=1772090941", url);
    }

    [Fact]
    public void StoreAssetUrl_PrefersLibraryCapsule2x_OverPlain()
    {
        var meta = new SteamStoreBrowseItem
        {
            Assets = new SteamStoreAssets
            {
                AssetUrlFormat = "steam/apps/9/${FILENAME}?t=1",
                LibraryCapsule2x = "x/library_capsule_2x.jpg",
                LibraryCapsule = "x/library_capsule.jpg",
            },
        };

        var url = SteamStoreImportService.StoreAssetUrl(meta);

        Assert.Contains("library_capsule_2x.jpg", url);
        Assert.DoesNotContain("library_capsule.jpg", url);
    }

    [Fact]
    public void StoreAssetUrl_ReturnsNull_WhenNoAssets()
    {
        Assert.Null(SteamStoreImportService.StoreAssetUrl(new SteamStoreBrowseItem { AppId = 5 }));
        Assert.Null(SteamStoreImportService.StoreAssetUrl(null));
    }
}
