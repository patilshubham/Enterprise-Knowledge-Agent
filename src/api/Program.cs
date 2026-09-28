using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using EnterpriseKnowledgeAgent.Api;
using EnterpriseKnowledgeAgent.Api.Correlation;
using EnterpriseKnowledgeAgent.Api.ErrorHandling;
using EnterpriseKnowledgeAgent.Api.HealthChecks;
using EnterpriseKnowledgeAgent.Api.Observability;
using EnterpriseKnowledgeAgent.Api.Realtime;
using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Chat;
using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Dashboards;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Generation;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;
using EnterpriseKnowledgeAgent.Infrastructure.Jobs;
using EnterpriseKnowledgeAgent.Infrastructure.Messaging;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Infrastructure.Realtime;
using EnterpriseKnowledgeAgent.Infrastructure.Storage;
using EnterpriseKnowledgeAgent.Infrastructure.Tokenization;
using EnterpriseKnowledgeAgent.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// TASK-OBS-01 (ASDD §23.1): structured logging via Serilog, enriched with
// whatever the CorrelationIdMiddleware pushes onto the Serilog LogContext
// per request. Console sink only (local/dev-friendly; matches the ASDD's
// "console/OTLP for local" guidance for module 33) - no appsettings
// "Serilog" section is required since everything is configured in code.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// TASK-OBS-02 (ASDD §23.2): metrics SDK - exports AppMetrics.MeterName (the
// app's own counters/histograms, e.g. api.request.duration recorded by
// RequestMetricsMiddleware below) plus ASP.NET Core's/HttpClient's built-in
// instrumentation, to the console for local inspection (no OTLP collector
// required for local/dev).
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(AppMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    // TASK-OBS-03 (ASDD §23.3): tracing SDK - exports AppTracing.SourceName
    // (the hand-authored "Authorize"/"Load agent snapshot" spans below)
    // plus ASP.NET Core's/HttpClient's built-in request/dependency spans.
    .WithTracing(tracing => tracing
        .AddSource(AppTracing.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// TASK-PG-01: Postgres/pgvector-backed DbContext. UseVector() (from
// Pgvector.EntityFrameworkCore) registers the `vector` type mapping so later
// modules can add vector columns without extra per-DbContext wiring.
builder.Services.AddDbContext<EnterpriseKnowledgeAgentDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseVector()));

// TASK-APP-01: Application aggregate repository.
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ApplicationsService>();
// TASK-APP-04: shared archived/deleted precondition for future modules
// (uploads, agent config, run submission, output edits) to depend on.
builder.Services.AddScoped<IApplicationReadOnlyGuard, ApplicationReadOnlyGuard>();

// -----------------------------------------------------------------------
// TASK-S3-01/SQS-01: AWS S3 + SQS clients pointed at LocalStack (path-style
// addressing + fake credentials - real AWS deployment would instead rely on
// the SDK's default credential chain / IAM role and omit ServiceURL).
// -----------------------------------------------------------------------
builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection(AwsOptions.SectionName));
builder.Services.Configure<S3Options>(builder.Configuration.GetSection(S3Options.SectionName));
builder.Services.Configure<SqsOptions>(builder.Configuration.GetSection(SqsOptions.SectionName));
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection("Outbox"));
builder.Services.Configure<JobsOptions>(builder.Configuration.GetSection(JobsOptions.SectionName));

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

// TASK-S3-01/02/04: S3-backed object storage.
builder.Services.AddScoped<IObjectStorage, S3ObjectStorage>();

// TASK-SQS-01/02: SQS-backed message queue + typed producers (only the
// outbox publisher below is expected to call the producers - Decision D1).
builder.Services.AddScoped<IMessageQueue, SqsMessageQueue>();
builder.Services.AddScoped<IDocumentProcessingProducer, DocumentProcessingProducer>();
builder.Services.AddScoped<IAgentExecutionProducer, AgentExecutionProducer>();
builder.Services.AddScoped<IDeletionProducer, DeletionProducer>();

// TASK-JOB-01/02/03: job/run/outbox/heartbeat persistence + creation service.
builder.Services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();
builder.Services.AddScoped<IAgentRunRepository, AgentRunRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IWorkerHeartbeatRepository, WorkerHeartbeatRepository>();
builder.Services.AddScoped<JobCreationService>();

// TASK-JOB-04/05/06: outbox publisher, reconciler safety-net, and lease
// reaper - all hosted in the API process (see each type's remarks for why).
builder.Services.AddHostedService<OutboxPublisherService>();
builder.Services.AddHostedService<JobReconcilerService>();
builder.Services.AddHostedService<LeaseReaperService>();

