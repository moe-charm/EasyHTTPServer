using System.Windows.Media.Imaging;

namespace EasyHttpServer.Desktop.Wpf.Tests;

public sealed class QrCodeRendererTests
{
    [Fact]
    public void RendersHttpsUrlAsFrozenBitmap()
    {
        var image = Assert.IsType<BitmapImage>(new QrCodeRenderer().RenderUrl("https://192.168.1.20:18080/"));

        Assert.True(image.IsFrozen);
        Assert.True(image.PixelWidth > 100);
        Assert.Equal(image.PixelWidth, image.PixelHeight);
    }

    [Theory]
    [InlineData("http://192.168.1.20:18080/")]
    [InlineData("https://192.168.1.20:18080/?code=secret")]
    [InlineData("https://192.168.1.20:18080/#secret")]
    public void RejectsPayloadsThatCouldCarryCredentials(string payload) =>
        Assert.Throws<ArgumentException>(() => new QrCodeRenderer().RenderUrl(payload));
}
