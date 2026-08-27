using LexPCImages.Modules.Optimizer.Application.Abstractions;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpDeskMaskRefiner : IDeskMaskRefiner
{
    private const float BinaryThreshold = 160f / 255f;
    private const int CloseRadius = 3;
    private const double LargeAreaRatio = 0.65;
    private const double WideAspectRatio = 3.0;
    private const double BottomWidthRatio = 0.70;
    private const float ProtectedAlphaThreshold = 0.5f;

    private static readonly (int Dx, int Dy)[] Neighbors =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1),
    };

    public MaskResult RemoveDesk(MaskResult mask)
    {
        var binary = Binarize(mask.Values);
        var closed = Close(binary, mask.Width, mask.Height);
        var protectedMask = BuildProtectedMask(mask.Values);
        var labels = ConnectedComponents(closed, mask.Width, mask.Height);
        var blobs = ComputeBlobStats(labels, mask.Width, mask.Height);
        var deskLabels = IdentifyDeskBlobs(blobs, mask.Width, mask.Height);

        var output = new float[mask.Values.Length];
        for (var i = 0; i < mask.Values.Length; i++)
        {
            if (protectedMask[i])
            {
                output[i] = 1f;
            }
            else
            {
                output[i] = deskLabels.Contains(labels[i]) ? 0f : (closed[i] ? 1f : 0f);
            }
        }
        return new MaskResult(mask.Width, mask.Height, output);
    }

    private static bool[] BuildProtectedMask(float[] values)
    {
        var protectedMask = new bool[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            protectedMask[i] = values[i] >= ProtectedAlphaThreshold;
        }
        return protectedMask;
    }

    private static bool[] Binarize(float[] values)
    {
        var result = new bool[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = values[i] >= BinaryThreshold;
        }
        return result;
    }

    private static bool[] Close(bool[] values, int width, int height)
    {
        var dilated = Dilate(values, width, height, CloseRadius);
        return Erode(dilated, width, height, CloseRadius);
    }

    private static bool[] Dilate(bool[] values, int width, int height, int radius)
    {
        var result = new bool[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var any = false;
                for (var ky = -radius; ky <= radius && !any; ky++)
                {
                    var sy = y + ky;
                    if ((uint)sy >= (uint)height)
                    {
                        continue;
                    }
                    for (var kx = -radius; kx <= radius && !any; kx++)
                    {
                        var sx = x + kx;
                        if ((uint)sx >= (uint)width)
                        {
                            continue;
                        }
                        if (values[sy * width + sx])
                        {
                            any = true;
                        }
                    }
                }
                result[y * width + x] = any;
            }
        }
        return result;
    }

    private static bool[] Erode(bool[] values, int width, int height, int radius)
    {
        var result = new bool[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var all = true;
                for (var ky = -radius; ky <= radius && all; ky++)
                {
                    var sy = y + ky;
                    if ((uint)sy >= (uint)height)
                    {
                        all = false;
                        continue;
                    }
                    for (var kx = -radius; kx <= radius && all; kx++)
                    {
                        var sx = x + kx;
                        if ((uint)sx >= (uint)width)
                        {
                            all = false;
                            continue;
                        }
                        if (!values[sy * width + sx])
                        {
                            all = false;
                        }
                    }
                }
                result[y * width + x] = all;
            }
        }
        return result;
    }

    private static int[] ConnectedComponents(bool[] values, int width, int height)
    {
        var labels = new int[values.Length];
        var nextLabel = 1;
        var stack = new Stack<(int X, int Y)>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * width + x;
                if (!values[idx] || labels[idx] != 0)
                {
                    continue;
                }

                labels[idx] = nextLabel;
                stack.Push((x, y));
                while (stack.TryPop(out var p))
                {
                    foreach (var (dx, dy) in Neighbors)
                    {
                        var nx = p.X + dx;
                        var ny = p.Y + dy;
                        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                        {
                            continue;
                        }
                        var nidx = ny * width + nx;
                        if (values[nidx] && labels[nidx] == 0)
                        {
                            labels[nidx] = nextLabel;
                            stack.Push((nx, ny));
                        }
                    }
                }
                nextLabel++;
            }
        }
        return labels;
    }

    private sealed record BlobStats(int Label, int MinX, int MaxX, int MinY, int MaxY, int Area);

    private static List<BlobStats> ComputeBlobStats(int[] labels, int width, int height)
    {
        var dict = new Dictionary<int, BlobStats>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var label = labels[y * width + x];
                if (label == 0)
                {
                    continue;
                }
                if (dict.TryGetValue(label, out var existing))
                {
                    dict[label] = existing with
                    {
                        MinX = Math.Min(existing.MinX, x),
                        MaxX = Math.Max(existing.MaxX, x),
                        MinY = Math.Min(existing.MinY, y),
                        MaxY = Math.Max(existing.MaxY, y),
                        Area = existing.Area + 1,
                    };
                }
                else
                {
                    dict[label] = new BlobStats(label, x, x, y, y, 1);
                }
            }
        }
        return dict.Values.ToList();
    }

    private static (int MinX, int MaxX, int MinY, int MaxY) ComputeMaskBbox(List<BlobStats> blobs)
    {
        if (blobs.Count == 0)
        {
            return (0, 0, 0, 0);
        }
        var minX = int.MaxValue;
        var maxX = int.MinValue;
        var minY = int.MaxValue;
        var maxY = int.MinValue;
        foreach (var blob in blobs)
        {
            if (blob.MinX < minX) minX = blob.MinX;
            if (blob.MaxX > maxX) maxX = blob.MaxX;
            if (blob.MinY < minY) minY = blob.MinY;
            if (blob.MaxY > maxY) maxY = blob.MaxY;
        }
        return (minX, maxX, minY, maxY);
    }

    private static HashSet<int> IdentifyDeskBlobs(List<BlobStats> blobs, int width, int height)
    {
        var bbox = ComputeMaskBbox(blobs);
        var maskArea = Math.Max(1, (bbox.MaxX - bbox.MinX + 1) * (bbox.MaxY - bbox.MinY + 1));

        var deskLabels = new HashSet<int>();
        foreach (var blob in blobs)
        {
            var blobWidth = blob.MaxX - blob.MinX + 1;
            var blobHeight = blob.MaxY - blob.MinY + 1;

            var aspect = (double)blobWidth / Math.Max(1, blobHeight);
            var areaRatio = (double)blob.Area / maskArea;

            if (areaRatio > LargeAreaRatio && aspect > WideAspectRatio)
            {
                deskLabels.Add(blob.Label);
                continue;
            }

            if (blob.MaxY == height - 1 && (double)blobWidth / width > BottomWidthRatio)
            {
                deskLabels.Add(blob.Label);
            }
        }
        return deskLabels;
    }
}
