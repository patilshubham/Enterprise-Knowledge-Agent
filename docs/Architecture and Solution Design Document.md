# Architecture and Solution Design Document

## Enterprise Knowledge Agent Platform

**Version:** 1.1  
**Status:** Implementation Baseline  
**Architecture style:** Modular monolith with two dedicated background workers  
**Stack:** React, TypeScript, ASP.NET Core on .NET 10, Keycloak (OIDC identity provider), SignalR, PostgreSQL with pgvector, LocalStack S3 and SQS, Azure OpenAI, Docker Compose

---

## 1. Executive Summary

The platform converts application-specific TXT and Markdown knowledge into searchable vector content and allows prompt-configured agents to generate grounded Markdown outputs.

The updated solution removes Ollama and uses existing Azure OpenAI model deployments for both embeddings and text generation. It also removes broad versioning. The design retains only the history required to keep the system understandable and supportable:

- The current active file content is searchable.
- A replacement is processed safely before replacing current searchable content.
- Each agent has one current editable configuration.
- Each run stores an immutable snapshot of the prompt, model deployment, parameters, scope, and evidence used.
- Generated output stores the original generated text and current edited text.

This gives the project enough traceability without introducing a user-facing version-management system.

The implementation is intentionally focused on the main path:

```text
Application
→ Upload
→ Process
→ Embed
→ Retrieve
→ Generate
→ Preview Markdown
```

---

## 2. Architecture Objectives

The architecture shall:

- Run all application infrastructure locally through Docker Compose except Azure OpenAI.
- Keep the web experience simple.
- Use background workers for long-running ingestion and agent execution.
- Provide real-time processing and run progress through SignalR.
- Keep PostgreSQL as the authoritative state store.
- Use pgvector instead of a separate vector database.
- Use LocalStack S3 for uploaded file bytes.
- Use LocalStack SQS for job delivery.
- Use Azure OpenAI for embeddings and generation.
- Enforce application boundaries at every data-access point.
- Support large prompts and model-aware context budgeting.
- Store enough run history to troubleshoot output quality.
- Avoid cache, Redis, broad event sourcing, and unnecessary microservices.

---

## 3. Key Design Decisions

### 3.1 Modular monolith

The ASP.NET Core API is one deployable business application organized into internal modules. This reduces deployment and debugging complexity while keeping boundaries clear.

Modules:

```text
Identity (Keycloak OIDC integration)
Applications
Knowledge
Processing
Retrieval
Agents
AgentExecution
Outputs
Citations
AzureOpenAI
Audit
Operations
Notifications
```

Note: the `Membership` module is not a separate internal module. Application-scoped role assignment (`ApplicationAdmin` / `ApplicationViewer`) is resolved from a Keycloak-issued claim, not a local table (§19).

### 3.2 Dedicated workers

Two workers are deployed separately:

- Document Processing Worker
- Agent Execution Worker

This keeps long-running, retryable, model-dependent operations outside HTTP request lifetimes.

### 3.3 PostgreSQL plus pgvector

PostgreSQL stores relational data, operational job state, full-text search data, chunks, and embeddings. pgvector supplies vector similarity search without introducing a separate database.

### 3.4 Azure OpenAI gateway

All embedding and generation calls pass through internal interfaces. The initial provider is Azure OpenAI only. This is an abstraction for maintainability, not a multi-provider routing feature.

### 3.5 Minimal history model

The system does not implement general versioning. It records only:

- Current file data and the safe replacement operation
- Current agent configuration
- Immutable run snapshots
- Original and edited output text
- Processing attempts and audit events

### 3.6 No cache

The initial solution reads from PostgreSQL, S3, and Azure OpenAI directly. Cache invalidation and Redis are deferred until measured performance requires them.

---

## 4. System Context

