using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LexPCImages.Modules.Optimizer.Presentation.Requests;

/// <summary>
/// Cuerpo <c>multipart/form-data</c> de la petición de encolado. Agrupar los campos en un tipo
/// documenta el contrato en OpenAPI en vez de dejar parámetros sueltos en la firma de la acción.
/// </summary>
public sealed class EnqueueJobForm
{
    [FromForm(Name = "slotId")]
    public string? SlotId { get; init; }

    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}
