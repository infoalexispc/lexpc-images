namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

/// <summary>
/// Operaciones morfológicas sobre máscaras binarias representadas como <c>bool[]</c> en
/// disposición fila a fila. Estaban duplicadas —con matices distintos— en los tres refinadores.
/// </summary>
internal static class Morphology
{
    public static bool[] Binarize(float[] values, float threshold)
    {
        var result = new bool[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = values[i] >= threshold;
        }
        return result;
    }

    public static bool[] Dilate(bool[] values, int width, int height, int radius) =>
        Dilate(values, width, height, radius, radius);

    public static bool[] Dilate(bool[] values, int width, int height, int radiusX, int radiusY) =>
        Apply(values, width, height, radiusX, radiusY, dilate: true);

    public static bool[] Erode(bool[] values, int width, int height, int radius) =>
        Erode(values, width, height, radius, radius);

    public static bool[] Erode(bool[] values, int width, int height, int radiusX, int radiusY) =>
        Apply(values, width, height, radiusX, radiusY, dilate: false);

    /// <summary>Cierre morfológico: dilatación seguida de erosión con el mismo kernel.</summary>
    public static bool[] Close(bool[] values, int width, int height, int radius) =>
        Erode(Dilate(values, width, height, radius), width, height, radius);

    /// <summary>Apertura morfológica: erosión seguida de dilatación con el mismo kernel.</summary>
    public static bool[] Open(bool[] values, int width, int height, int radiusX, int radiusY) =>
        Dilate(Erode(values, width, height, radiusX, radiusY), width, height, radiusX, radiusY);

    /// <summary>Erosión sobre valores continuos: mínimo de la vecindad.</summary>
    public static float[] ErodeGrayscale(float[] values, int width, int height, int radius)
    {
        var result = new float[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var min = float.MaxValue;
                for (var ky = -radius; ky <= radius; ky++)
                {
                    var sy = y + ky;
                    if ((uint)sy >= (uint)height)
                    {
                        continue;
                    }
                    for (var kx = -radius; kx <= radius; kx++)
                    {
                        var sx = x + kx;
                        if ((uint)sx >= (uint)width)
                        {
                            continue;
                        }
                        var value = values[(sy * width) + sx];
                        if (value < min)
                        {
                            min = value;
                        }
                    }
                }
                result[(y * width) + x] = min;
            }
        }
        return result;
    }

    /// <summary>
    /// Dilatación y erosión comparten recorrido y solo difieren en el operador de agregación
    /// y en cómo tratan los píxeles fuera del borde (la erosión los considera vacíos).
    /// </summary>
    private static bool[] Apply(bool[] values, int width, int height, int radiusX, int radiusY, bool dilate)
    {
        var result = new bool[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                result[(y * width) + x] = dilate
                    ? AnyInKernel(values, width, height, x, y, radiusX, radiusY)
                    : AllInKernel(values, width, height, x, y, radiusX, radiusY);
            }
        }
        return result;
    }

    private static bool AnyInKernel(
        bool[] values, int width, int height, int x, int y, int radiusX, int radiusY)
    {
        for (var ky = -radiusY; ky <= radiusY; ky++)
        {
            var sy = y + ky;
            if ((uint)sy >= (uint)height)
            {
                continue;
            }
            for (var kx = -radiusX; kx <= radiusX; kx++)
            {
                var sx = x + kx;
                if ((uint)sx >= (uint)width)
                {
                    continue;
                }
                if (values[(sy * width) + sx])
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool AllInKernel(
        bool[] values, int width, int height, int x, int y, int radiusX, int radiusY)
    {
        for (var ky = -radiusY; ky <= radiusY; ky++)
        {
            var sy = y + ky;
            if ((uint)sy >= (uint)height)
            {
                return false;
            }
            for (var kx = -radiusX; kx <= radiusX; kx++)
            {
                var sx = x + kx;
                if ((uint)sx >= (uint)width)
                {
                    return false;
                }
                if (!values[(sy * width) + sx])
                {
                    return false;
                }
            }
        }
        return true;
    }
}
