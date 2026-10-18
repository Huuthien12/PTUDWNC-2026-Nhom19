using System.Text;
using CulinaryBlog.Infrastructure.Storage;
using ImageMagick;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class RecipeImageResizerTests
{
    [Fact]
    public void Resize_creates_exact_medium_and_square_thumbnail_jpegs()
    {
        using var source = new MagickImage(MagickColors.Red, 1200, 900);
        var variants = new RecipeImageResizer().Resize(new MemoryStream(source.ToByteArray(MagickFormat.Png)));

        using var medium = new MagickImage(variants.Medium);
        using var thumbnail = new MagickImage(variants.Thumbnail);

        Assert.Equal(800u, medium.Width);
        Assert.Equal(600u, medium.Height);
        Assert.Equal(300u, thumbnail.Width);
        Assert.Equal(300u, thumbnail.Height);
        Assert.Equal(MagickFormat.Jpeg, medium.Format);
        Assert.Equal(MagickFormat.Jpeg, thumbnail.Format);
    }

    [Fact]
    public void Resize_does_not_upscale_small_images()
    {
        var ppm = Encoding.ASCII.GetBytes("P3\n2 1\n255\n255 0 0 0 0 255\n");
        var variants = new RecipeImageResizer().Resize(new MemoryStream(ppm));
        using var medium = new MagickImage(variants.Medium);
        using var thumbnail = new MagickImage(variants.Thumbnail);
        Assert.Equal(2u, medium.Width);
        Assert.Equal(1u, medium.Height);
        Assert.Equal(2u, thumbnail.Width);
        Assert.Equal(1u, thumbnail.Height);
    }

    [Fact]
    public void Resize_rejects_excessive_decoded_dimensions()
    {
        var ppm = Encoding.ASCII.GetBytes("P6\n10000 10000\n255\n");
        Assert.Throws<InvalidDataException>(() => new RecipeImageResizer().Resize(new MemoryStream(ppm)));
    }
}
