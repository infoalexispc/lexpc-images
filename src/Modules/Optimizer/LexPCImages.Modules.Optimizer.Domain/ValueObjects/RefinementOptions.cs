namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

public sealed record RefinementOptions(
    bool SuppressShadow = true,
    bool RemoveDesk = true,
    bool ProtectLegs = true,
    double CropMarginPct = 0.05)
{
    public static readonly RefinementOptions Defaults = new();

    public RefinementOptions With(
        bool? suppressShadow = null,
        bool? removeDesk = null,
        bool? protectLegs = null,
        double? cropMarginPct = null)
    {
        var margin = cropMarginPct ?? CropMarginPct;
        if (margin is < 0 or > 0.5)
        {
            throw new ArgumentOutOfRangeException(nameof(cropMarginPct), margin,
                "CropMarginPct must be in [0, 0.5].");
        }
        return new RefinementOptions(
            suppressShadow ?? SuppressShadow,
            removeDesk ?? RemoveDesk,
            protectLegs ?? ProtectLegs,
            margin);
    }
}
