using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AmrGrandPrix.API.Services.LlmExtraction;

/// <summary>Live progress of a queued/running extraction, for the upload status endpoint.</summary>
public record ExtractionProgress(bool Started, int RowsExtracted);

/// <summary>
/// In-memory queue of upload batches awaiting LLM extraction, drained one at a time by
/// <see cref="ExtractionWorker"/>. The <c>UploadBatch</c> row (status <c>Extracting</c>) is the
/// durable record; this only holds the queue order, live row counts, and per-job cancellation.
/// Anything lost here on restart is rebuilt by the worker re-queuing <c>Extracting</c> batches.
/// </summary>
public sealed class ExtractionJobs
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    internal sealed class Job
    {
        private int _rows;
        private int _started;
        public CancellationTokenSource Cancellation { get; } = new();
        public bool Started => Volatile.Read(ref _started) == 1;
        public int Rows => Volatile.Read(ref _rows);
        public void MarkStarted() => Volatile.Write(ref _started, 1);
        public void ReportRows(int rows) => Volatile.Write(ref _rows, rows);
    }

    /// <summary>Queue a batch for extraction. No-op if it's already queued or running.</summary>
    public void Enqueue(Guid batchId)
    {
        if (_jobs.TryAdd(batchId, new Job()))
            _channel.Writer.TryWrite(batchId);
    }

    /// <summary>Progress of a batch, or null if it isn't queued here (e.g. right after a restart).</summary>
    public ExtractionProgress? GetProgress(Guid batchId) =>
        _jobs.TryGetValue(batchId, out var job) ? new ExtractionProgress(job.Started, job.Rows) : null;

    /// <summary>Stop a batch's extraction (it was deleted). Safe to call for unknown batches.</summary>
    public void Cancel(Guid batchId)
    {
        if (_jobs.TryRemove(batchId, out var job))
            job.Cancellation.Cancel();
    }

    internal IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    /// <summary>The job to run for a dequeued batch, or null if it was cancelled while queued.</summary>
    internal Job? Start(Guid batchId)
    {
        if (!_jobs.TryGetValue(batchId, out var job))
            return null;
        job.MarkStarted();
        return job;
    }

    internal void Complete(Guid batchId)
    {
        if (_jobs.TryRemove(batchId, out var job))
            job.Cancellation.Dispose();
    }
}
