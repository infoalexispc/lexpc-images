namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

/// <summary>
/// Agrupa varias salidas bajo un único id público: una imagen entra y se publica en todos los
/// slots del paquete. No sustituye al invariante "un trabajo produce una imagen": lo que hace es
/// que una subida cree un trabajo por salida.
/// </summary>
public sealed record SlotBundle
{
    /// <summary>La imagen principal del PC: la misma foto en el tamaño de la home y en el ancho.</summary>
    public static readonly SlotBundle PcHome = new(
        SlotId.Parse("optimizar-imagen-pc-home"),
        [SlotDefinition.PcHomeSmall, SlotDefinition.PcHomeWide]);

    public SlotId Id { get; }
    public IReadOnlyList<SlotDefinition> Outputs { get; }

    public SlotBundle(SlotId id, IReadOnlyList<SlotDefinition> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Bundle id cannot be empty.", nameof(id));
        }
        if (outputs.Count == 0)
        {
            throw new ArgumentException("A bundle must declare at least one output.", nameof(outputs));
        }
        if (outputs.Select(output => output.Id).Distinct().Count() != outputs.Count)
        {
            throw new ArgumentException("A bundle cannot repeat an output slot.", nameof(outputs));
        }

        Id = id;
        Outputs = outputs;
    }
}
