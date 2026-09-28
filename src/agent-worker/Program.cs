using Amazon.Runtime;
using Amazon.SQS;
using EnterpriseKnowledgeAgent.AgentWorker;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Generation;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Application.Tokenization;
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

// TASK-OBS-02 (ASDD §23.2): metrics SDK - exports AppMetrics.MeterName (run
// duration/failure, retrieval latency/empty rate, evidence count, token
// usage/context utilization, invalid citations, AOAI latency/throttling,
// worker heartbeat age) plus HttpClient's built-in instrumentation (covers
// the SignalR-relay HTTP hop), to the console for local inspection (no OTLP
// collector required for local/dev).
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(EnterpriseKnowledgeAgent.Shared.Observability.AppMetrics.MeterName)
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    // TASK-OBS-03 (ASDD §23.3): tracing SDK - exports AppTracing.SourceName
    // (the agent-run pipeline's hand-authored stage spans) plus
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
builder.Services.Configure<SqsOptions>(builder.Configuration.GetSection(SqsOptions.SectionName));
builder.Services.Configure<RealtimeOptions>(builder.Configuration.GetSection(RealtimeOptions.SectionName));

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
builder.Services.AddScoped<IMessageQueue, SqsMessageQueue>();

// TASK-JOB-01/03/04/06: run/outbox/heartbeat persistence.
builder.Services.AddScoped<IAgentRunRepository, AgentRunRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IWorkerHeartbeatRepository, WorkerHeartbeatRepository>();

// TASK-KNW-01/CHK-02: knowledge lookups consumed by retrieval + evidence snapshotting.
builder.Services.AddScoped<IKnowledgeFileRepository, KnowledgeFileRepository>();
builder.Services.AddScoped<IKnowledgeChunkRepository, KnowledgeChunkRepository>();

// TASK-RET-01..05: retrieval (internal to the run pipeline).
builder.Services.AddScoped<IRetrievalService, RetrievalService>();

builder.Services.AddSingleton<ITokenEstimator, HeuristicTokenEstimator>();

// MODULE 12 - Azure OpenAI (same registrations/lifetimes as the API process, src/api/Program.cs).
builder.Services.AddOptions<AoiOptions>()
    .Bind(builder.Configuration.GetSection(AoiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AoiOptions>, AoiOptionsValidator>();
builder.Services.AddSingleton<AzureOpenAiClientFactory>();
builder.Services.AddSingleton<IDeploymentRegistry, ConfigDeploymentRegistry>();
builder.Services.AddSingleton<IEmbeddingService, AzureOpenAiEmbeddingService>();
builder.Services.AddSingleton<ITextGenerationService, AzureOpenAiTextGenerationService>();

// TASK-PRMT-01: pure context-budget token math - stateless, so a singleton.
builder.Services.AddSingleton<ContextBudgetCalculator>();

// TASK-GEN-01: generation call + requested/effective parameter recording.
builder.Services.AddScoped<GenerationService>();

// TASK-CIT-01/02/03: run-evidence + citation persistence.
builder.Services.AddScoped<IRunEvidenceRepository, RunEvidenceRepository>();
builder.Services.AddScoped<RunEvidenceService>();
builder.Services.AddScoped<ICitationRepository, CitationRepository>();
builder.Services.AddScoped<CitationService>();

// TASK-OUT-01: persists the completed run's Markdown output.
builder.Services.AddScoped<IGeneratedOutputRepository, GeneratedOutputRepository>();

// TASK-SIG-03/04: worker-side HTTP-relay publisher (separate process from the Api's SignalR hub - see HttpRealtimeEventPublisher's doc comment).
builder.Services.AddHttpClient<IRealtimeEventPublisher, HttpRealtimeEventPublisher>((sp, client) =>
{
    var realtime = sp.GetRequiredService<IOptions<RealtimeOptions>>().Value;
    client.BaseAddress = new Uri(realtime.ApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("X-Internal-Token", realtime.InternalToken);
});

// TASK-RUN-02: the agent-run pipeline orchestrator.
builder.Services.AddScoped<AgentRunOrchestrator>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
