using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Infrastructure.Storage;

public sealed class ImageFileValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public ValidatedImage Validate(Stream content, string? declaredContentType)
    {
        if (!content.CanSeek)
            throw new BusinessRuleException("INVALID_IMAGE_FILE", "Image content must be seekable.");
        if (content.Length is 0 or > MaxBytes)
            throw new BusinessRuleException("INVALID_IMAGE_FILE", "Images must be no larger than 5 MB.");

        var position = content.Position;
        content.Position = 0;
        Span<byte> header = stackalloc byte[32];
        var length = content.Read(header);
        content.Position = position;
        var detected = Detect(header[..length]);
        if (detected is null || !string.Equals(detected.ContentType, declaredContentType,
                StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("INVALID_IMAGE_FILE", "Image MIME type or signature is not allowed.");
        return detected with { Length = content.Length };
    }

    private static ValidatedImage? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }))
            return new("image/jpeg", "jpg", 0);
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return new("image/png", "png", 0);
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return new("image/webp", "webp", 0);
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8) &&
            (bytes.Slice(8, 4).SequenceEqual("avif"u8) || bytes.Slice(8, 4).SequenceEqual("avis"u8)))
            return new("image/avif", "avif", 0);
        return null;
    }
}

public sealed record ValidatedImage(string ContentType, string Extension, long Length);
