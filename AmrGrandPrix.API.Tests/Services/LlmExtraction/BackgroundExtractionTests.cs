using System.Text;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.LlmExtraction;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AmrGrandPrix.API.Tests.Services.LlmExtraction;

public class ExtractionJobRunnerTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options);
    private readonly Mock<ILlmExtractionService> _llm = new();

    public void Dispose() => _context.Dispose();

    private ExtractionJobRunner Runner() => new(_context, _llm.Object, Mock.Of<ILogger<ExtractionJobRunner>>());

    private async Task<UploadBatch> AddBatchAsync(UploadStatus status = UploadStatus.Extracting)
    {
        var batch = new UploadBatch
        {
            UploadBatchId = Guid.NewGuid(), FileName = "results.pdf", Status = status,
            ExtractedText = "1 Alice 1:00:00", KnownVariants = new List<string> { "Dome" }, OnlyTheseVariants = true
        };
        _context.UploadBatches.Add(batch);
        await _context.SaveChangesAsync();
        return batch;
    }

    private void SetupExtraction(Func<Task<ExtractionResult>> result) =>
        _llm.Setup(l => l.ExtractFromTextAsync("1 Alice 1:00:00", "results.pdf",
                It.Is<IReadOnlyList<string>?>(v => v!.SequenceEqual(new[] { "Dome" })), true,
                It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()))
            .Returns(result);

    [Fact]
    public async Task RunAsync_Success_MarksBatchPendingWithLlmAuditData()
    {
        var batch = await AddBatchAsync();
        var row = new ExtractedRow(1, "Alice", 30, null, "F", "1:00:00", "Finished", null);
        SetupExtraction(() => Task.FromResult(new ExtractionResult(
            new List<ExtractedSection> { new(null, null, null, new List<ExtractedRow> { row, row }) },
            "{\"sections\":[]}", "claude-test", 100, 50)));

        await Runner().RunAsync(batch.UploadBatchId, null, CancellationToken.None);

        var saved = await _context.UploadBatches.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(UploadStatus.Pending);
        saved.RawLlmJson.Should().Be("{\"sections\":[]}");
        saved.LlmModel.Should().Be("claude-test");
        saved.LlmInputTokens.Should().Be(100);
        saved.LlmOutputTokens.Should().Be(50);
        saved.RecordsUploaded.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_LlmError_MarksBatchFailedWithMessage()
    {
        var batch = await AddBatchAsync();
        SetupExtraction(() => throw new InvalidOperationException("LLM returned invalid JSON: truncated"));

        await Runner().RunAsync(batch.UploadBatchId, null, CancellationToken.None);

        var saved = await _context.UploadBatches.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(UploadStatus.Failed);
        saved.ExtractionError.Should().Be("LLM returned invalid JSON: truncated");
    }

    [Fact]
    public async Task RunAsync_Cancelled_LeavesBatchExtractingAndRethrows()
    {
        var batch = await AddBatchAsync();
        using var cts = new CancellationTokenSource();
        SetupExtraction(() => { cts.Cancel(); throw new OperationCanceledException(cts.Token); });

        var act = () => Runner().RunAsync(batch.UploadBatchId, null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await _context.UploadBatches.AsNoTracking().SingleAsync()).Status.Should().Be(UploadStatus.Extracting);
    }

    [Fact]
    public async Task RunAsync_BatchNoLongerExtracting_DoesNothing()
    {
        var batch = await AddBatchAsync(UploadStatus.Pending);

        await Runner().RunAsync(batch.UploadBatchId, null, CancellationToken.None);

        _llm.VerifyNoOtherCalls();
    }
}

public class ExtractionJobsTests
{
    [Fact]
    public void Enqueue_SameBatchTwice_QueuesItOnce()
    {
        var jobs = new ExtractionJobs();
        var id = Guid.NewGuid();

        jobs.Enqueue(id);
        jobs.Enqueue(id);

        jobs.Start(id).Should().NotBeNull();
        jobs.Complete(id);
        jobs.GetProgress(id).Should().BeNull();
    }

    [Fact]
    public void Cancel_WhileQueued_StartReturnsNull()
    {
        var jobs = new ExtractionJobs();
        var id = Guid.NewGuid();
        jobs.Enqueue(id);

        jobs.Cancel(id);

        jobs.Start(id).Should().BeNull();
    }
}

public class StreamedRowCounterTests
{
    [Fact]
    public void Advance_CountsRowsAsJsonStreamsIn_IncludingKeysSplitAcrossDeltas()
    {
        const string json = """{"sections":[{"name":"Men","rows":[{"name":"A","status":"Finished"},{"name":"B \"status\" C","status":"DNF"},{"name":"C","status":"Finished"}]}]}""";
        var counter = new StreamedRowCounter();
        var builder = new StringBuilder();
        var reported = new List<int>();

        // Feed it in 5-char deltas so "status" keys get split across chunks.
        for (var i = 0; i < json.Length; i += 5)
        {
            builder.Append(json.AsSpan(i, Math.Min(5, json.Length - i)));
            if (counter.Advance(builder))
                reported.Add(counter.Count);
        }

        counter.Count.Should().Be(3, "an escaped \\\"status\\\" inside a name isn't a key");
        reported.Should().Equal(1, 2, 3);
    }
}

public class ResultRowEstimatorTests
{
    [Fact]
    public void Estimate_CountsLinesWithTimesOrNonFinishStatuses()
    {
        const string text = """
            Mount Marathon 2026 - Men
            Place Name Age Time
            1 Alice Runner 30 45:12
            2 Bob Runner 41 1:02:33.4
            3 Cat Runner 55 DNF
            Printed 7/4/2026
            """;

        ResultRowEstimator.Estimate(text).Should().Be(3);
    }

    [Fact]
    public void Estimate_NoRows_ReturnsNull() =>
        ResultRowEstimator.Estimate("Just a header\nand a footer").Should().BeNull();
}
