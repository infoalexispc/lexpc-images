using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LexPCImages.API;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.IntegrationTests;

public sealed class OptimizerPipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OptimizerPipelineTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackgroundRemovalService>();
                services.AddSingleton<IBackgroundRemovalService, FakeBackgroundRemovalService>();
            });
        });
    }

    [Fact]
    public async Task Full_pipeline_upload_processes_and_returns_webp()
    {
        var client = _factory.CreateClient();
        var imageBytes = CreateTestPng(400, 300);

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, imageBytes, "image/png", "pc.png"));
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
    public async Task Download_returns_404_for_unknown_jobId()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/optimizer/jobs/{Guid.NewGuid()}/download");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_returns_409_when_job_is_queued()
    {
        var client = _factory.CreateClient();
        var imageBytes = CreateTestPng(400, 300);

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcHome.Id.Value, imageBytes, "image/png", "pc.png"));
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        var jobId = enqueued!.JobId.ToString();

        var download = await client.GetAsync($"/api/optimizer/jobs/{jobId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static MultipartFormDataContent BuildMultipart(string slotId, byte[] bytes, string contentType, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(slotId), "slotId");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    private static async Task<JobStatusResponseDto> PollUntilTerminalAsync(HttpClient client, string jobId, TimeSpan timeout)
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
            await Task.Delay(200);
        }
        throw new Xunit.Sdk.XunitException($"Job {jobId} did not reach terminal status in {timeout}. Last: {last?.Status} (progress={last?.Progress})");
    }

    private static byte[] CreateTestPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    row[x] = new Rgba32(200, 50, 50, 255);
                }
            }
        });
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed record EnqueueJobResponseDto(Guid JobId, string Status);
    private sealed record JobStatusResponseDto(Guid JobId, string Status, string? Stage, int Progress);
}

internal sealed class FakeBackgroundRemovalService : IBackgroundRemovalService
{
    public Task<MaskResult> RemoveBackgroundAsync(DecodedImage image, CancellationToken cancellationToken)
    {
        var mask = new float[image.Width * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var idx = y * image.Width + x;
                var cx = image.Width / 2f;
                var cy = image.Height / 2f;
                var dx = (x - cx) / cx;
                var dy = (y - cy) / cy;
                var dist = (dx * dx + dy * dy);
                mask[idx] = dist < 0.8f ? 1.0f : 0.0f;
            }
        }
        return Task.FromResult(new MaskResult(image.Width, image.Height, mask));
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(T))
            {
                services.RemoveAt(i);
            }
        }
    }
}
