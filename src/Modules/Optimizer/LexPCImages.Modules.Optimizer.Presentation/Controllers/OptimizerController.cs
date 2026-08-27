using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Errors;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Modules.Optimizer.Presentation.Responses;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LexPCImages.Modules.Optimizer.Presentation.Controllers;

[ApiController]
[Route("api/optimizer/jobs")]
[AllowAnonymous]
public sealed class OptimizerController : ControllerBase
{
    private readonly EnqueueJobHandler _enqueue;
    private readonly GetJobStatusHandler _getStatus;
    private readonly IJobRepository _jobs;

    public OptimizerController(
        EnqueueJobHandler enqueue,
        GetJobStatusHandler getStatus,
        IJobRepository jobs)
    {
        _enqueue = enqueue;
        _getStatus = getStatus;
        _jobs = jobs;
    }

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EnqueueJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enqueue(
        [FromForm] string slotId,
        [FromForm] IFormFile file,
        [FromForm] bool? shadowSuppression = null,
        [FromForm] bool? deskRemoval = null,
        [FromForm] bool? legProtection = null,
        [FromForm] double? cropMarginPct = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return BadRequest(new { code = "optimizer.slot_id_required", message = "slotId is required." });
        }
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { code = "optimizer.file_required", message = "file is required and cannot be empty." });
        }
        if (cropMarginPct is < 0 or > 0.5)
        {
            return BadRequest(new { code = "optimizer.crop_margin_out_of_range", message = "cropMarginPct must be between 0 and 0.5." });
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var refinement = new RefinementOverrides(
            SuppressShadow: shadowSuppression,
            RemoveDesk: deskRemoval,
            ProtectLegs: legProtection,
            CropMarginPct: cropMarginPct);

        var command = new EnqueueJobCommand(
            SlotId: LexPCImages.Modules.Optimizer.Domain.ValueObjects.SlotId.Parse(slotId),
            ImageBytes: memory.ToArray(),
            ContentType: file.ContentType,
            Refinement: refinement);

        var result = await _enqueue.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        var response = new EnqueueJobResponse(result.Value!.JobId, result.Value.Status.ToString());
        return AcceptedAtAction(nameof(GetStatus), new { id = response.JobId }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getStatus.HandleAsync(new GetJobStatusQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }
        return Ok(JobStatusResponseMapper.From(result.Value!));
    }

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetAsync(id, cancellationToken);
        if (job is null)
        {
            return MapError(OptimizerErrors.JobNotFound(id.ToString()));
        }
        if (job.Status != JobStatus.Done || job.OutputImage is null || job.OutputContentType is null)
        {
            return Conflict(new
            {
                code = "optimizer.job_not_ready",
                message = $"Job is in status '{job.Status}'. Download is only available when status is 'Done'.",
                status = job.Status.ToString(),
                progress = job.Progress,
            });
        }

        var fileName = $"pc-home-{id:N}.webp";
        return File(job.OutputImage, job.OutputContentType, fileName);
    }

    private IActionResult MapError(Error error)
    {
        var problem = new
        {
            type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15",
            title = error.Type.ToString(),
            status = StatusCodes.Status400BadRequest,
            code = error.Code,
            detail = error.Message,
        };
        return StatusCode(
            error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError,
            },
            problem);
    }
}