// TASK-AUD-01: append-only audit trail writer + search.
builder.Services.AddScoped<IAuditWriter, AuditEventRepository>();
builder.Services.AddScoped<IAuditEventRepository, AuditEventRepository>();

// TASK-KNW-01/02/03/04/05: knowledge file management.
builder.Services.Configure<KnowledgeFileOptions>(builder.Configuration.GetSection(KnowledgeFileOptions.SectionName));
builder.Services.AddScoped<IKnowledgeFileRepository, KnowledgeFileRepository>();
builder.Services.AddScoped<IKnowledgeChunkRepository, KnowledgeChunkRepository>();
builder.Services.AddScoped<FileValidationService>();
builder.Services.AddScoped<KnowledgeFilesService>();

// TASK-RET-01..05: retrieval (internal to the run pipeline - no HTTP endpoint;
// consumed by the agent execution pipeline, module 20+).
builder.Services.AddScoped<IRetrievalService, RetrievalService>();

// TASK-AGT-01/02/03: agent configuration CRUD + deployment-capability validation.
builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<AgentParameterValidator>();
builder.Services.AddScoped<AgentsService>();

// TASK-RUN-01: agent run submission (snapshot + idempotency), built on top of JobCreationService.
builder.Services.AddScoped<AgentRunSubmissionService>();

// Module CHAT: shared knowledge-base conversations (config-free defaults +
// optional advanced overrides) and the SSE streaming answer pipeline.
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ChatCompletionService>();

// TASK-AOI-04: deployment-aware interface shape; still the character-based
// heuristic underneath (no tokenizer library in the package set).
builder.Services.AddSingleton<ITokenEstimator, HeuristicTokenEstimator>();

// -----------------------------------------------------------------------
// MODULE 12 - Azure OpenAI (TASK-AOI-01/02/03/05). All singletons: the
// client/credential and retry policy are stateless/thread-safe, and
// AoaiHealthService intentionally holds cross-request cache state.
// -----------------------------------------------------------------------
builder.Services.AddOptions<AoiOptions>()
    .Bind(builder.Configuration.GetSection(AoiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AoiOptions>, AoiOptionsValidator>();
builder.Services.AddSingleton<AzureOpenAiClientFactory>();
builder.Services.AddSingleton<IDeploymentRegistry, ConfigDeploymentRegistry>();
builder.Services.AddSingleton<IEmbeddingService, AzureOpenAiEmbeddingService>();
builder.Services.AddSingleton<ITextGenerationService, AzureOpenAiTextGenerationService>();
builder.Services.AddSingleton<IAoaiHealthService, AoaiHealthService>();

// TASK-PRMT-01: pure context-budget token math (deployment context capacity
// minus fixed prompt layers minus output reservation) - stateless, so a singleton.
builder.Services.AddSingleton<ContextBudgetCalculator>();

// TASK-GEN-01: generation call + requested/effective parameter recording.
builder.Services.AddScoped<GenerationService>();

// TASK-CIT-01/02: persists the run's evidence package as RunEvidence rows
// (snapshotted filename/heading/passage/offsets), idempotent per run.
builder.Services.AddScoped<IRunEvidenceRepository, RunEvidenceRepository>();
builder.Services.AddScoped<RunEvidenceService>();

// TASK-CIT-03: parses + validates citation markers against a run's
// persisted evidence, persisting a Citation per valid marker.
builder.Services.AddScoped<ICitationRepository, CitationRepository>();
builder.Services.AddScoped<CitationService>();

// TASK-OUT-01: persists the completed run's Markdown output.
builder.Services.AddScoped<IGeneratedOutputRepository, GeneratedOutputRepository>();

// TASK-OUT-02/03/04/05 + TASK-CIT-04: output detail/list/edit/actions and citation evidence lookups.
builder.Services.AddScoped<OutputsService>();

// TASK-PDSH-01/TASK-ADSH-01: platform-wide + per-application dashboard aggregation.
builder.Services.AddScoped<PlatformDashboardService>();
builder.Services.AddScoped<ApplicationDashboardService>();

// TASK-PRDSH-01/02: platform-wide + per-application processing-job list/detail queries.
builder.Services.AddScoped<ProcessingJobsQueryService>();

// TASK-RDSH-01/02: per-application agent-run list/detail queries.
builder.Services.AddScoped<AgentRunsQueryService>();

// -----------------------------------------------------------------------
// MODULE 25 - SignalR (TASK-SIG-01/03/04). AddSignalR() registers
// IHubContext<RealtimeHub> regardless of where MapHub is called from.
// HubContextRealtimeEventPublisher is the in-process publisher used by
// job/run *creation* (JobCreationService runs inside this same Api
// process); document-worker/agent-worker instead register
// HttpRealtimeEventPublisher in their own Program.cs (Infrastructure
// layer) since they are separate OS processes that cannot use
// IHubContext directly - both relay into the same RealtimeHub groups
// either way (see RealtimeEndpoints for the worker-facing relay).
// -----------------------------------------------------------------------
builder.Services.Configure<RealtimeOptions>(builder.Configuration.GetSection(RealtimeOptions.SectionName));
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeEventPublisher, HubContextRealtimeEventPublisher>();

// TASK-OPS-01 (ASDD §24): liveness is process-responsiveness only (no
// dependency calls - a dependency outage must not make the process itself
// look dead to the orchestrator and get needlessly restarted). Readiness
// checks every hard dependency the API needs to actually serve traffic.
// Azure OpenAI is intentionally NOT included here - deep AOAI checks are
// user-initiated/sparing (ASDD §24) via the existing
// GET /api/operations/aoai-health endpoint, not part of routine readiness
// polling.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
    .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"])
    .AddCheck<MessageQueueHealthCheck>("message-queue", tags: ["ready"])
    .AddCheck<WorkerHeartbeatHealthCheck>("workers", tags: ["ready"]);

// TASK-FND-06: same-origin proxying (Vite dev proxy / nginx in compose)
// keeps the frontend and API on the same origin in normal use. This policy
// is a fallback for direct cross-origin access (e.g. a contributor running
// the frontend dev server without the proxy) - no credentials are involved
// since the app has no login/session of its own.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// TASK-ERR-01: centralized Problem Details error handling (ASDD §22) - the
// safety net for anything that escapes an endpoint's local try/catch.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// TASK-OBS-01: registered first of all so every later middleware/endpoint -
// including the exception handler below - runs with the resolved
// correlation id already published to CorrelationContext + the Serilog
// LogContext.
app.UseMiddleware<CorrelationIdMiddleware>();

// TASK-OBS-02: records api.request.duration/api.request.errors for every
// request; placed right after correlation resolution so its own log/metric
// correlation-id tag (via LogContext) is already available, and before the
// exception handler so the recorded status code reflects any exception
// UseExceptionHandler below turns into a response.
app.UseMiddleware<RequestMetricsMiddleware>();

// TASK-ERR-01: registered next so it wraps every later middleware/endpoint
// and can turn any escaped exception into a safe Problem Details response.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// TASK-MDR-02: baseline security headers (defense-in-depth alongside the
// SPA's sanitization + the equivalent headers nginx.conf sets for the
// frontend). This is a pure JSON API with no HTML views/Swagger UI, so a
// "deny everything" CSP is safe - there is no first-party script/style for
// this origin to serve.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'self'";
    await next();
});

