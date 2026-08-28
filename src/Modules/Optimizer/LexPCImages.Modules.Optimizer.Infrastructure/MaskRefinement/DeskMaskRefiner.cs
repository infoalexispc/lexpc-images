using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

namespace LexPCImages.Modules.Optimizer.Infrastructure.MaskRefinement;

/// <summary>
/// Elimina de la máscara los componentes conexos que se comportan como una mesa: muy anchos
/// respecto a su alto, o apoyados en el borde inferior ocupando casi todo el ancho.
/// </summary>
public sealed class DeskMaskRefiner : IDeskMaskRefiner
{
    private const float BinaryThreshold = 160f / 255f;
    private const int CloseRadius = 3;
    private const double LargeAreaRatio = 0.65;
    private const double WideAspectRatio = 3.0;
    private const double BottomWidthRatio = 0.70;
    private const float ProtectedAlphaThreshold = 0.5f;

    private static readonly (int Dx, int Dy)[] Neighbors =
    [
        (-1, 0), (1, 0), (0, -1), (0, 1),
    ];

    public MaskResult RemoveDesk(MaskResult mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var binary = Morphology.Binarize(mask.Values, BinaryThreshold);
        var closed = Morphology.Close(binary, mask.Width, mask.Height, CloseRadius);
        var protectedPixels = Morphology.Binarize(mask.Values, ProtectedAlphaThreshold);
        var labels = ConnectedComponents(closed, mask.Width, mask.Height);
        var blobs = ComputeBlobStats(labels, mask.Width, mask.Height);
        var deskLabels = IdentifyDeskBlobs(blobs, mask.Width, mask.Height);

        var output = new float[mask.Values.Length];
        for (var i = 0; i < mask.Values.Length; i++)
        {
            if (protectedPixels[i])
            {
                output[i] = 1f;
                continue;
            }
            output[i] = !deskLabels.Contains(labels[i]) && closed[i] ? 1f : 0f;
        }
        return new MaskResult(mask.Width, mask.Height, output);
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
                var index = (y * width) + x;
                if (!values[index] || labels[index] != 0)
                {
                    continue;
                }

                labels[index] = nextLabel;
                stack.Push((x, y));
                while (stack.TryPop(out var point))
                {
                    foreach (var (dx, dy) in Neighbors)
                    {
                        var nx = point.X + dx;
                        var ny = point.Y + dy;
                        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                        {
                            continue;
                        }
                        var neighborIndex = (ny * width) + nx;
                        if (values[neighborIndex] && labels[neighborIndex] == 0)
                        {
                            labels[neighborIndex] = nextLabel;
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
        var byLabel = new Dictionary<int, BlobStats>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var label = labels[(y * width) + x];
                if (label == 0)
                {
                    continue;
                }
                byLabel[label] = byLabel.TryGetValue(label, out var existing)
                    ? existing with
                    {
                        MinX = Math.Min(existing.MinX, x),
                        MaxX = Math.Max(existing.MaxX, x),
                        MinY = Math.Min(existing.MinY, y),
                        MaxY = Math.Max(existing.MaxY, y),
                        Area = existing.Area + 1,
                    }
                    : new BlobStats(label, x, x, y, y, 1);
            }
        }
        return byLabel.Values.ToList();
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
            minX = Math.Min(minX, blob.MinX);
            maxX = Math.Max(maxX, blob.MaxX);
            minY = Math.Min(minY, blob.MinY);
            maxY = Math.Max(maxY, blob.MaxY);
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

            var isWideSlab = areaRatio > LargeAreaRatio && aspect > WideAspectRatio;
            var sitsOnBottomEdge = blob.MaxY == height - 1 && (double)blobWidth / width > BottomWidthRatio;
            if (isWideSlab || sitsOnBottomEdge)
            {
                deskLabels.Add(blob.Label);
            }
        }
        return deskLabels;
    }
}