```text
┌──────────────────────────────────────────────────────────┐
│ React Browser Application                               │
│ Uploads, dashboards, agents, runs, Markdown, citations  │
└───────────────────────┬──────────────────────────────────┘
                        │ REST + SignalR
                        ▼
┌──────────────────────────────────────────────────────────┐
│ ASP.NET Core API                                        │
│ OIDC/BFF session, authorization, application services,  │
│ hubs                                                     │
└──────────────┬────────────────┬───────────────┬──────────┘
               │                │               │ OIDC (Authorization Code + PKCE)
               ▼                ▼               ▼
┌────────────────────────┐  ┌──────────────────────────┐  ┌──────────────────────────┐
│ PostgreSQL + pgvector  │  │ LocalStack               │  │ Keycloak                 │
│ Business state         │  │ S3 source files          │  │ Credentials, realm       │
│ Jobs and runs          │  │ SQS processing queues    │  │ roles, app-role claims   │
│ Chunks and vectors     │  │ Dead-letter queues       │  │ (own Postgres database)  │
│ Full-text search       │  └──────────────┬───────────┘  └──────────────────────────┘
└───────────▲────────────┘                 │
            │                              ▼
            │                ┌─────────────────────────────┐
            ├────────────────│ Document Processing Worker  │
            │                └──────────────┬──────────────┘
            │                               │ Embeddings
            │                               ▼
            │                  ┌──────────────────────────┐
            │                  │ Azure OpenAI            │
            │                  │ Embeddings + generation │
            │                  └──────────────────────────┘
            │                               ▲
            │                ┌──────────────┴──────────────┐
            └────────────────│ Agent Execution Worker      │
                             └─────────────────────────────┘
```

---

## 5. Physical Deployment

### 5.1 Docker Compose services

```text
frontend
api
document-worker
agent-worker
postgres-pgvector
keycloak
keycloak-postgres
localstack
```

Azure OpenAI is an external managed dependency. Keycloak and its dedicated `keycloak-postgres` database are local infrastructure (own container/database, separate from `postgres-pgvector`), not an external managed dependency.

### 5.2 Service responsibilities

#### Frontend

- Hosts the React application.
- Calls REST endpoints.
- Connects to SignalR.
- Contains no Azure OpenAI credentials.

#### API

- Delegates authentication to Keycloak via OpenID Connect, using the Backend-for-Frontend (BFF) pattern: it performs the Authorization Code + PKCE handshake server-side and issues its own HTTP-only session cookie (tokens never reach browser JS).
- Applies role and application authorization from Keycloak-issued claims (platform roles, application-scoped roles).
- Manages applications, files, agents, runs, and outputs.
- Streams accepted files to S3.
- Creates jobs and sends SQS messages.
- Hosts SignalR hubs.
- Exposes health and operations endpoints.

#### Document worker

- Consumes document-processing messages.
- Downloads source content from S3.
- Validates and parses text.
- Creates chunks.
- Calls the Azure OpenAI embedding deployment.
- Saves chunks and vectors.
- Updates job progress.

#### Agent worker

- Consumes run messages.
- Loads the run and agent snapshot.
- Performs application-scoped retrieval.
- Assembles the model request.
- Calls the Azure OpenAI chat deployment.
- Validates and saves Markdown and citations.
- Updates run progress.

#### PostgreSQL

- Stores business state.
- Stores processing jobs and agent runs.
- Stores chunks and vectors.
- Supports full-text retrieval.

#### LocalStack

- Emulates S3 and SQS.
- Hosts processing, execution, deletion, and dead-letter queues.

---

## 6. Network Design

All local containers join one private Docker network. Service communication uses Docker DNS names:

```text
api:8080
postgres:5432
localstack:4566
```

Only the frontend and API need host exposure. PostgreSQL and LocalStack ports may be exposed under a development profile for diagnostics.

Azure OpenAI HTTPS access is outbound from the API and workers. The browser never connects directly to Azure OpenAI.

---

## 7. Frontend Design

### 7.1 Recommended stack

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- SignalR JavaScript client
- Monaco Editor or CodeMirror
- `react-markdown`
- `remark-gfm`
- A proven HTML sanitizer
- A UI component library

### 7.2 Route structure

```text
/login

/platform/dashboard
/platform/applications
/platform/processing
/platform/users
/platform/audit
/platform/operations

/applications/:applicationId/overview
/applications/:applicationId/knowledge
/applications/:applicationId/knowledge/:fileId
/applications/:applicationId/processing
/applications/:applicationId/agents
/applications/:applicationId/agents/:agentId
/applications/:applicationId/runs
/applications/:applicationId/runs/:runId
/applications/:applicationId/outputs
/applications/:applicationId/outputs/:outputId
/applications/:applicationId/activity
```

### 7.3 State model

