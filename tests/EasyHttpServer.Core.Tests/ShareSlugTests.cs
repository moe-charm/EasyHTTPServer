using EasyHttpServer.Core;

namespace EasyHttpServer.Core.Tests;

public sealed class ShareSlugTests
{
    [Theory]
    [InlineData("share")]
    [InlineData("my-share-2")]
    [InlineData("a")]
    public void IsValidAcceptsCanonicalSlug(string value) =>
        Assert.True(ShareSlug.IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("Upper")]
    [InlineData("-share")]
    [InlineData("share-")]
    [InlineData("two words")]
    [InlineData("../../outside")]
    public void IsValidRejectsUnsafeOrNonCanonicalSlug(string value) =>
        Assert.False(ShareSlug.IsValid(value));

    [Fact]
    public void FromDisplayNameReturnsUsableCanonicalSlug() =>
        Assert.Equal("my-share", ShareSlug.FromDisplayName(" My Share "));

    [Fact]
    public void FromDisplayNameReturnsStableSlugForJapaneseName()
    {
        var first = ShareSlug.FromDisplayName("資料");
        var second = ShareSlug.FromDisplayName(" 資料 ");

        Assert.Equal(first, second);
        Assert.Matches("^share-[0-9a-f]{8}$", first);
        Assert.True(ShareSlug.IsValid(first));
    }

    [Fact]
    public void FromDisplayNameDistinguishesDifferentJapaneseNames() =>
        Assert.NotEqual(ShareSlug.FromDisplayName("資料"), ShareSlug.FromDisplayName("写真"));
}
