using Microsoft.AspNetCore.Http;

namespace LexPCImages.Modules.Optimizer.Presentation.Requests;

public sealed record EnqueueJobApiRequest(string SlotId, IFormFile File);
