using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobDownload;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Application.Errors;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Modules.Optimizer.Presentation.Requests;
using LexPCImages.Modules.Optimizer.Presentation.Responses;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;
using LexPCImages.Shared.Web.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LexPCImages.Modules.Optimizer.Presentation.Controllers;

/// <summary>
/// Adaptador HTTP del módulo. No contiene reglas de negocio: transporta la petición al caso de
/// uso y traduce el <see cref="Result{T}"/> a una respuesta.
/// </summary>
[ApiController]
[Route("api/optimizer/jobs")]
[AllowAnonymous]
public sealed class OptimizerController : ControllerBase
{
    private readonly EnqueueJobHandler _enqueue;
    private readonly GetJobStatusHandler _getStatus;
    private readonly GetJobDownloadHandler _getDownload;

    public OptimizerController(
        EnqueueJobHandler enqueue,
        GetJobStatusHandler getStatus,
        GetJobDownloadHandler getDownload)
    {
        _enqueue = enqueue;
        _getStatus = getStatus;
        _getDownload = getDownload;
    }

    [HttpPost]
    [RequestSizeLimit(ProcessJob.MaxInputBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProcessJob.MaxInputBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EnqueueJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Enqueue(
        [FromForm] EnqueueJobForm form,
        CancellationToken cancellationToken)
    {
        if (!SlotId.TryParse(form.SlotId, out var slotId))
        {
            return Problem(OptimizerErrors.SlotIdRequired);
        }
        if (form.File is not { Length: > 0 } file)
        {
            return Problem(OptimizerErrors.FileRequired);
        }

        var command = new EnqueueJobCommand(
            slotId,
            await ReadAllBytesAsync(file, cancellationToken),
            file.ContentType);

        var result = await _enqueue.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorOrThrow());
        }

        // Sin cabecera Location: un id puede producir varias salidas y apuntar a una de ellas
        // seria arbitrario. Los ids de todos los trabajos viajan en el cuerpo.
        return Accepted(EnqueueJobResponse.From(result.ValueOrThrow()));
    }

    [HttpGet("{id:guid}", Name = nameof(GetStatus))]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getStatus.HandleAsync(new GetJobStatusQuery(id), cancellationToken);
        return result.IsFailure
            ? Problem(result.ErrorOrThrow())
            : Ok(JobStatusResponse.From(result.ValueOrThrow()));
    }

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getDownload.HandleAsync(new GetJobDownloadQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorOrThrow());
        }

        var download = result.ValueOrThrow();
        return File(download.Content, download.ContentType, download.FileName);
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)Math.Min(file.Length, ProcessJob.MaxInputBytes));
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>Traduce el error del dominio a <c>application/problem+json</c> con el mapeo compartido.</summary>
    private IActionResult Problem(Error error) =>
        ErrorHttpMapper.ToProblemResult(error, HttpContext.Request.Path);
}
