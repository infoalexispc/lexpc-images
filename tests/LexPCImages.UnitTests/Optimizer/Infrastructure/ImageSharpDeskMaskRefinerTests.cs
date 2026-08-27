using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ImageSharpDeskMaskRefinerTests
{
    [Fact]
    public void RemoveDesk_discards_horizontal_wide_blob_at_bottom()
    {
        var mask = MakeWeakRect(80, 40, 0, 35, 79, 39);

        var refiner = new ImageSharpDeskMaskRefiner();
        var result = refiner.RemoveDesk(mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public void RemoveDesk_keeps_tall_vertical_blobs()
    {
        var mask = new MaskResult(40, 40, BuildValues(40, 40, (values, w, h) =>
        {
            FillRect(values, w, h, 10, 5, 12, 34, 0.9f);
            FillRect(values, w, h, 27, 5, 29, 34, 0.9f);
        }));

        var refiner = new ImageSharpDeskMaskRefiner();
        var result = refiner.RemoveDesk(mask);

        for (var y = 5; y <= 34; y++)
        {
            for (var x = 10; x <= 12; x++)
            {
                result.Values[y * 40 + x].Should().Be(1f, $"pixel ({x},{y}) of leg 1");
            }
            for (var x = 27; x <= 29; x++)
            {
                result.Values[y * 40 + x].Should().Be(1f, $"pixel ({x},{y}) of leg 2");
            }
        }
    }

    [Fact]
    public void RemoveDesk_keeps_pc_when_no_desk_present()
    {
        var mask = MakeSolidRect(40, 40, 15, 10, 24, 29);

        var refiner = new ImageSharpDeskMaskRefiner();
        var result = refiner.RemoveDesk(mask);

        for (var y = 10; y <= 29; y++)
        {
            for (var x = 15; x <= 24; x++)
            {
                result.Values[y * 40 + x].Should().Be(1f);
            }
        }
    }

    [Fact]
    public void RemoveDesk_returns_empty_when_input_is_empty()
    {
        var mask = new MaskResult(40, 40, new float[40 * 40]);

        var refiner = new ImageSharpDeskMaskRefiner();
        var result = refiner.RemoveDesk(mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public void RemoveDesk_binarizes_input_using_threshold()
    {
        var mask = new MaskResult(40, 40, BuildValues(40, 40, (values, w, h) =>
        {
            FillRect(values, w, h, 15, 10, 24, 29, 0.4f);
            FillRect(values, w, h, 0, 35, 39, 39, 0.4f);
        }));

        var refiner = new ImageSharpDeskMaskRefiner();
        var result = refiner.RemoveDesk(mask);

        for (var y = 10; y <= 29; y++)
        {
            for (var x = 15; x <= 24; x++)
            {
                result.Values[y * 40 + x].Should().Be(0f, $"pixel ({x},{y}) below threshold");
            }
        }
    }

    [Fact]
    public void RemoveDesk_discards_desk_below_threshold_when_touching_bottom()
    {
        var mask = new MaskResult(80, 40, BuildValues(80, 40, (values, w, h) =>
        {
            FillRect(values, w, h, 0, 39, 79, 39, 0.4f);
        }));

        var refiner = new ImageSharpDeskMaskRefiner();
        var result = refiner.RemoveDesk(mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    private static MaskResult MakeSolidRect(int width, int height, int minX, int minY, int maxX, int maxY)
    {
        var values = new float[width * height];
        FillRect(values, width, height, minX, minY, maxX, maxY, 1f);
        return new MaskResult(width, height, values);
    }

    private static MaskResult MakeWeakRect(int width, int height, int minX, int minY, int maxX, int maxY)
    {
        var values = new float[width * height];
        FillRect(values, width, height, minX, minY, maxX, maxY, 0.4f);
        return new MaskResult(width, height, values);
    }

    private static float[] BuildValues(int width, int height, Action<float[], int, int> fill)
    {
        var values = new float[width * height];
        fill(values, width, height);
        return values;
    }

    private static void FillRect(float[] values, int width, int height, int minX, int minY, int maxX, int maxY, float value)
    {
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                values[y * width + x] = value;
            }
        }
    }
}
