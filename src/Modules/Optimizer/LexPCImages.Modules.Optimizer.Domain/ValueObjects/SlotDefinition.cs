namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

/// <summary>
/// Define un destino de publicación: dimensiones exigidas y cómo debe procesarse la imagen.
/// </summary>
public sealed record SlotDefinition
{
    public static readonly SlotDefinition PcHome = new(
        SlotId.Parse("optimizar-imagen-pc-home"),
        width: 320,
        height: 315,
        refinement: RefinementOptions.Defaults,
        mode: SlotMode.BackgroundRemoval);

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
    public RefinementOptions? Refinement { get; }
    public SlotMode Mode { get; }
    public CoverFitOptions? CoverFit { get; }

    public SlotDefinition(
        SlotId id,
        int width,
        int height,
        RefinementOptions? refinement = null,
        SlotMode mode = SlotMode.BackgroundRemoval,
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
        Refinement = refinement;
        Mode = mode;
        CoverFit = coverFit;
    }

    public RefinementOptions EffectiveRefinement => Refinement ?? RefinementOptions.Defaults;

    public CoverFitOptions EffectiveCoverFit => CoverFit ?? CoverFitOptions.Defaults;

    public bool IsSatisfiedBy(int width, int height) => width == Width && height == Height;
}
