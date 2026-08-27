namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

public readonly record struct SlotId
{
    public string Value { get; }

    private SlotId(string value) => Value = value;

    public static SlotId Parse(string value) => new(value);

    public override string ToString() => Value;
}
