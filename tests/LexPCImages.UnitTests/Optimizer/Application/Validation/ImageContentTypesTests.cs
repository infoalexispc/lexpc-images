using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Validation;

namespace LexPCImages.UnitTests.Optimizer.Application.Validation;

public sealed class ImageContentTypesTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/PNG")]
    public void IsAllowed_accepts_the_supported_media_types(string contentType)
    {
        ImageContentTypes.IsAllowed(contentType).Should().BeTrue();
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAllowed_rejects_everything_else(string? contentType)
    {
        ImageContentTypes.IsAllowed(contentType).Should().BeFalse();
    }

    [Fact]
    public void DetectContentType_recognises_a_png_signature()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

        ImageContentTypes.DetectContentType(png).Should().Be(ImageContentTypes.Png);
    }

    [Fact]
    public void DetectContentType_recognises_a_jpeg_signature()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0];

        ImageContentTypes.DetectContentType(jpeg).Should().Be(ImageContentTypes.Jpeg);
    }

    [Fact]
    public void DetectContentType_recognises_a_webp_signature()
    {
        byte[] webp = [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

        ImageContentTypes.DetectContentType(webp).Should().Be(ImageContentTypes.Webp);
    }

    [Fact]
    public void DetectContentType_rejects_a_RIFF_container_that_is_not_webp()
    {
        byte[] wave = [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45];

        ImageContentTypes.DetectContentType(wave).Should().BeNull();
    }

    [Theory]
    [InlineData(new byte[] { 0x4D, 0x5A })]                        // ejecutable PE
    [InlineData(new byte[] { 0x89, 0x50, 0x4E })]                  // PNG truncado
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })]            // PDF
    [InlineData(new byte[0])]
    public void HasSupportedSignature_rejects_non_images(byte[] bytes)
    {
        ImageContentTypes.HasSupportedSignature(bytes).Should().BeFalse();
    }
}
