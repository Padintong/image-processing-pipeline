using ImagePipeline.Core;
using ImagePipeline.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImagePipeline.Tests.Processing;

// Integration-style tests: real ImageSharp, no mocking. Each test creates a
// programmatic PNG source image and asserts on the output stream dimensions.
public sealed class ImageSharpProcessorTests
{
    private readonly ImageSharpProcessor _sut = new();

    // ─── Resize: correct output dimensions ───────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Resize_ScalesImageToFitWithinBox_WhenAspectRatioIsExactFit()
    {
        // 800×600 → 400×300 box: same 4:3 ratio, should land exactly on target.
        using var input = CreateTestPng(width: 800, height: 600);
        var op = new ResizeOperation(new ResizeParams(400, 300));

        using var output = await _sut.ExecuteAsync(input, op);

        using var result = await Image.LoadAsync(output);
        Assert.Equal(400, result.Width);
        Assert.Equal(300, result.Height);
    }

    [Fact]
    public async Task ExecuteAsync_Resize_ConstrainsToWidth_WhenWidthIsBindingConstraint()
    {
        // 800×400 (2:1) → 400×400 box: width-limited → 400×200.
        using var input = CreateTestPng(width: 800, height: 400);
        var op = new ResizeOperation(new ResizeParams(400, 400));

        using var output = await _sut.ExecuteAsync(input, op);

        using var result = await Image.LoadAsync(output);
        Assert.Equal(400, result.Width);
        Assert.Equal(200, result.Height);
    }

    [Fact]
    public async Task ExecuteAsync_Resize_ConstrainsToHeight_WhenHeightIsBindingConstraint()
    {
        // 400×800 (1:2) → 400×400 box: height-limited → 200×400.
        using var input = CreateTestPng(width: 400, height: 800);
        var op = new ResizeOperation(new ResizeParams(400, 400));

        using var output = await _sut.ExecuteAsync(input, op);

        using var result = await Image.LoadAsync(output);
        Assert.Equal(200, result.Width);
        Assert.Equal(400, result.Height);
    }

    // ─── Resize: 10,000 px-per-side ceiling (ADR-010) ────────────────────────

    [Fact]
    public async Task ExecuteAsync_Resize_ThrowsArgumentOutOfRangeException_WhenWidthExceedsCeiling()
    {
        using var input = CreateTestPng(width: 100, height: 100);
        var op = new ResizeOperation(new ResizeParams(10_001, 100));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.ExecuteAsync(input, op));
    }

    [Fact]
    public async Task ExecuteAsync_Resize_ThrowsArgumentOutOfRangeException_WhenHeightExceedsCeiling()
    {
        using var input = CreateTestPng(width: 100, height: 100);
        var op = new ResizeOperation(new ResizeParams(100, 10_001));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _sut.ExecuteAsync(input, op));
    }

    [Fact]
    public async Task ExecuteAsync_Resize_SucceedsAtExactCeiling()
    {
        // 10,000 is the allowed maximum — should not throw.
        using var input = CreateTestPng(width: 100, height: 100);
        var op = new ResizeOperation(new ResizeParams(10_000, 10_000));

        var output = await _sut.ExecuteAsync(input, op);
        output.Dispose();
    }

    // ─── Unsupported operations ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ThrowsNotImplementedException_ForCropOperation()
    {
        using var input = CreateTestPng(width: 100, height: 100);
        var op = new CropOperation(new CropParams(0, 0, 50, 50));

        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.ExecuteAsync(input, op));
    }

    // ─── Output stream contract ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Resize_ReturnsStreamPositionedAtZero()
    {
        using var input = CreateTestPng(width: 100, height: 100);
        var op = new ResizeOperation(new ResizeParams(50, 50));

        var output = await _sut.ExecuteAsync(input, op);

        Assert.Equal(0, output.Position);
        output.Dispose();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Stream CreateTestPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;
        return ms;
    }
}
