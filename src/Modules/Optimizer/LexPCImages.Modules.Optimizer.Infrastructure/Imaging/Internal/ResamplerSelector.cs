using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

/// <summary>
/// Traduce el <see cref="DownscaleFilter"/> configurado al remuestreador de ImageSharp, con una
/// única regla añadida: el filtro configurable solo manda cuando la imagen se reduce.
/// <para>
/// El promedio de área degenera en vecino más próximo al ampliar —su soporte se encoge con la
/// razón de escala—, así que una imagen más pequeña que el slot saldría a bloques. Ampliar es raro
/// (el catálogo entra con másteres de 2000 px) pero está permitido desde 200 px, y ahí Lanczos3 es
/// la única opción sensata.
/// </para>
/// </summary>
internal static class ResamplerSelector
{
    public static IResampler For(
        DownscaleFilter filter,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
    {
        var reduces = (long)targetWidth * targetHeight < (long)sourceWidth * sourceHeight;

        return (reduces, filter) switch
        {
            (true, DownscaleFilter.Box) => KnownResamplers.Box,
            _ => KnownResamplers.Lanczos3,
        };
    }
}
