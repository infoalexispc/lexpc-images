using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LexPCImages.Modules.Optimizer.Presentation.Requests;

/// <summary>
/// Cuerpo <c>multipart/form-data</c> de la petición de encolado. Agrupar los campos en un tipo
/// evita una firma de acción con seis parámetros sueltos y documenta el contrato en OpenAPI.
/// </summary>
public sealed class EnqueueJobForm
{
    [FromForm(Name = "slotId")]
    public string? SlotId { get; init; }

    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }

    [FromForm(Name = "shadowSuppression")]
    public bool? ShadowSuppression { get; init; }

    [FromForm(Name = "deskRemoval")]
    public bool? DeskRemoval { get; init; }

    [FromForm(Name = "legProtection")]
    public bool? LegProtection { get; init; }

    [FromForm(Name = "cropMarginPct")]
    public double? CropMarginPct { get; init; }

    public RefinementOverrides ToRefinementOverrides() => new(
        SuppressShadow: ShadowSuppression,
        RemoveDesk: DeskRemoval,
        ProtectLegs: LegProtection,
        CropMarginPct: CropMarginPct);
}
