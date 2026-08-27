namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

public sealed record SlotDefinition(
    SlotId Id,
    int Width,
    int Height,
    RefinementOptions? Refinement = null)
{
    public static readonly SlotDefinition PcHome = new(
        SlotId.Parse("optimizar-imagen-pc-home"),
        Width: 320,
        Height: 315,
        Refinement: RefinementOptions.Defaults);

    public RefinementOptions EffectiveRefinement => Refinement ?? RefinementOptions.Defaults;

    public bool IsSatisfiedBy(int width, int height) =>
        width == Width && height == Height;
}
