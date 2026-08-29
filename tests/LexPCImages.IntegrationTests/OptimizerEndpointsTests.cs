using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.IntegrationTests;

public sealed class OptimizerEndpointsTests : IClassFixture<OptimizerWebApplicationFactory>
{
    private readonly OptimizerWebApplicationFactory _factory;

    public OptimizerEndpointsTests(OptimizerWebApplicationFactory factory)
    {
        _factory = factory;
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

    [Fact]
    public async Task Enqueue_returns_202_with_a_single_job_for_a_plain_slot()
    {
        var client = _factory.CreateClient();
        var slot = SlotDefinition.PcMainSection;

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(slot.Id.Value, TestImages.Png(400, 300), "image/png", "test.png"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        json.Should().NotBeNull();
        var job = json!.Jobs.Should().ContainSingle().Subject;
        job.JobId.Should().NotBeEmpty();
        job.SlotId.Should().Be(slot.Id.Value);
        job.Width.Should().Be(slot.Width);
        job.Height.Should().Be(slot.Height);
        job.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task Enqueue_returns_one_job_per_output_when_the_slot_is_a_bundle()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotBundle.PcHome.Id.Value, TestImages.Png(800, 600), "image/png", "pc.png"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var json = await response.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        json!.Jobs.Should().HaveCount(2);
        json.Jobs.Select(job => (job.Width, job.Height)).Should().Equal((320, 315), (992, 715));
        json.Jobs.Select(job => job.JobId).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Enqueue_returns_404_for_unknown_slot()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart("nonexistent-slot", TestImages.Png(400, 300), "image/png", "test.png"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Enqueue_returns_400_for_unsupported_content_type()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcMainSection.Id.Value, [0x01, 0x02], "image/gif", "test.gif"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Enqueue_returns_400_when_the_bytes_are_not_a_real_image()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcMainSection.Id.Value, [0x4D, 0x5A, 0x90], "image/png", "fake.png"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem!.Code.Should().Be("optimizer.image_content_mismatch");
    }

    [Fact]
    public async Task Error_responses_are_problem_json_with_a_machine_readable_code()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/optimizer/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem!.Code.Should().Be("optimizer.job_not_found");
        problem.Status.Should().Be(404);
    }

    [Fact]
    public async Task Enqueue_then_GetStatus_returns_consistent_data()
    {
        var client = _factory.CreateClient();

        var enqueueResponse = await client.PostAsync(
            "/api/optimizer/jobs",
            BuildMultipart(SlotDefinition.PcMainSection.Id.Value, TestImages.Png(400, 300), "image/png", "test.png"));
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueued = await enqueueResponse.Content.ReadFromJsonAsync<EnqueueJobResponseDto>();
        var jobId = enqueued!.Jobs[0].JobId;

        var statusResponse = await client.GetAsync($"/api/optimizer/jobs/{jobId}");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await statusResponse.Content.ReadFromJsonAsync<JobStatusResponseDto>();
        status.Should().NotBeNull();
        status!.JobId.Should().Be(jobId);
        status.Status.Should().BeOneOf("Queued", "Processing", "Done");
    }

    [Fact]
    public async Task Module_health_endpoint_responds()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/optimizer/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record EnqueueJobResponseDto(IReadOnlyList<EnqueuedJobDto> Jobs);
    private sealed record EnqueuedJobDto(Guid JobId, string SlotId, int Width, int Height, string Status);
    private sealed record JobStatusResponseDto(Guid JobId, string Status, string? Stage, int Progress);
    private sealed record ProblemDetailsDto(string? Title, int? Status, string? Detail, string? Code);
}