- TanStack Query owns server state.
- React local state owns form and editor interaction.
- SignalR events invalidate or patch TanStack Query results.
- REST responses remain authoritative after reconnect.

### 7.4 Agent editor

The editor provides:

- Name and description
- Large prompt editor
- Character and estimated token count
- Azure OpenAI deployment selection
- Temperature
- Maximum output tokens
- Top P when supported
- Retrieval mode
- Chunk count
- Similarity threshold
- Evidence-token limit
- Strict-grounding toggle
- Citation toggle
- File scope
- Save and test actions

Agent changes overwrite the current editable configuration. There is no published-version workflow in the first implementation.

### 7.5 Output experience

The output screen supports:

- Preview
- Markdown source
- Split view
- Citation evidence drawer
- Copy
- Download `.md`
- Admin edit
- Regenerate as a new run

---

## 8. Backend Layering

```text
API Layer
↓
Application Layer
↓
Domain Layer
↓
Infrastructure Layer
```

### API layer

- REST endpoints
- SignalR hubs
- Request validation
- Authentication and authorization
- Problem Details responses

### Application layer

- Use-case handlers
- Commands and queries
- Transaction coordination
- Job creation
- Run orchestration

### Domain layer

- Application, file, job, agent, run, and output rules
- Valid state transitions
- Value objects and domain errors

### Infrastructure layer

- Entity Framework Core
- Npgsql and pgvector
- LocalStack S3 and SQS adapters
- Azure OpenAI adapters
- Markdown parser
- Token estimator
- SignalR publisher
- Structured logging

---

## 9. Core Data Model

```text
Application
KnowledgeFile
KnowledgeChunk
ProcessingJob
ProcessingAttempt
Agent
AgentRun
RunEvidence
GeneratedOutput
Citation
AuditEvent
ModelDeploymentConfiguration
WorkerHeartbeat
```

There is no local `User` or `ApplicationMembership` entity (decision D20, §19). User identity, credentials, platform roles, and application-scoped roles are all owned by Keycloak; the application reads them as claims on the authenticated principal rather than persisting them itself. Entities that reference "the acting user" (e.g. `UploadedBy`, audit actor) store the Keycloak subject id (`sub`) as an opaque string, not a foreign key into a local users table.

### 9.1 Knowledge file

```text
KnowledgeFile
- Id
- ApplicationId
- DisplayName
- ObjectKey
- ContentHash
- FileSize
- MimeType
- Encoding
- Status
- IsEnabled
- UploadedBy
- UploadedAt
- LastProcessedAt
- ReplacementObjectKey, nullable
- ReplacementStatus, nullable
```

The replacement fields are temporary operational state, not a general file-version system.

### 9.2 Knowledge chunk

```text
KnowledgeChunk
- Id
- ApplicationId
- FileId
- SequenceNumber
- HeadingPath
- Content
- TokenCount
- CharacterStart
- CharacterEnd
- ContentHash
- EmbeddingDeployment
- EmbeddingDimensions
- Embedding vector
- SearchVector
- IsActive
```

### 9.3 Agent

```text
Agent
- Id
- ApplicationId
- Name
- Description
- Prompt
- Status
- KnowledgeScopeType
- GenerationDeployment
- Temperature
- MaximumOutputTokens
- TopP
- RetrievalMode
- RetrievalCount
- MinimumSimilarity
- MaximumEvidenceTokens
- IncludeNeighbors
- StrictGrounding
- CitationsRequired
- CreatedBy
- CreatedAt
- UpdatedBy
- UpdatedAt
- ConcurrencyToken
```

### 9.4 Agent run

```text
AgentRun
- Id
- ApplicationId
- AgentId
- RequestedBy
- UserRequest
- Status
- Stage
- Progress
- AgentNameSnapshot
- AgentPromptSnapshot
- KnowledgeScopeSnapshot
- DeploymentSnapshot
- RequestedParametersJson
- EffectiveParametersJson
- RetrievalSettingsJson
- StartedAt
- CompletedAt
- FailureCategory
- FailureMessage
- CorrelationId
```

### 9.5 Output

```text
GeneratedOutput
- Id
- ApplicationId
- AgentRunId
- Title
- OriginalMarkdown
- CurrentMarkdown
- IsEdited
- CreatedBy
- CreatedAt
- LastEditedBy
- LastEditedAt
- IsDeleted
```

