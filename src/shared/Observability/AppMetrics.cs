using System.Diagnostics.Metrics;

namespace EnterpriseKnowledgeAgent.Shared.Observability;

/// <summary>
/// TASK-OBS-02 (ASDD §23.2): a single process-wide <see cref="Meter"/> shared
/// by the API and both background workers, so every instrument in this
/// class is registered once (<see cref="MeterName"/>) and exported the same
/// way regardless of which process emits it. Kept in Shared (rather than
/// duplicated per project) because instrumentation call sites span the Api,
/// Application, and Infrastructure layers. Instrument names follow OTEL's
/// dot-separated naming convention; tag/label values are always small,
/// closed-set strings (status codes, categories, operation names) - never
/// free-form content, file names, or user input, per the "no sensitive
/// content in metric labels" requirement.
/// </summary>
public static class AppMetrics
{
    public const string MeterName = "EnterpriseKnowledgeAgent";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    // --- API request latency / error rate ---
    public static readonly Histogram<double> ApiRequestDurationMs = Meter.CreateHistogram<double>(
        "api.request.duration", unit: "ms", description: "API request duration in milliseconds.");
    public static readonly Counter<long> ApiRequestErrors = Meter.CreateCounter<long>(
        "api.request.errors", description: "Count of API requests that returned a 4xx/5xx status code.");

    // --- Queue depth / oldest-message age ---
    public static readonly Histogram<long> QueueDepth = Meter.CreateHistogram<long>(
        "queue.depth", description: "Approximate visible/in-flight/DLQ message count for a queue, sampled at read time.");
    public static readonly Histogram<double> QueueMessageWaitSeconds = Meter.CreateHistogram<double>(
        "queue.message.wait", unit: "s", description: "Seconds between a message being sent and received (proxy for oldest-message age under load).");

    // --- Job (document processing) duration / failure ---
    public static readonly Histogram<double> JobDurationMs = Meter.CreateHistogram<double>(
        "job.duration", unit: "ms", description: "Document-processing job duration in milliseconds.");
    public static readonly Counter<long> JobFailures = Meter.CreateCounter<long>(
        "job.failures", description: "Count of document-processing job failures, tagged by error category.");

    // --- Agent run duration / failure ---
    public static readonly Histogram<double> RunDurationMs = Meter.CreateHistogram<double>(
        "run.duration", unit: "ms", description: "Agent run duration in milliseconds.");
    public static readonly Counter<long> RunFailures = Meter.CreateCounter<long>(
        "run.failures", description: "Count of agent run failures, tagged by error category.");

    // --- Azure OpenAI latency / throttling ---
    public static readonly Histogram<double> AoaiRequestDurationMs = Meter.CreateHistogram<double>(
        "aoai.request.duration", unit: "ms", description: "Azure OpenAI request duration in milliseconds, including retries, tagged by operation and outcome.");
    public static readonly Counter<long> AoaiThrottles = Meter.CreateCounter<long>(
        "aoai.throttles", description: "Count of Azure OpenAI 429 (throttled) responses encountered, tagged by operation.");

    // --- Embedding batches / chunks per file ---
    public static readonly Counter<long> EmbeddingBatches = Meter.CreateCounter<long>(
        "embedding.batches", description: "Count of embedding batches submitted to Azure OpenAI.");
    public static readonly Histogram<long> ChunksPerFile = Meter.CreateHistogram<long>(
        "chunking.chunks_per_file", description: "Number of chunks produced for a processed file.");

    // --- Retrieval latency / empty-retrieval rate ---
    public static readonly Histogram<double> RetrievalDurationMs = Meter.CreateHistogram<double>(
        "retrieval.duration", unit: "ms", description: "Retrieval (semantic + keyword search) duration in milliseconds.");
    public static readonly Counter<long> RetrievalTotal = Meter.CreateCounter<long>(
        "retrieval.total", description: "Count of retrieval operations executed.");
    public static readonly Counter<long> RetrievalEmpty = Meter.CreateCounter<long>(
        "retrieval.empty", description: "Count of retrievals that returned no qualifying evidence.");

    // --- Evidence count / token usage / context utilization ---
    public static readonly Histogram<long> EvidenceCount = Meter.CreateHistogram<long>(
        "evidence.count", description: "Number of evidence sources persisted for a run.");
    public static readonly Histogram<long> ContextTokenUsage = Meter.CreateHistogram<long>(
        "context.token_usage", description: "Token count consumed by a prompt layer for a run, tagged by layer.");
    public static readonly Histogram<double> ContextUtilization = Meter.CreateHistogram<double>(
        "context.utilization", description: "Fraction (0-1) of the deployment's context window consumed by the fixed prompt layers for a run.");

    // --- Invalid citation count ---
    public static readonly Counter<long> InvalidCitations = Meter.CreateCounter<long>(
        "citations.invalid", description: "Count of citation markers that did not reference a valid in-run evidence source.");

    // --- Worker heartbeat age ---
    public static readonly Histogram<double> WorkerHeartbeatAgeSeconds = Meter.CreateHistogram<double>(
        "worker.heartbeat.age", unit: "s", description: "Age in seconds of a worker's most recent heartbeat, sampled at health-check time, tagged by worker kind.");
}
