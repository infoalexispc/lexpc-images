using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.IntegrationTests;

public sealed class OptimizerPipelineTests : IClassFixture<OptimizerWebApplicationFactory>
{
    private readonly OptimizerWebApplicationFactory _factory;

    public OptimizerPipelineTests(OptimizerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Full_pipeline_upload_processes_and_returns_webp()
    {
        var client = _factory.CreateClient();

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, TestImages.Png(400, 300), "image/png", "pc.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        enqueued.Should().NotBeNull();

        var jobId = enqueued!.JobId.ToString();
        var final = await PollUntilTerminalAsync(client, jobId, TimeSpan.FromSeconds(15));

        final.Status.Should().Be("Done");
        final.Progress.Should().Be(100);

        var download = await client.GetAsync($"/api/optimizer/jobs/{jobId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType?.MediaType.Should().Be("image/webp");
        var downloaded = await download.Content.ReadAsByteArrayAsync();
        downloaded.Length.Should().BeGreaterThan(0);
        downloaded[0..4].Should().Equal((byte)'R', (byte)'I', (byte)'F', (byte)'F');
    }

    [Fact]
    public async Task Download_file_name_is_derived_from_the_slot()
    {
        var client = _factory.CreateClient();

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, TestImages.Png(400, 300), "image/png", "pc.png"));
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        var jobId = enqueued!.JobId.ToString();
        await PollUntilTerminalAsync(client, jobId, TimeSpan.FromSeconds(15));

        var download = await client.GetAsync($"/api/optimizer/jobs/{jobId}/download");

        download.Content.Headers.ContentDisposition!.FileName
            .Should().Contain(SlotDefinition.PcHome.Id.Value);
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

        var final = await PollUntilTerminalAsync(client, enqueued!.JobId.ToString(), TimeSpan.FromSeconds(20));
        final.Status.Should().Be("Done");

        var download = await client.GetAsync($"/api/optimizer/jobs/{enqueued.JobId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);

        using var image = SixLabors.ImageSharp.Image.Load(await download.Content.ReadAsByteArrayAsync());
        image.Width.Should().Be(slot.Width);
        image.Height.Should().Be(slot.Height);
    }

    [Fact]
    public async Task A_decode_failure_marks_the_job_as_error_without_leaking_internals()
    {
        var client = _factory.CreateClient();
        // Cabecera PNG válida con contenido corrupto: pasa la validación de firma y revienta al decodificar.
        var corruptPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xDE, 0xAD, 0xBE, 0xEF };

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, corruptPng, "image/png", "pc.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var final = await PollUntilTerminalAsync(client, enqueued!.JobId.ToString(), TimeSpan.FromSeconds(10));

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
            BuildMultipart(SlotDefinition.PcHome.Id.Value, TestImages.Png(3000, 3000), "image/png", "pc.png"));
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var download = await client.GetAsync($"/api/optimizer/jobs/{enqueued!.JobId}/download");

        download.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

    private sealed record EnqueueJobResponseDto(Guid JobId, string Status);
    private sealed record JobStatusResponseDto(
        Guid JobId, string Status, string? Stage, int Progress, string? ErrorMessage = null);
}
