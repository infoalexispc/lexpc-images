namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

/// <summary>
/// Define un destino de publicación: dimensiones exigidas y cómo debe procesarse la imagen.
/// </summary>
public sealed record SlotDefinition
{
    public static readonly SlotDefinition PcHomeSmall = new(
        SlotId.Parse("optimizar-imagen-pc-home-320x315"),
        width: 320,
        height: 315,
        mode: SlotMode.FitTransparent);

    public static readonly SlotDefinition PcHomeWide = new(
        SlotId.Parse("optimizar-imagen-pc-home-992x715"),
        width: 992,
        height: 715,
        mode: SlotMode.FitTransparent);

    public static readonly SlotDefinition PcMainSection = new(
        SlotId.Parse("optimizar-imagen-pc-seccion-principal"),
        width: 1000,
        height: 720,
        mode: SlotMode.ResizeAndPad);

    public static readonly SlotDefinition PcLastSection = new(
        SlotId.Parse("optimizar-imagen-pc-ultima-seccion"),
        width: 619,
        height: 720,
        mode: SlotMode.CoverOrPad,
        coverFit: CoverFitOptions.Defaults);

    public SlotId Id { get; }
    public int Width { get; }
    public int Height { get; }
    public SlotMode Mode { get; }
    public CoverFitOptions? CoverFit { get; }

    public SlotDefinition(
        SlotId id,
        int width,
        int height,
        SlotMode mode = SlotMode.ResizeAndPad,
        CoverFitOptions? coverFit = null)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Slot id cannot be empty.", nameof(id));
        }
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Slot width must be positive.");
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Slot height must be positive.");
        }
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown slot mode.");
        }

        Id = id;
        Width = width;
        Height = height;
        Mode = mode;
        CoverFit = coverFit;
    }

    public CoverFitOptions EffectiveCoverFit => CoverFit ?? CoverFitOptions.Defaults;

    public bool IsSatisfiedBy(int width, int height) => width == Width && height == Height;
}
