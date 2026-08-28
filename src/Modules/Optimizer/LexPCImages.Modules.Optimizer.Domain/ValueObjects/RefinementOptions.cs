using System.Diagnostics.CodeAnalysis;

namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

/// <summary>
/// Ajustes de refinado de la máscara. Value object inmutable y siempre válido:
/// no existe una instancia con <see cref="CropMarginPct"/> fuera de rango.
/// </summary>
public sealed record RefinementOptions
{
    public const double MinCropMarginPct = 0.0;
    public const double MaxCropMarginPct = 0.5;
    public const double DefaultCropMarginPct = 0.05;

    public static readonly RefinementOptions Defaults = new();

    public bool SuppressShadow { get; }
    public bool RemoveDesk { get; }
    public bool ProtectLegs { get; }
    public double CropMarginPct { get; }

    public RefinementOptions(
        bool suppressShadow = true,
        bool removeDesk = true,
        bool protectLegs = true,
        double cropMarginPct = DefaultCropMarginPct)
    {
        if (!IsValidCropMargin(cropMarginPct))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cropMarginPct),
                cropMarginPct,
                $"CropMarginPct must be in [{MinCropMarginPct}, {MaxCropMarginPct}].");
        }

        SuppressShadow = suppressShadow;
        RemoveDesk = removeDesk;
        ProtectLegs = protectLegs;
        CropMarginPct = cropMarginPct;
    }

    public static bool IsValidCropMargin(double value) =>
        !double.IsNaN(value)
        && !double.IsInfinity(value)
        && value is >= MinCropMarginPct and <= MaxCropMarginPct;

    /// <summary>Construcción sin excepciones, para validar entrada externa sin usar try/catch como flujo de control.</summary>
    public static bool TryCreate(
        bool suppressShadow,
        bool removeDesk,
        bool protectLegs,
        double cropMarginPct,
        [NotNullWhen(true)] out RefinementOptions? options)
    {
        if (!IsValidCropMargin(cropMarginPct))
        {
            options = null;
            return false;
        }

        options = new RefinementOptions(suppressShadow, removeDesk, protectLegs, cropMarginPct);
        return true;
    }

    public RefinementOptions With(
        bool? suppressShadow = null,
        bool? removeDesk = null,
        bool? protectLegs = null,
        double? cropMarginPct = null) =>
        new(
            suppressShadow ?? SuppressShadow,
            removeDesk ?? RemoveDesk,
            protectLegs ?? ProtectLegs,
            cropMarginPct ?? CropMarginPct);

    /// <summary>Variante de <see cref="With"/> sin excepciones para aplicar overrides de entrada externa.</summary>
    public bool TryWith(
        bool? suppressShadow,
        bool? removeDesk,
        bool? protectLegs,
        double? cropMarginPct,
        [NotNullWhen(true)] out RefinementOptions? options) =>
        TryCreate(
            suppressShadow ?? SuppressShadow,
            removeDesk ?? RemoveDesk,
            protectLegs ?? ProtectLegs,
            cropMarginPct ?? CropMarginPct,
            out options);
}