---

## 10. PostgreSQL Schema and Indexing

Suggested schemas:

```text
platform
knowledge
agents
execution
outputs
operations
audit
```

There is no `identity` schema in this database (decision D20) — Keycloak owns all credential/account/role data in its own separate `keycloak-postgres` database, entirely outside this schema list and outside EF Core's migrations.

Essential indexes:

```text
knowledge_files(application_id, status, is_enabled)
knowledge_files(application_id, content_hash)
knowledge_chunks(application_id, file_id, is_active)
processing_jobs(application_id, status, created_at)
agents(application_id, status)
agent_runs(application_id, status, created_at)
generated_outputs(application_id, created_at)
audit_events(application_id, occurred_at)
```

Vector search begins with exact cosine search for a modest local dataset. Add HNSW only after query latency and corpus size justify it.

PostgreSQL full-text search shall use a generated or maintained `tsvector` value for chunk text and heading path.

---

## 11. LocalStack Design

### 11.1 S3 buckets

```text
rag-source-files
rag-generated-exports
```

The export bucket is optional until asynchronous exports are needed.

### 11.2 S3 key

```text
applications/{applicationId}/files/{fileId}/source
```

A replacement may use:

```text
applications/{applicationId}/files/{fileId}/replacement/{replacementId}
```

After successful processing, the replacement becomes the active source and the old object is deleted according to cleanup policy.

### 11.3 SQS queues

```text
document-processing
document-processing-dlq
agent-execution
agent-execution-dlq
deletion-jobs
deletion-jobs-dlq
```

Messages contain identifiers and correlation data only.

---

## 12. Job Reliability

### 12.1 Delivery semantics

SQS is treated as at least once. Workers must safely handle duplicate delivery.

### 12.2 Job claim

A worker atomically changes a queued job to running and records:

- Worker ID
- Started time
- Lease expiration
- Attempt number
- Heartbeat time

A message for an already completed or actively claimed job is ignored safely.

### 12.3 Database and queue consistency

For the initial focused implementation, use one of these approaches:

**Preferred:** A small PostgreSQL outbox publisher that commits the job and queue message intent in one database transaction.

**Acceptable first milestone:** Create the job, attempt SQS publication, and run a periodic reconciler that republishes queued jobs without a message-delivered marker.

The outbox is preferred because it prevents jobs from becoming permanently stuck if database commit succeeds but SQS publication fails.

### 12.4 Retry

Automatically retry:

- Azure OpenAI throttling
- Temporary Azure OpenAI failure
- S3 timeout
- Database connectivity interruption
- SQS transient failure

Do not automatically retry:

- Invalid file
- Unsupported encoding
- Empty content
- Context overflow caused by configuration
- Unauthorized operation
- Invalid deployment configuration
- Embedding-dimension mismatch

---

## 13. File Processing Pipeline

```text
Queue message
→ Claim job
→ Load file and application
→ Download source from S3
→ Verify hash and size
→ Validate text encoding
→ Normalize text
→ Parse TXT or Markdown
→ Create token-aware chunks
→ Generate embeddings in batches
→ Save chunks and vectors
→ Validate counts and dimensions
→ Mark file Ready
→ Notify via SignalR
```

### 13.1 Chunking

Markdown chunking shall:

- Prefer heading and paragraph boundaries.
- Preserve heading hierarchy.
- Avoid breaking code fences.
- Record source offsets.
- Use token-aware size limits.
- Add controlled overlap only when needed.

Starting configuration:

```text
Target: 700 tokens
Maximum: 1,100 tokens
Overlap: 100 tokens
```

These are defaults and should be validated with representative documents.

### 13.2 Embedding batches

The worker shall:

- Respect Azure OpenAI input and request limits.
- Batch chunks within configured limits.
- Use bounded concurrency.
- Track completed batches for progress.
- Verify returned vector count and dimensions.
- Reject non-finite vectors.

### 13.3 Safe replacement

Replacement processing writes temporary chunks marked inactive. When all chunks are valid, one database transaction:

1. Removes or deactivates old chunks.
2. Activates replacement chunks.
3. Updates the S3 object reference.
4. Updates file hash and metadata.
5. Marks the file Ready.

