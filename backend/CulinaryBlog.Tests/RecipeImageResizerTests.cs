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
        var ppm = Encoding.ASCII.GetBytes("P3\n2 1\n255\n255 0 0 0 0 255\n");
        var variants = new RecipeImageResizer().Resize(new MemoryStream(ppm));

        using var medium = new MagickImage(variants.Medium);
        using var thumbnail = new MagickImage(variants.Thumbnail);

        Assert.Equal(800u, medium.Width);
        Assert.Equal(600u, medium.Height);
        Assert.Equal(300u, thumbnail.Width);
        Assert.Equal(300u, thumbnail.Height);
        Assert.Equal(MagickFormat.Jpeg, medium.Format);
        Assert.Equal(MagickFormat.Jpeg, thumbnail.Format);
    }
}
