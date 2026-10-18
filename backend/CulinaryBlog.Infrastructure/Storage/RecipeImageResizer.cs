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
        using var info = new MagickImage();
        info.Ping(source);
        // Bound decoded pixels before allocation, including compressed images below the 5 MB limit.
        if (info.Width == 0 || info.Height == 0 || (ulong)info.Width * info.Height > 16_000_000)
            throw new InvalidDataException("Image dimensions exceed the processing limit.");
        source.Position = 0;
        using var image = new MagickImage(source);
        image.AutoOrient();

        return new ResizedRecipeImage(
            ResizeToJpeg(image, MediumWidth, MediumHeight),
            ResizeToJpeg(image, ThumbnailWidth, ThumbnailHeight));
    }

    private static byte[] ResizeToJpeg(MagickImage source, uint width, uint height)
    {
        using var image = (MagickImage)source.Clone();
        var scale = Math.Min(1d, Math.Max((double)width / image.Width, (double)height / image.Height));
        image.Resize((uint)Math.Max(1, Math.Round(image.Width * scale)),
            (uint)Math.Max(1, Math.Round(image.Height * scale)));
        image.Crop(Math.Min(width, image.Width), Math.Min(height, image.Height), Gravity.Center);
        image.ResetPage();
        image.BackgroundColor = MagickColors.White;
        image.Alpha(AlphaAction.Remove);
        image.Format = MagickFormat.Jpeg;
        image.Quality = 85;
        return image.ToByteArray();
    }
}

public sealed record ResizedRecipeImage(byte[] Medium, byte[] Thumbnail);
