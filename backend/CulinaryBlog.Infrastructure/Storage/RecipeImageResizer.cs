using ImageMagick;

namespace CulinaryBlog.Infrastructure.Storage;

public sealed class RecipeImageResizer
{
    public const uint MediumWidth = 800;
    public const uint MediumHeight = 600;
    public const uint ThumbnailWidth = 300;
    public const uint ThumbnailHeight = 300;

    public ResizedRecipeImage Resize(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var image = new MagickImage(source);
        image.AutoOrient();

        return new ResizedRecipeImage(
            ResizeToJpeg(image, MediumWidth, MediumHeight),
            ResizeToJpeg(image, ThumbnailWidth, ThumbnailHeight));
    }

    private static byte[] ResizeToJpeg(MagickImage source, uint width, uint height)
    {
        using var image = (MagickImage)source.Clone();
        image.Resize(new MagickGeometry(width, height) { FillArea = true });
        image.Crop(width, height, Gravity.Center);
        image.ResetPage();
        image.BackgroundColor = MagickColors.White;
        image.Alpha(AlphaAction.Remove);
        image.Format = MagickFormat.Jpeg;
        image.Quality = 85;
        return image.ToByteArray();
    }
}

public sealed record ResizedRecipeImage(byte[] Medium, byte[] Thumbnail);
