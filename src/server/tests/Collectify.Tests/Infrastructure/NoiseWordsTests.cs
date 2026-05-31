using Collectify.Infrastructure.Lookup;

namespace Collectify.Tests.Infrastructure;

public class NoiseWordsTests
{
    [Fact]
    public void GetNoiseWordsFor_Games_ContainsDefaults()
    {
        var options = new MetadataLookupOptions();
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Games);

        Assert.Contains("nintendo", words);
        Assert.Contains("championship", words);
        Assert.Contains("esrb", words);
    }

    [Fact]
    public void GetNoiseWordsFor_Movies_ContainsDefaults()
    {
        var options = new MetadataLookupOptions();
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Movies);

        Assert.Contains("dvd", words);
        Assert.Contains("paramount", words);
        Assert.Contains("4k", words);
    }

    [Fact]
    public void GetNoiseWordsFor_Music_ContainsDefaults()
    {
        var options = new MetadataLookupOptions();
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Music);

        Assert.Contains("vinyl", words);
        Assert.Contains("lp", words);
        Assert.Contains("remastered", words);
    }

    [Fact]
    public void GetNoiseWordsFor_Games_MergesConfigExtras()
    {
        var options = new MetadataLookupOptions
        {
            NoiseWords = new NoiseWordsOptions { GamesExtraRaw = "retro, arcade, atari" }
        };
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Games);

        Assert.Contains("nintendo", words); // default
        Assert.Contains("retro", words);    // extra
        Assert.Contains("arcade", words);   // extra
        Assert.Contains("atari", words);    // extra
    }

    [Fact]
    public void GetNoiseWordsFor_Movies_MergesConfigExtras()
    {
        var options = new MetadataLookupOptions
        {
            NoiseWords = new NoiseWordsOptions { MoviesExtraRaw = "netflix, hulu" }
        };
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Movies);

        Assert.Contains("dvd", words);     // default
        Assert.Contains("netflix", words); // extra
        Assert.Contains("hulu", words);    // extra
    }

    [Fact]
    public void GetNoiseWordsFor_Music_MergesConfigExtras()
    {
        var options = new MetadataLookupOptions
        {
            NoiseWords = new NoiseWordsOptions { MusicExtraRaw = "bootleg, fan mix" }
        };
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Music);

        Assert.Contains("vinyl", words);   // default
        Assert.Contains("bootleg", words); // extra
    }

    [Fact]
    public void GetNoiseWordsFor_NoConfigExtras_ReturnsDefaultsOnly()
    {
        var options = new MetadataLookupOptions();
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Games);

        Assert.DoesNotContain("retro", words);
    }

    [Fact]
    public void GetNoiseWordsFor_ExtraWordsAreCaseInsensitive()
    {
        var options = new MetadataLookupOptions
        {
            NoiseWords = new NoiseWordsOptions { GamesExtraRaw = "RETRO, Arcade" }
        };
        var words = options.GetNoiseWordsFor(MetadataLookupOptions.Category.Games);

        Assert.Contains("retro", words);
        Assert.Contains("ARCADE", words);
    }

    [Fact]
    public void VisionResultLimit_DefaultIs100()
    {
        var options = new MetadataLookupOptions();
        Assert.Equal(100, options.VisionResultLimit);
    }

    [Fact]
    public void VisionResultLimit_Configurable()
    {
        var options = new MetadataLookupOptions { VisionResultLimit = 50 };
        Assert.Equal(50, options.VisionResultLimit);
    }
}