If processing fails, temporary replacement chunks and object are cleaned up while the current file remains searchable.

---

## 14. Azure OpenAI Integration

### 14.1 Configuration

Server configuration shall include:

```text
Endpoint
Authentication mode
Chat deployment name
Embedding deployment name
Known context limit
Known maximum output tokens
Embedding dimensions
Request timeout
Maximum retry count
```

### 14.2 Authentication

Preferred authentication is Microsoft Entra ID through `DefaultAzureCredential` for local development and managed identity in Azure. API-key authentication can be supported through server-side secrets.

Credentials are never persisted in application tables and never sent to React.

### 14.3 Internal interfaces

```text
IEmbeddingService
- GenerateEmbeddingsAsync(inputs, cancellationToken)
- CheckHealthAsync()

ITextGenerationService
- GenerateAsync(request, cancellationToken)
- CheckHealthAsync()

ITokenEstimator
- Estimate(text, deployment)
```

### 14.4 Deployment registry

The initial deployment registry may be configuration-based rather than a complex administrative module.

Each entry records:

- Friendly name
- Azure deployment name
- Purpose: embedding or generation
- Enabled status
- Context limit
- Maximum output limit
- Supported parameters
- Embedding dimensions where applicable

### 14.5 Rate limiting and retries

Use bounded exponential retry for transient status codes and throttling. Respect any server-provided retry delay. Do not retry invalid requests or context overflow.

### 14.6 Network resilience

Set explicit request timeouts. The processing or run job remains durable if Azure OpenAI is temporarily unavailable.

---

## 15. Retrieval Design

### 15.1 Retrieval flow

```text
User request
→ Authorization
→ Load current agent configuration
→ Resolve enabled file scope
→ Create query embedding
→ Semantic search
→ Optional PostgreSQL keyword search
→ Normalize and combine ranks
→ Remove duplicate or overlapping passages
→ Optional neighbor inclusion
→ Apply evidence token budget
→ Return evidence package
```

### 15.2 Application boundary

Every vector and keyword query includes:

```text
application_id = authorized application
file enabled = true
file status = Ready
chunk active = true
optional selected file IDs
```

The system never searches the global vector set and filters results afterward.

### 15.3 Query text

Start by embedding the user's request. For broad document-generation requests, a concise retrieval query may combine the user request with the agent objective. Do not embed the entire large prompt automatically.

### 15.4 Hybrid search

The hybrid strategy:

1. Retrieve semantic candidates.
2. Retrieve full-text candidates.
3. Normalize ranks separately.
4. Combine with configurable weights.
5. Remove duplicates.
6. Apply threshold and context budget.

### 15.5 Context budget

```text
Available evidence tokens =
Model context capacity
- platform instructions
- current agent prompt
- user request
- output reservation
- protocol overhead
```

The agent prompt is never silently truncated. If fixed content exceeds the model context window, the run fails before calling Azure OpenAI with a clear configuration error.

### 15.6 Evidence package

Each selected chunk receives an internal source identifier:

```text
SOURCE-001
SOURCE-002
```

The model receives filename, heading path, and content inside strongly delimited source blocks.

---

## 16. Agent Execution Pipeline

```text
User submits request
→ API authorizes user
→ API snapshots current agent configuration
→ API creates run and queue job
→ Agent worker claims run
→ Validate Azure OpenAI deployment availability
→ Calculate context budget
→ Retrieve evidence
→ Assemble prompt
→ Call Azure OpenAI
→ Validate Markdown and citations
→ Save run evidence and output
→ Mark completed
→ SignalR notifies user
```

### 16.1 Prompt layers

The generated request contains separate logical sections:

1. Platform safety and grounding instructions
2. Agent prompt snapshot
3. Retrieved evidence marked as untrusted data
4. User request
5. Markdown and citation output protocol

### 16.2 Large prompts

Before a run:

- Estimate platform instruction tokens.
- Estimate agent prompt tokens.
- Estimate user request tokens.
- Reserve maximum output tokens.
- Calculate remaining evidence capacity.
- Reject impossible configurations.

### 16.3 Run snapshots

The run snapshot is the main traceability mechanism and replaces broad agent versioning. It freezes what was used for that specific output even if the agent is edited later.

### 16.4 Partial knowledge

