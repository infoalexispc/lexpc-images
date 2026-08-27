namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

public sealed record SlotDefinition(
    SlotId Id,
    int Width,
    int Height,
    RefinementOptions? Refinement = null,
    SlotMode Mode = SlotMode.BackgroundRemoval)
{
    public static readonly SlotDefinition PcHome = new(
        SlotId.Parse("optimizar-imagen-pc-home"),
        Width: 320,
        Height: 315,
        Refinement: RefinementOptions.Defaults);

    public static readonly SlotDefinition PcMainSection = new(
        SlotId.Parse("optimizar-imagen-pc-seccion-principal"),
        Width: 1000,
        Height: 720,
        Mode: SlotMode.ResizeAndPad);

    public RefinementOptions EffectiveRefinement => Refinement ?? RefinementOptions.Defaults;

    public bool IsSatisfiedBy(int width, int height) =>
        width == Width && height == Height;
}
