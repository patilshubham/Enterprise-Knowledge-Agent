namespace EnterpriseKnowledgeAgent.Application.Embeddings;

/// <summary>
/// Final stage of the document-processing pipeline (ASDD §13, modules 12/13
/// - Azure OpenAI embeddings): given a file whose chunks have just been
/// persisted inactive (TASK-CHK-02), generates and stores their vectors and
/// then activates the chunks so retrieval can see them (TASK-CHK-02's
/// <c>Activate()</c>). Implemented by <see cref="AzureOpenAiEmbeddingPipelineStage"/>
/// (TASK-EMB-01/02/03).
/// </summary>
public interface IEmbeddingPipelineStage
{
    /// <param name="activateOnCompletion">
    /// TASK-REP-02: when <see langword="false"/>, chunks are embedded and
    /// persisted as usual but left inactive - the caller (replacement flow)
    /// performs the actual old-generation removal + new-generation
    /// activation itself, atomically, via
    /// <c>IKnowledgeChunkRepository.SwapReplacementAsync</c>, so the new
    /// content never becomes searchable before the old content is removed.
    /// Defaults to <see langword="true"/> (the normal first-time-processing
    /// behavior, unchanged).
    /// </param>
    /// <param name="onProgress">
    /// TASK-EMB-01: optional callback invoked after each batch completes with
    /// the fraction (0..1) of batches finished so far, letting the caller
    /// (<see cref="DocumentProcessingOrchestrator"/>) map it onto the
    /// job's overall progress percentage. Invocations are sequential/awaited
    /// (never concurrent), so it is safe for the callback to touch a
    /// non-thread-safe resource such as an EF Core <c>DbContext</c>.
    /// </param>
    Task RunAsync(
        Guid applicationId,
        Guid fileId,
        Func<double, CancellationToken, Task>? onProgress = null,
        bool activateOnCompletion = true,
        CancellationToken cancellationToken = default);
}