If files are still processing, the run uses ready files only and records excluded files. The UI displays a warning before execution.

### 16.5 Generation streaming

For the first milestone, show stage progress but save and display the final validated output after completion. Token streaming can be added later without changing the run model.

---

## 17. Citation Design

### 17.1 Structural storage

```text
AgentRun
→ RunEvidence
→ KnowledgeChunk
→ KnowledgeFile

GeneratedOutput
→ Citation
→ RunEvidence
```

### 17.2 Citation protocol

Prefer requesting a structured model response containing Markdown and citation references. If the selected deployment does not reliably support structured output, use a constrained citation marker syntax and parse it server-side.

### 17.3 Validation

A citation is valid only when its source identifier exists in evidence supplied for the current run. Unknown or cross-application identifiers are rejected.

### 17.4 UI

The citation drawer displays:

- Filename
- Heading path
- Matching passage
- Surrounding text
- Processing time

---

## 18. SignalR Design

### 18.1 Events

```text
FileQueued
FileStageChanged
FileCompleted
FileFailed
RunQueued
RunStageChanged
RunCompleted
RunFailed
DashboardChanged
```

### 18.2 Groups

```text
user:{userId}
application:{applicationId}
run:{runId}
platform:operations
```

The server validates the caller's `eka_platform_role`/`eka_app_role` claims before adding a connection to a group.

### 18.3 Event contract

Each event contains:

- Event ID
- Event type
- Application ID
- Entity ID
- UTC timestamp
- Entity sequence number
- Safe payload

The client ignores stale sequence numbers.

### 18.4 Source of truth

Workers save status in PostgreSQL before notification. If a SignalR event is lost, the frontend recovers current state through REST.

---

## 19. Authentication and Authorization

### 19.1 Identity provider: Keycloak via OpenID Connect (Backend-for-Frontend)

Keycloak is the sole identity provider and system of record for user accounts and credentials. The ASP.NET Core API integrates with it using OpenID Connect and the **Backend-for-Frontend (BFF) pattern**:

- The API performs the OIDC Authorization Code flow with PKCE against Keycloak server-side.
- Access/ID tokens are handled only by the API; they are never sent to, or stored in, the browser.
- After a successful handshake, the API issues its own HTTP-only session cookie to the browser (see §19.2). The cookie — not a token — is the artifact the SPA holds.
- Keycloak owns password hashing, password policy, account lockout, and credential lifecycle entirely; the application does not implement any of this itself.

Roles arrive as claims copied onto the session principal during the OIDC handshake:

- **Platform roles** (`SuperAdmin`, `SuperViewer`) are Keycloak **realm roles**, exposed via a multivalued `eka_platform_role` claim.
- **Application-scoped roles** (`ApplicationAdmin`, `ApplicationViewer`) are a Keycloak **user attribute**, formatted `"{applicationId}:{Role}"` per entry, exposed via a multivalued `eka_app_role` claim. There is no local Postgres membership table; assigning a user to an application is done by editing this Keycloak attribute (admin console, or realm-import JSON for local dev).

Local development runs Keycloak as its own Docker Compose service with its own dedicated Postgres database (separate from the application's `postgres-pgvector` database), with a realm auto-imported at startup that includes a bootstrap Super Admin — no separate application-side seeding step is required.

### 19.2 Browser session

Use secure HTTP-only cookies for the React web application, with CSRF protection and appropriate SameSite settings. Under the BFF pattern, this cookie is issued by the API's own cookie-authentication scheme once the OIDC handshake with Keycloak completes, rather than being issued directly from a local username/password check.

### 19.3 Policies

```text
CanViewApplication
CanManageKnowledge
CanManageAgents
CanExecuteAgent
CanViewOutput
CanManageOutput
CanViewPlatformProcessing
CanViewAudit
```

Each policy's handler evaluates the `eka_platform_role` and/or `eka_app_role` claims on the authenticated principal for the target application. (`CanManageUsers` is not part of this list — there is no local user-management surface; see §28 and the backlog's Module 5 removal note.)

### 19.4 Resource authorization

Every file, agent, run, output, and citation is loaded with its application ID and checked against the caller's `eka_platform_role` claim or their `eka_app_role` claim entry for that application.

---

## 20. Security Design

### 20.1 Cross-application isolation

Isolation is enforced in:

