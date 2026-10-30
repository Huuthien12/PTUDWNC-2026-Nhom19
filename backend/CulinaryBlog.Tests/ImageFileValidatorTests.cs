using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Infrastructure.Storage;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class ImageFileValidatorTests
{
    [Theory]
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }, "jpg")]
    [InlineData("image/png", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, "png")]
    [InlineData("image/webp", new byte[] { 82, 73, 70, 70, 0, 0, 0, 0, 87, 69, 66, 80 }, "webp")]
    [InlineData("image/avif", new byte[] { 0, 0, 0, 0, 102, 116, 121, 112, 97, 118, 105, 102 }, "avif")]
    public void Accepts_allowed_matching_signatures(string contentType, byte[] bytes, string extension)
    {
        var image = new ImageFileValidator().Validate(new MemoryStream(bytes), contentType);
        Assert.Equal(extension, image.Extension);
    }

    [Fact]
    public void Rejects_mismatched_mime_and_signature() => Assert.Throws<BusinessRuleException>(() =>
        new ImageFileValidator().Validate(new MemoryStream([0xFF, 0xD8, 0xFF]), "image/png"));
}
