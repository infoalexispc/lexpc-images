using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LexPCImages.API;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexPCImages.IntegrationTests;

public sealed class OptimizerEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OptimizerEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private static MultipartFormDataContent BuildMultipart(string slotId, byte[] bytes, string contentType, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(slotId), "slotId");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Enqueue_returns_202_with_jobId_for_valid_image()
    {
        var client = CreateClient();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic-ish

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, bytes, "image/png", "test.png"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        json.Should().NotBeNull();
        json!.JobId.Should().NotBeEmpty();
        json.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task Enqueue_returns_404_for_unknown_slot()
    {
        var client = CreateClient();
        var bytes = new byte[] { 0x01, 0x02 };

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart("nonexistent-slot", bytes, "image/png", "test.png"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Enqueue_returns_400_for_unsupported_content_type()
    {
        var client = CreateClient();
        var bytes = new byte[] { 0x01, 0x02 };

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, bytes, "image/gif", "test.gif"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStatus_returns_404_for_unknown_jobId()
    {
        var client = CreateClient();
        var unknownId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/optimizer/jobs/{unknownId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Enqueue_then_GetStatus_returns_consistent_data()
    {
        var client = CreateClient();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, bytes, "image/jpeg", "test.jpg"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();

        var statusResponse = await client.GetAsync($"/api/optimizer/jobs/{enqueued!.JobId}");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponseDto>();
        status.Should().NotBeNull();
        status!.JobId.Should().Be(enqueued.JobId);
        status.Status.Should().Be("Queued");
        status.Progress.Should().Be(0);
    }

    private sealed record EnqueueJobResponseDto(Guid JobId, string Status);
    private sealed record JobStatusResponseDto(Guid JobId, string Status, string? Stage, int Progress);
}
