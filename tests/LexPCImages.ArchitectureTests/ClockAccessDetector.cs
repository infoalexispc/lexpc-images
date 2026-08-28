using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace LexPCImages.ArchitectureTests;

/// <summary>
/// Detecta el uso del reloj ambiental (<c>DateTime.UtcNow</c>, <c>DateTimeOffset.UtcNow</c>,
/// <c>DateTime.Now</c>, <c>DateTimeOffset.Now</c>) leyendo el IL del ensamblado.
/// </summary>
internal static class ClockAccessDetector
{
    private static readonly string[] ForbiddenMembers =
    [
        "get_UtcNow",
        "get_Now",
    ];

    private static readonly string[] ClockTypes =
    [
        "DateTime",
        "DateTimeOffset",
    ];

    public static bool ReadsAmbientClock(Type type)
    {
        var location = type.Assembly.Location;
        if (string.IsNullOrEmpty(location) || !File.Exists(location))
        {
            return false;
        }

        using var stream = File.OpenRead(location);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();

        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            var name = metadata.GetString(member.Name);
            if (!ForbiddenMembers.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            if (member.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var parent = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (ClockTypes.Contains(metadata.GetString(parent.Name), StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