- Database queries
- Vector queries
- Full-text queries
- S3 object paths
- SQS job validation
- SignalR group membership
- Output and citation endpoints

### 20.2 Prompt injection

Retrieved files are untrusted data. The platform:

- Separates system, agent, evidence, and user content.
- Does not expose secrets to the model.
- Gives the model no tools or external actions.
- Constrains citations to supplied evidence.
- Sanitizes output.
- Logs suspicious failures without logging full content.

### 20.3 Markdown safety

```text
Markdown
→ GFM parser
→ raw HTML disabled
→ allowed URL validation
→ HTML sanitization
→ React rendering
```

Block script content, event handlers, unsafe protocols, and arbitrary embedded frames.

### 20.4 Secret storage

For local development, use environment variables, .NET user secrets, or Docker secrets. Do not commit endpoint keys or credentials.

### 20.5 Logging

Do not log full source documents, prompts, user requests, generated outputs, credentials, or embeddings by default.

---

## 21. Dashboard and Operations Design

### 21.1 Platform dashboard

- Applications
- Users
- Total files
- Queued and active processing jobs
- Failed jobs
- Active and failed runs
- Worker health
- Azure OpenAI availability
- Dead-letter queue counts

### 21.2 Application dashboard

- Knowledge readiness
- Ready, processing, and failed files
- Active agents
- Recent runs
- Recent outputs
- Attention-required items

### 21.3 Processing details

- Job ID
- File
- Stage
- Progress
- Batch counts
- Attempts
- Worker
- Duration
- Error category
- Correlation ID
- Retry action

### 21.4 Run details

- Agent snapshot
- User request
- Deployment
- Requested and effective parameters
- Retrieval settings
- Evidence used
- Stage timings
- Output
- Citations
- Failure details

---

## 22. Error Handling

Use Problem Details for API errors.

Error categories:

```text
Validation
Authentication
Authorization
Conflict
Configuration
DependencyUnavailable
Throttled
Timeout
ContextOverflow
RetrievalEmpty
EmbeddingFailure
GenerationFailure
StorageFailure
QueueFailure
OutputValidationFailure
InternalFailure
```

User-facing errors contain:

- Clear title
- Safe explanation
- Retry availability
- Recommended action
- Correlation ID

Raw Azure OpenAI responses and stack traces remain in restricted logs.

---

## 23. Observability

### 23.1 Correlation

A correlation ID flows through:

```text
Browser
→ API
→ Database job
→ SQS message
→ Worker
→ Azure OpenAI request
→ Output
→ SignalR event
```

### 23.2 Metrics

Track:

- API latency and error rate
- Queue depth and oldest-message age
- Job duration and failure rate
- Azure OpenAI request latency
- Throttling count
- Embedding batch count
- Chunk count per file
- Retrieval latency
- Empty retrieval rate
- Evidence count and token usage
- Run duration and failure rate
- Context utilization
- Invalid citation count
- Worker heartbeat age

### 23.3 Tracing

Agent-run trace spans:

```text
Authorize
Load agent snapshot
Calculate budget
Create query embedding
Semantic search
Keyword search
Rank fusion
Evidence assembly
Azure OpenAI generation
Markdown validation
Citation validation
Persist output
```

---

## 24. Health Checks

### Liveness

- API process is responsive.
- Worker process is responsive.

### Readiness

- PostgreSQL available
- Required schema available
- pgvector extension available
- LocalStack S3 available
- LocalStack SQS available
- Required worker heartbeat current

### External dependency health

- Azure OpenAI embedding deployment reachable
- Azure OpenAI generation deployment reachable

Deep checks should be user-initiated or scheduled sparingly to avoid unnecessary model usage.

---

## 25. Testing Strategy

### 25.1 Unit tests

- State transitions
- Authorization decisions
- Token budgeting
- Chunking
- Markdown hierarchy extraction
- Parameter validation
- Rank fusion
- Citation validation
- Retry classification

### 25.2 Integration tests

Use PostgreSQL and LocalStack containers and a fake Azure OpenAI HTTP service for deterministic tests.

Test:

- S3 upload
- SQS delivery
- Duplicate message handling
- pgvector persistence
- Application-scoped retrieval
- SignalR notification
- Run persistence

