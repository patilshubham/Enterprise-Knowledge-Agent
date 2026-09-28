using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Chunking;
using EnterpriseKnowledgeAgent.Application.Embeddings;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Application.Processing;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.DocumentWorker;
using EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;
using EnterpriseKnowledgeAgent.Infrastructure.Messaging;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Infrastructure.Realtime;
using EnterpriseKnowledgeAgent.Infrastructure.Storage;
using EnterpriseKnowledgeAgent.Infrastructure.Tokenization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// TASK-OBS-01 (ASDD §23.1): structured logging via Serilog, enriched with
// the correlation id each poll pushes onto the Serilog LogContext while
// processing a message (see Worker.ProcessWithCorrelationAsync).
builder.Services.AddSerilog((services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// TASK-OBS-02 (ASDD §23.2): metrics SDK - exports AppMetrics.MeterName (job
// duration/failure, embedding batches, chunks per file, queue depth/wait,
// AOAI latency/throttling, worker heartbeat age) plus HttpClient's built-in
// instrumentation (covers the SignalR-relay HTTP hop), to the console for
// local inspection (no OTLP collector required for local/dev).
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(EnterpriseKnowledgeAgent.Shared.Observability.AppMetrics.MeterName)
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    // TASK-OBS-03 (ASDD §23.3): tracing SDK - exports AppTracing.SourceName
    // (the document-processing pipeline's hand-authored stage spans) plus
    // HttpClient's built-in dependency spans.
    .WithTracing(tracing => tracing
        .AddSource(EnterpriseKnowledgeAgent.Shared.Observability.AppTracing.SourceName)
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

builder.Services.AddDbContext<EnterpriseKnowledgeAgentDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseVector()));

builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection(AwsOptions.SectionName));
builder.Services.Configure<S3Options>(builder.Configuration.GetSection(S3Options.SectionName));
builder.Services.Configure<SqsOptions>(builder.Configuration.GetSection(SqsOptions.SectionName));
builder.Services.Configure<KnowledgeFileOptions>(builder.Configuration.GetSection(KnowledgeFileOptions.SectionName));
builder.Services.Configure<ChunkingOptions>(builder.Configuration.GetSection(ChunkingOptions.SectionName));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
builder.Services.Configure<RealtimeOptions>(builder.Configuration.GetSection(RealtimeOptions.SectionName));

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var aws = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
    return new AmazonS3Client(new BasicAWSCredentials(aws.AccessKey, aws.SecretKey), new AmazonS3Config
    {
        ServiceURL = aws.ServiceUrl,
        AuthenticationRegion = aws.Region,
        ForcePathStyle = true,
    });
});
builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var aws = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
    return new AmazonSQSClient(new BasicAWSCredentials(aws.AccessKey, aws.SecretKey), new AmazonSQSConfig
    {
        ServiceURL = aws.ServiceUrl,
        AuthenticationRegion = aws.Region,
    });
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IObjectStorage, S3ObjectStorage>();
builder.Services.AddScoped<IMessageQueue, SqsMessageQueue>();

builder.Services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IWorkerHeartbeatRepository, WorkerHeartbeatRepository>();
builder.Services.AddScoped<IKnowledgeFileRepository, KnowledgeFileRepository>();
builder.Services.AddScoped<IKnowledgeChunkRepository, KnowledgeChunkRepository>();

// TASK-PRC-03/CHK-01: normalization/parsing/chunking abstractions.
builder.Services.AddScoped<IDocumentParser, MarkdownAwareDocumentParser>();
builder.Services.AddScoped<IChunker, TokenAwareChunker>();
builder.Services.AddSingleton<ITokenEstimator, HeuristicTokenEstimator>();

// MODULE 12 - Azure OpenAI (same registrations/lifetimes as the API process, src/api/Program.cs).
builder.Services.AddOptions<AoiOptions>()
    .Bind(builder.Configuration.GetSection(AoiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AoiOptions>, AoiOptionsValidator>();
builder.Services.AddSingleton<AzureOpenAiClientFactory>();
builder.Services.AddSingleton<IEmbeddingService, AzureOpenAiEmbeddingService>();

// TASK-EMB-01/02/03: real embedding pipeline stage (batching, bounded concurrency, validation, persistence, activation).
builder.Services.AddScoped<IEmbeddingPipelineStage, AzureOpenAiEmbeddingPipelineStage>();

// TASK-SIG-03/04: worker-side HTTP-relay publisher (separate process from the Api's SignalR hub - see HttpRealtimeEventPublisher's doc comment).
builder.Services.AddHttpClient<IRealtimeEventPublisher, HttpRealtimeEventPublisher>((sp, client) =>
{
    var realtime = sp.GetRequiredService<IOptions<RealtimeOptions>>().Value;
    client.BaseAddress = new Uri(realtime.ApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("X-Internal-Token", realtime.InternalToken);
});

builder.Services.AddScoped<DocumentProcessingOrchestrator>();
builder.Services.AddScoped<DeletionOrchestrator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