app.UseCors(FrontendCorsPolicy);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// TASK-APP-02: Application CRUD + lifecycle endpoints.
app.MapApplicationsEndpoints();

// TASK-JOB-06: manual retry endpoints for processing jobs / agent runs.
app.MapJobsEndpoints();

// TASK-KNW-03/04/05: knowledge file upload/list/preview/disable/enable/delete/reprocess.
app.MapKnowledgeFilesEndpoints();

// TASK-AGT-02: agent CRUD/lifecycle endpoints.
app.MapAgentsEndpoints();

// TASK-RUN-01: agent run submission endpoint.
app.MapRunsEndpoints();

// Module CHAT: knowledge-base chat assistant (conversations + SSE answers).
app.MapChatEndpoints();

// TASK-OUT-02/03/04/05 + TASK-CIT-04: output detail/list/edit/actions and citation evidence endpoints.
app.MapOutputsEndpoints();

// TASK-AOI-01/05: deployment registry + AOAI availability endpoints.
app.MapAzureOpenAiEndpoints();

// TASK-PDSH-01/TASK-ADSH-01: platform-wide + per-application dashboard aggregation endpoints.
app.MapDashboardsEndpoints();

// TASK-OPS-03: platform operations aggregation endpoint (component health, queue depths, worker heartbeats).
app.MapOperationsEndpoints();

// TASK-PRDSH-01/02: platform-wide + per-application processing-job list/detail endpoints.
app.MapProcessingEndpoints();

// TASK-AUD-03: platform-wide + per-application audit search endpoints.
app.MapAuditEndpoints();

// TASK-SIG-01: authenticated realtime hub - nginx.conf already proxies
// /hubs/ with WebSocket upgrade headers (TASK-FND-06/MDR-02).
app.MapHub<RealtimeHub>("/hubs/realtime");

// TASK-SIG-03/04: internal, non-proxied relay endpoint for the worker-side
// HTTP publisher (see the AddSignalR block above for why this exists).
app.MapRealtimeEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