A small controlled test suite may call the real Azure OpenAI deployment separately.

### 25.3 Security tests

- Modified application ID
- Cross-application file access
- Cross-application vector search
- Cross-application citation access
- Unauthorized SignalR group request
- Viewer agent-edit attempt
- Disabled-user session reuse
- Script-bearing Markdown
- Prompt-injection text inside uploaded documents
- S3 key manipulation

### 25.4 End-to-end tests

1. Create application.
2. Assign admin and viewer.
3. Upload TXT and Markdown files.
4. Observe processing.
5. Create agent.
6. Execute agent.
7. Review Markdown.
8. Open citations.
9. Replace a source file.
10. Execute again.
11. Confirm the earlier run retains its snapshot.

---

## 26. Local Repository Structure

```text
/
├── src/
│   ├── frontend/
│   ├── api/
│   ├── document-worker/
│   ├── agent-worker/
│   └── shared/
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── security/
│   └── end-to-end/
├── deploy/
│   ├── localstack/
│   ├── postgres/
│   └── docker/
├── docs/
├── docker-compose.yml
├── docker-compose.override.yml
└── .env.example
```

---

## 27. Local Bootstrap Sequence

```text
1. Copy .env.example to .env.
2. Configure Azure OpenAI endpoint and deployment names.
3. Place credentials in local secure configuration.
4. Start PostgreSQL and LocalStack.
5. Apply database migrations.
6. Create S3 buckets and SQS queues.
7. Start API and workers.
8. Start the frontend.
9. Seed the initial Super Admin.
10. Run Azure OpenAI connectivity checks.
```

---

## 28. Delivery Plan

### Increment 1: Foundation

- Docker Compose
- React shell
- ASP.NET Core API
- PostgreSQL and pgvector
- Keycloak (OIDC realm, BFF integration)
- Four roles via Keycloak claims (platform realm roles + application-role attribute)
- Applications

### Increment 2: Upload and processing

- S3 upload
- SQS jobs
- Document worker
- TXT and Markdown parsing
- SignalR progress
- Processing dashboard

### Increment 3: Azure OpenAI embeddings and retrieval

- Azure authentication and client adapter
- Embedding batches
- pgvector storage
- Semantic retrieval
- Optional hybrid retrieval
- Application isolation tests

### Increment 4: Agents

- Agent CRUD
- Large prompt editor
- Deployment and parameter configuration
- Token budget validation
- File scope

### Increment 5: Execution and output

- Agent run queue
- Retrieval context
- Azure OpenAI generation
- Run snapshots
- Markdown preview
- Citations
- Output library

### Increment 6: Hardening

- Retries and dead-letter handling
- Worker leases
- Audit events
- Health dashboard
- Security tests
- Backup scripts
- Performance tests

---

## 29. Definition of Done

The solution is complete for the initial baseline when:

- All local services start through Docker Compose.
- Azure OpenAI connectivity is configurable and validated.
- Four roles are enforced by backend policies.
- Applications isolate files, vectors, agents, runs, and outputs.
- TXT and Markdown files process asynchronously.
- SignalR displays live processing and run status.
- Azure OpenAI creates embeddings.
- pgvector retrieves authorized evidence.
- Agents support large prompts and advanced settings.
- Azure OpenAI generates grounded Markdown.
- Runs preserve prompt, deployment, parameter, scope, and evidence snapshots.
- Markdown is safely rendered.
- Citations open authorized evidence.
- Failed jobs can be retried and diagnosed.
- File replacement cannot destroy working knowledge when replacement processing fails.
- Cross-application tests pass.
- No Ollama, Redis, general cache, or broad version-management subsystem is required.

---

## 30. References

- Microsoft Learn, Azure OpenAI client library for .NET: https://learn.microsoft.com/dotnet/api/overview/azure/ai.openai-readme
- Microsoft Learn, Azure OpenAI embeddings: https://learn.microsoft.com/azure/foundry/openai/how-to/embeddings
- Microsoft Learn, ASP.NET Core SignalR: https://learn.microsoft.com/aspnet/core/signalr/introduction
- LocalStack S3 documentation: https://docs.localstack.cloud/aws/services/s3/
- LocalStack SQS documentation: https://docs.localstack.cloud/aws/services/sqs/
- pgvector: https://github.com/pgvector/pgvector
