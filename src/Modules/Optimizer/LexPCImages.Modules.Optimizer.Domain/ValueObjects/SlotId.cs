namespace LexPCImages.Modules.Optimizer.Domain.ValueObjects;

public readonly record struct SlotId
{
    public string Value { get; }

    private SlotId(string value) => Value = value;

    public static SlotId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Slot id cannot be empty.", nameof(value));
        }

        return new(value.Trim());
    }

    public static bool TryParse(string? value, out SlotId slotId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            slotId = default;
            return false;
        }

        slotId = new(value.Trim());
        return true;
    }

    public override string ToString() => Value;
}
