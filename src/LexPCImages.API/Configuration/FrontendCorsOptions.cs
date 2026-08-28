using System.ComponentModel.DataAnnotations;

namespace LexPCImages.API.Configuration;

/// <summary>
/// Orígenes permitidos para el frontend, enlazados desde la sección <c>Cors</c>. Antes estaban
/// escritos en <c>Program.cs</c>, lo que obligaba a recompilar para desplegar en otro entorno.
/// </summary>
public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "LexPCImagesFrontend";

    [MinLength(1, ErrorMessage = "At least one allowed origin must be configured under Cors:AllowedOrigins.")]
    public string[] AllowedOrigins { get; set; } = [];
}
