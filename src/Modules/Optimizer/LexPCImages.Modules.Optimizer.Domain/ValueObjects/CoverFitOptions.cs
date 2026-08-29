using System.Diagnostics.CodeAnalysis;

namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

/// <summary>
/// Regla que decide si una imagen se adapta al slot recortando (cover) o rellenando (pad).
/// Value object inmutable y siempre válido: no existe una instancia con
/// <see cref="MinCoverage"/> fuera de rango.
/// </summary>
public sealed record CoverFitOptions
{
    public const double MinAllowedCoverage = 0.0;
    public const double MaxAllowedCoverage = 1.0;

    /// <summary>
    /// Fracción mínima del área original que debe sobrevivir al recorte para que compense
    /// recortar. Con 0.85 solo se recorta cuando la proporción de origen es muy parecida a la
    /// del slot; el resto se rellena, que es la opción que menos mutila la imagen.
    /// </summary>
    public const double DefaultMinCoverage = 0.85;

    public static readonly CoverFitOptions Defaults = new();

    public double MinCoverage { get; }

    public CoverFitOptions(double minCoverage = DefaultMinCoverage)
    {
        if (!IsValidMinCoverage(minCoverage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minCoverage),
                minCoverage,
                $"MinCoverage must be in ({MinAllowedCoverage}, {MaxAllowedCoverage}].");
        }

        MinCoverage = minCoverage;
    }

    public static bool IsValidMinCoverage(double value) =>
        !double.IsNaN(value)
        && !double.IsInfinity(value)
        && value is > MinAllowedCoverage and <= MaxAllowedCoverage;

    /// <summary>Construcción sin excepciones, para validar entrada externa sin usar try/catch como flujo de control.</summary>
    public static bool TryCreate(double minCoverage, [NotNullWhen(true)] out CoverFitOptions? options)
    {
        if (!IsValidMinCoverage(minCoverage))
        {
            options = null;
            return false;
        }

        options = new CoverFitOptions(minCoverage);
        return true;
    }

    /// <summary>
    /// Fracción del área de origen que sobrevive a un recorte centrado hasta la proporción del
    /// slot. Al cubrir el destino, una de las dos dimensiones se aprovecha entera y la otra se
    /// recorta en la misma proporción en que difieren los aspect ratios, así que la cobertura es
    /// el cociente entre el menor y el mayor de los dos.
    /// </summary>
    public static double CoverageOf(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            return 0.0;
        }

        var sourceAspect = (double)sourceWidth / sourceHeight;
        var targetAspect = (double)targetWidth / targetHeight;

        return Math.Min(sourceAspect, targetAspect) / Math.Max(sourceAspect, targetAspect);
    }

    /// <summary>
    /// <see langword="true"/> si conviene recortar. Ante dimensiones inválidas devuelve
    /// <see langword="false"/>: el relleno nunca pierde parte de la imagen.
    /// </summary>
    public bool ShouldCrop(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight) =>
        CoverageOf(sourceWidth, sourceHeight, targetWidth, targetHeight) >= MinCoverage;
}
