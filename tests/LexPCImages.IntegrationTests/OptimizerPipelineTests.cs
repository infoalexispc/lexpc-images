using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.IntegrationTests;

public sealed class OptimizerPipelineTests : IClassFixture<OptimizerWebApplicationFactory>
{
    private readonly OptimizerWebApplicationFactory _factory;

    public OptimizerPipelineTests(OptimizerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_bundle_turns_one_upload_into_two_downloadable_sizes()
    {
        var client = _factory.CreateClient();

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotBundle.PcHome.Id.Value, TestImages.Png(800, 600), "image/png", "pc.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        enqueued!.Jobs.Should().HaveCount(2);

        var sizes = new List<(int Width, int Height)>();
        foreach (var job in enqueued.Jobs)
        {
            var final = await PollUntilTerminalAsync(client, job.JobId.ToString(), TimeSpan.FromSeconds(20));
            final.Status.Should().Be("Done");

            var download = await client.GetAsync($"/api/optimizer/jobs/{job.JobId}/download");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            download.Content.Headers.ContentType?.MediaType.Should().Be("image/webp");
            download.Content.Headers.ContentDisposition!.FileName.Should().Contain(job.SlotId);

            using var image = SixLabors.ImageSharp.Image.Load(await download.Content.ReadAsByteArrayAsync());
            sizes.Add((image.Width, image.Height));
        }

        sizes.Should().Equal((320, 315), (992, 715));
    }

    [Fact]
    public async Task Fit_transparent_slot_leaves_the_leftover_bands_fully_transparent()
    {
        var client = _factory.CreateClient();
        // 800x600 es mas apaisada que 320x315: al encajarla proporcional sobran bandas arriba y abajo.
        var slot = SlotDefinition.PcHomeSmall;

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(slot.Id.Value, TestImages.Png(800, 600), "image/png", "pc.png"));
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        var jobId = enqueued!.Jobs[0].JobId;

        var final = await PollUntilTerminalAsync(client, jobId.ToString(), TimeSpan.FromSeconds(20));
        final.Status.Should().Be("Done");

        var download = await client.GetAsync($"/api/optimizer/jobs/{jobId}/download");
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(await download.Content.ReadAsByteArrayAsync());
        image.Width.Should().Be(320);
        image.Height.Should().Be(315);
        image[160, 1].A.Should().Be(0, "la banda superior no lleva color de relleno, solo alfa 0");
        image[160, image.Height - 2].A.Should().Be(0, "la banda inferior tampoco");
        image[160, image.Height / 2].A.Should().Be(255, "la imagen en si sigue siendo opaca");
    }

    [Fact]
    public async Task Resize_and_pad_slot_produces_an_image_of_the_slot_size()
    {
        var client = _factory.CreateClient();
        var slot = SlotDefinition.PcMainSection;

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(slot.Id.Value, TestImages.Png(800, 450), "image/png", "banner.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var jobId = enqueued!.Jobs[0].JobId;
        var final = await PollUntilTerminalAsync(client, jobId.ToString(), TimeSpan.FromSeconds(20));
        final.Status.Should().Be("Done");

        var download = await client.GetAsync($"/api/optimizer/jobs/{jobId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);

        using var image = SixLabors.ImageSharp.Image.Load(await download.Content.ReadAsByteArrayAsync());
        image.Width.Should().Be(slot.Width);
        image.Height.Should().Be(slot.Height);
    }

    [Fact]
    public async Task Cover_or_pad_slot_crops_a_source_whose_aspect_ratio_is_close_to_the_slot()
    {
        var client = _factory.CreateClient();
        var slot = SlotDefinition.PcLastSection;

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(slot.Id.Value, TestImages.PngWithSideMarkers(1000, 1000), "image/png", "ficha.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var jobId = enqueued!.Jobs[0].JobId;
        var final = await PollUntilTerminalAsync(client, jobId.ToString(), TimeSpan.FromSeconds(20));
        final.Status.Should().Be("Done");

        var download = await client.GetAsync($"/api/optimizer/jobs/{jobId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(await download.Content.ReadAsByteArrayAsync());
        image.Width.Should().Be(619);
        image.Height.Should().Be(720);
        ShouldLookLike(
            image[10, image.Height / 2],
            TestImages.Background,
            "el recorte centrado se come las marcas laterales");
    }

    [Fact]
    public async Task Cover_or_pad_slot_pads_a_wide_source_with_the_detected_background()
    {
        var client = _factory.CreateClient();
        var slot = SlotDefinition.PcLastSection;

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(slot.Id.Value, TestImages.PngWithSideMarkers(1600, 900), "image/png", "ficha.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var jobId = enqueued!.Jobs[0].JobId;
        var final = await PollUntilTerminalAsync(client, jobId.ToString(), TimeSpan.FromSeconds(20));
        final.Status.Should().Be("Done");

        var download = await client.GetAsync($"/api/optimizer/jobs/{jobId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(await download.Content.ReadAsByteArrayAsync());
        image.Width.Should().Be(619);
        image.Height.Should().Be(720);
        ShouldLookLike(
            image[10, image.Height / 2],
            TestImages.Marker,
            "al rellenar se conserva la imagen entera, marcas incluidas");
        ShouldLookLike(
            image[image.Width / 2, 5],
            TestImages.Background,
            "la banda superior se rellena con el color de fondo detectado");
        image[image.Width / 2, 5].A.Should().Be(255, "el relleno es opaco, no transparente");
    }

    [Fact]
    public async Task A_decode_failure_marks_the_job_as_error_without_leaking_internals()
    {
        var client = _factory.CreateClient();
        // Cabecera PNG válida con contenido corrupto: pasa la validación de firma y revienta al decodificar.
        var corruptPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xDE, 0xAD, 0xBE, 0xEF };

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHomeSmall.Id.Value, corruptPng, "image/png", "pc.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var jobId = enqueued!.Jobs[0].JobId;
        var final = await PollUntilTerminalAsync(client, jobId.ToString(), TimeSpan.FromSeconds(10));

        final.Status.Should().Be("Error");
        final.ErrorMessage.Should().Contain("Check the server logs");
        final.ErrorMessage.Should().NotContain("SixLabors", "el mensaje no debe filtrar detalles internos");
    }

    [Fact]
    public async Task Download_returns_404_for_unknown_jobId()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/optimizer/jobs/{Guid.NewGuid()}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_returns_409_when_job_is_not_done_yet()
    {
        var client = _factory.CreateClient();
        // Imagen grande: garantiza que el trabajo sigue en curso cuando se pide la descarga.
        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHomeSmall.Id.Value, TestImages.Png(3000, 3000), "image/png", "pc.png"));
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var download = await client.GetAsync($"/api/optimizer/jobs/{enqueued!.Jobs[0].JobId}/download");

        download.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>El remuestreo Lanczos3 no devuelve el color exacto, asi que se compara con holgura.</summary>
    private static void ShouldLookLike(Rgba32 actual, Rgba32 expected, string because)
    {
        const int Tolerance = 24;
        var distance =
            Math.Abs(actual.R - expected.R)
            + Math.Abs(actual.G - expected.G)
            + Math.Abs(actual.B - expected.B);

        distance.Should().BeLessThanOrEqualTo(
            Tolerance, "{0} (esperado {1}, obtenido {2})", because, expected, actual);
    }

    private static MultipartFormDataContent BuildMultipart(
        string slotId, byte[] bytes, string contentType, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(slotId), "slotId");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    private static async Task<JobStatusResponseDto> PollUntilTerminalAsync(
        HttpClient client, string jobId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        JobStatusResponseDto? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/optimizer/jobs/{jobId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            last = await response.Content.ReadFromJsonAsync<JobStatusResponseDto>();
            if (last is { Status: "Done" or "Error" })
            {
                return last;
            }
            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"Job {jobId} did not reach terminal status in {timeout}. Last: {last?.Status} (progress={last?.Progress})");
    }

    private sealed record EnqueueJobResponseDto(IReadOnlyList<EnqueuedJobDto> Jobs);
    private sealed record EnqueuedJobDto(Guid JobId, string SlotId, int Width, int Height, string Status);
    private sealed record JobStatusResponseDto(
        Guid JobId, string Status, string? Stage, int Progress, string? ErrorMessage = null);
}
