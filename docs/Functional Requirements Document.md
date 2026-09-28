# Functional Requirements Document

## Enterprise Knowledge Agent Platform

**Version:** 1.1  
**Status:** Implementation Baseline  
**Primary objective:** Deliver a working enterprise-oriented RAG application with a simple user experience and a locally runnable development environment.  
**Technology baseline:** React, ASP.NET Core on .NET 10, SignalR, PostgreSQL with pgvector, LocalStack S3 and SQS, Azure OpenAI, Docker Compose.

---

## 1. Purpose

The platform allows authorized users to create isolated applications, upload `.txt` and `.md` files, process those files into searchable knowledge, configure prompt-driven agents, and generate source-grounded Markdown output inside the web application.

The first implementation prioritizes the complete working path:

1. Create an application.
2. Assign application users.
3. Upload TXT or Markdown files.
4. Monitor processing progress.
5. Create an agent using a natural-language prompt.
6. Optionally configure generation and retrieval parameters.
7. Run the agent against the application's knowledge.
8. Review Markdown output and supporting sources.

The solution intentionally avoids broad platform features until this main path is stable.

---

## 2. Scope

### 2.1 Included

- Local user authentication
- Four fixed roles
- Application management and isolation
- TXT and Markdown upload
- Multiple-file upload
- File validation and duplicate detection
- Background processing through LocalStack SQS
- File storage through LocalStack S3
- Markdown-aware text chunking
- Azure OpenAI embeddings
- PostgreSQL vector storage through pgvector
- Semantic and optional hybrid retrieval
- Prompt-configured agents
- Large agent prompts
- Advanced generation parameters
- Advanced retrieval parameters
- Azure OpenAI chat generation
- Asynchronous agent execution
- SignalR progress updates
- Markdown preview, source, and split views
- Source citations
- Output history
- Processing and run dashboards
- Basic audit events
- Docker Compose setup

### 2.2 Excluded from the initial implementation

- Billing and subscriptions
- Cache or Redis
- Dedicated vector database
- Ollama or other local model runtime
- Multiple active LLM providers
- Automatic model fallback
- PDF, DOCX, image, audio, or video ingestion
- DOCX or PDF output
- External connectors such as Jira or SharePoint
- Visual Markdown template builder
- Multi-agent orchestration
- Tool execution
- Scheduled runs
- Public sharing
- Custom roles
- Complex content approval workflows
- Versioning on every entity

---

## 3. Roles and Authorization

The platform shall support exactly four roles.

### 3.1 Super Admin

A Super Admin can:

- Create, edit, archive, restore, and delete applications.
- Assign Super Viewer, Application Admin, and Application Viewer access (managed as Keycloak realm roles / user attributes; see §5).
- Access every application.
- Manage files, agents, runs, and outputs in every application.
- Retry failed processing and execution jobs.
- View system health, processing status, and audit activity.
- Configure Azure OpenAI deployment settings available to agents.

### 3.2 Super Viewer

A Super Viewer can:

- View all applications.
- View file-processing states.
- View agents and their non-secret configuration.
- View runs, outputs, dashboards, and audit events.

A Super Viewer cannot modify data, upload files, execute agents, retry jobs, or manage users.

### 3.3 Application Admin

Within assigned applications, an Application Admin can:

- View application details.
- Upload, replace, disable, reprocess, and delete files.
- Create, edit, enable, disable, and delete agents.
- Configure prompts and advanced parameters.
- Test and run agents.
- View all application runs and outputs.
- Edit generated Markdown.
- View application-level activity and failures.

### 3.4 Application Viewer

Within assigned applications, an Application Viewer can:

- View application details.
- View available knowledge files and processing status.
- Preview readable source content.
- View active agents.
- Run active agents when application viewer execution is enabled.
- View, copy, and download generated Markdown.

An Application Viewer cannot change knowledge or agent configuration.

### 3.5 Enforcement

Authorization shall be enforced by the backend for every API, file, agent, run, output, citation, retrieval query, and SignalR subscription. Frontend visibility is not an authorization control.

---

## 4. Application Requirements

### FR-APP-001: Create application

A Super Admin shall create an application using:

- Name
- Description
- Active or archived status
- Whether Application Viewers may run active agents

### FR-APP-002: Application isolation

All application-owned data shall include an application identifier. Retrieval, file access, runs, outputs, and SignalR events shall be constrained to the authorized application.

### FR-APP-003: Archive application

Archiving an application shall make it read-only and prevent uploads, agent changes, and new agent runs.

### FR-APP-004: Application dashboard

The application dashboard shall show:

- Total files
- Ready files
- Queued files
- Processing files
- Failed files
- Active agents
- Running agent executions
- Failed executions
- Generated outputs
- Recent activity
- Items requiring attention

---

## 5. User Management Requirements

User accounts and credentials are provisioned and managed in Keycloak (the platform's OpenID Connect identity provider), not in this application. There is no in-app screen or API to create, disable, or list users — this keeps the application focused on the knowledge/agent/output workflow rather than identity administration.

### FR-USR-001: Roles are claims-derived

A user's platform role (Super Admin or Super Viewer) and application-scoped role (Application Admin or Application Viewer, per application) shall be determined entirely from claims issued by Keycloak at sign-in: platform roles from the user's Keycloak realm roles, and application-scoped roles from a Keycloak user attribute listing `{applicationId}:{role}` pairs. The application shall not maintain its own copy of this assignment.

### FR-USR-002: No local account lifecycle

Creating, disabling, or re-enabling a user account is out of scope for this application. Account lifecycle (including any password reset or lockout) is handled by Keycloak's own administration tooling.

---

## 6. Knowledge File Requirements

### FR-KNW-001: Supported formats

The system shall accept `.txt` and `.md` files.

### FR-KNW-002: Multiple-file upload

An Application Admin shall upload multiple files in one operation. Each file shall be independently validated and processed.

### FR-KNW-003: File validation

The system shall validate:

- Extension
- Configured maximum size
- Non-empty content
- Readable text
- Supported character encoding
- Absence of obvious binary content
- Safe filename
- Application permission

### FR-KNW-004: File storage

Accepted files shall be stored in a private LocalStack S3 bucket using a server-generated key.

### FR-KNW-005: Duplicate detection

The system shall calculate a content hash. An identical active file within the same application shall produce a warning and shall not be duplicated by default.

### FR-KNW-006: File replacement

The initial system shall keep replacement simple:

1. Upload replacement content.
2. Process the replacement separately.
3. Keep the current searchable content available until replacement processing succeeds.
4. On success, replace the active chunks and vectors.
5. On failure, retain the existing searchable content.

The platform does not expose general file-version browsing in the initial release.

### FR-KNW-007: Disable file

An Application Admin shall be able to disable a file. Disabled files remain visible to administrators but are excluded from retrieval.

### FR-KNW-008: Delete file

Deleting a file shall remove it from future retrieval and start asynchronous cleanup of stored content and vectors. Historical outputs remain, but their source may be shown as unavailable.

### FR-KNW-009: File list

The file list shall support search, status filtering, and the following information:

- File name
- Type
- Size
- Processing status
- Current stage
- Progress
- Uploaded by
- Uploaded time
- Last processed time

---

## 7. Processing Requirements

### FR-PRC-001: Background processing

After file upload, the API shall create a processing job and send an identifier-based message to LocalStack SQS. File content shall not be placed in the queue message.

### FR-PRC-002: Processing stages

Internal stages shall include:

1. Queued
2. Downloading
3. Validating
4. Parsing
5. Chunking
6. Generating embeddings
7. Saving vectors
8. Finalizing
9. Completed

User-facing states shall be Queued, Processing, Ready, Failed, Disabled, and Deleting.

### FR-PRC-003: Meaningful progress

Progress shall be based on completed stages and embedding-batch counts. The application shall not show fake continuously increasing progress.

### FR-PRC-004: Markdown-aware chunking

Markdown parsing shall preserve headings, paragraphs, lists, tables, block quotes, and code fences. Chunks shall include heading path and source offsets.

### FR-PRC-005: Embeddings

The worker shall use the configured Azure OpenAI embedding deployment. It shall record the deployment name, vector dimensions, and embedding configuration used by the active knowledge index.

### FR-PRC-006: Idempotency

Repeated delivery of the same processing message shall not create duplicate active chunks or vectors.

### FR-PRC-007: Retry

Temporary dependency or network failures may be retried automatically. Invalid files and incompatible configuration errors require user or administrator action.

### FR-PRC-008: Failure details

A failed job shall show:

- Failed stage
- Safe error summary
- Attempt count
- Last attempt time
- Retry eligibility
- Recommended action
- Correlation ID

---

## 8. Processing Dashboard Requirements

### FR-DSH-001: Platform dashboard

Super Admin and Super Viewer shall see processing across all applications, including queued, running, completed, and failed jobs.

### FR-DSH-002: Application processing page

Assigned users shall see processing for their application with Active, Queued, Completed, Failed, and All views.

### FR-DSH-003: Live updates

SignalR shall update job stages, progress, completion, failure, and dashboard counters without requiring a manual page refresh.

### FR-DSH-004: Reconnection

If SignalR disconnects, the UI shall show a connection indicator, reconnect automatically, and refresh current data through REST after reconnection.

---

## 9. Agent Requirements

### FR-AGT-001: Create agent

An Application Admin shall create an agent with:

- Name
- Description
- Prompt
- Active or disabled status
- Knowledge scope
- Azure OpenAI model deployment
- Generation parameters
- Retrieval parameters

### FR-AGT-002: Prompt-defined output

The agent prompt shall define the desired Markdown structure and behavior. No separate template engine is required.

### FR-AGT-003: Large prompts

The prompt editor shall support large prompts and display estimated token use against the selected model context limit. The platform shall not silently truncate the agent prompt.

### FR-AGT-004: Knowledge scope

An agent shall use either:

- All ready and enabled files in the application
- A selected set of ready and enabled files

### FR-AGT-005: Simple changes without general versioning

Editing an agent shall update its current configuration. The initial release shall not provide full agent version management.

For traceability, every agent run shall store a snapshot of the agent prompt, selected deployment, generation parameters, retrieval parameters, and knowledge scope used for that run.

### FR-AGT-006: Prompt visibility

Super Admin and Application Admin can view full prompts. Application Viewers do not see full prompts by default.

---

## 10. Generation and Retrieval Parameters

### FR-PAR-001: Initial generation parameters

The system shall support:

- Temperature
- Maximum output tokens
- Top P when supported by the selected Azure OpenAI deployment

### FR-PAR-002: Initial retrieval parameters

The system shall support:

- Semantic or hybrid retrieval mode
- Retrieved chunk count
- Minimum similarity threshold
- Maximum evidence context tokens
- Neighboring-chunk inclusion
- Strict grounding
- Citations required

### FR-PAR-003: Presets

The UI shall provide Precise, Balanced, Creative, and Custom presets while allowing numeric adjustment.

### FR-PAR-004: Capability validation

The backend shall validate selected settings against a model-deployment capability configuration. Unsupported values shall be rejected or explicitly adjusted and recorded. They shall not be silently ignored.

---

## 11. Azure OpenAI Requirements

### FR-AOI-001: Deployment configuration

The platform shall support separate Azure OpenAI deployment settings for:

- Chat or text generation
- Embeddings

Configuration shall include endpoint, deployment names, authentication mode, and known context/output limits.

### FR-AOI-002: Authentication

The application shall support Azure OpenAI credentials through secure server configuration. Microsoft Entra ID shall be preferred where available. API keys may be supported for local development and shall never be returned to the frontend.

### FR-AOI-003: Provider abstraction

Application services shall call internal generation and embedding interfaces rather than Azure OpenAI directly. This keeps future provider support possible without adding multi-provider complexity now.

### FR-AOI-004: Availability

The operations view shall report whether configured embedding and generation deployments are reachable.

### FR-AOI-005: Rate and quota handling

Azure OpenAI throttling and temporary failures shall be classified as retryable according to configured retry limits. The UI shall show a clear failure if retries are exhausted.

---

## 12. Retrieval Requirements

### FR-RET-001: Application-scoped search

All semantic and keyword searches shall constrain candidates to the authorized application before ranking.

### FR-RET-002: Ready and enabled knowledge

Only successfully processed, enabled files shall be searchable.

### FR-RET-003: Semantic retrieval

The system shall embed the retrieval query using the same embedding deployment/configuration used to create stored vectors.

### FR-RET-004: Hybrid retrieval

Hybrid mode shall combine vector similarity with PostgreSQL full-text relevance using normalized rank fusion.

### FR-RET-005: Duplicate suppression

The retrieval service shall reduce duplicate, heavily overlapping, and repeated passages before model context assembly.

### FR-RET-006: Context budgeting

The server shall calculate available evidence capacity after accounting for platform instructions, agent prompt, user request, protocol overhead, and reserved output tokens.

### FR-RET-007: No evidence

In strict-grounding mode, if no evidence meets the configured threshold, the platform shall return a controlled no-information result rather than ask the model to invent content.

### FR-RET-008: Partial readiness

An agent may run with currently ready files while others are processing. The UI shall disclose which files were excluded.

---

## 13. Agent Run Requirements

### FR-RUN-001: Submit run

An authorized user shall select an active agent, enter a request, and optionally provide an output title.

### FR-RUN-002: Asynchronous execution

The API shall create a durable run record and send an identifier-based message to the agent-execution SQS queue. Navigating away shall not cancel the run.

### FR-RUN-003: Run stages

Run stages shall include:

1. Queued
2. Preparing
3. Retrieving
4. Building context
5. Generating
6. Validating
7. Saving
8. Completed

Terminal alternatives are Failed and Cancelled.

### FR-RUN-004: Run snapshot

Every run shall store:

- Agent name and prompt snapshot
- User request
- Azure OpenAI deployment used
- Requested and effective parameters
- Retrieval settings
- Files and chunks used
- Start and completion times
- Failure details when applicable

### FR-RUN-005: Duplicate submission

The API shall use an idempotency key to prevent duplicate runs caused by repeated clicks or client retries.

### FR-RUN-006: Prompt assembly

The model request shall distinguish platform instructions, agent instructions, untrusted retrieved evidence, and the user's request.

### FR-RUN-007: Progress

SignalR shall publish meaningful run-stage changes and completion or failure events.

---

## 14. Markdown Output Requirements

### FR-OUT-001: Views

The output page shall support Preview, Markdown Source, and Split views.

### FR-OUT-002: Secure rendering

Generated Markdown shall be treated as untrusted. Raw HTML shall be disabled by default and rendered content shall be sanitized.

### FR-OUT-003: Stored output

A completed output shall contain:

- Title
- Original generated Markdown
- Current Markdown
- Run reference
- Agent snapshot
- Azure OpenAI and parameter snapshot
- Citations
- Created by and time
- Edited status and time

### FR-OUT-004: Edit output

Application Admins may edit generated Markdown. The initial generated text shall be retained within the run record.

### FR-OUT-005: Output actions

Application Admins can edit, copy, download `.md`, regenerate, and delete. Application Viewers can view, copy, and download.

### FR-OUT-006: Output library

The output library shall support searching and filtering by title, agent, creator, date, and run status.

---

## 15. Citation Requirements

### FR-CIT-001: Structural citations

Citations shall be stored as records connected to retrieved chunks rather than relying only on model-generated filenames.

### FR-CIT-002: Citation validation

A valid citation must reference evidence supplied to the model for the current run and belonging to the same application.

### FR-CIT-003: Evidence viewer

Selecting a citation shall show filename, heading path, source passage, and surrounding text.

### FR-CIT-004: Deleted source

If the source is deleted, historical output remains visible but the source may be shown as unavailable according to retention policy.

---

## 16. Security Requirements

### FR-SEC-001: Prompt injection

Uploaded content shall be treated as untrusted data. Instructions inside documents shall not override platform or agent instructions.

### FR-SEC-002: Secrets

Azure endpoints and credentials shall remain server-side. Secrets shall not appear in prompts, queue messages, SignalR events, audit summaries, or frontend responses.

### FR-SEC-003: File access

S3 object keys shall be server-generated. File reads and downloads require application authorization.

### FR-SEC-004: Cross-application protection

Database queries, vector searches, S3 access, run execution, outputs, citations, and SignalR groups shall all enforce application boundaries.

### FR-SEC-005: Secure authentication

Authentication shall be delegated to Keycloak via OpenID Connect using the Backend-for-Frontend (BFF) pattern: the API performs the sign-in handshake server-side and issues its own protected, HTTP-only session cookie to the browser; access/ID tokens shall never reach browser JavaScript. Credential hashing, lockout, and password policy are owned by Keycloak. CSRF controls are required for state-changing requests.

---

## 17. Audit Requirements

The system shall record important events including:

- Login success and failure
- User and role changes
- Application creation, archive, and deletion
- File upload, replacement, disabling, deletion, and retry
- Agent creation, modification, enablement, and deletion
- Agent execution
- Output edit and deletion
- Azure OpenAI deployment-configuration changes

Audit records shall contain a timestamp, user, action, application, entity, outcome, and correlation ID without storing full source documents or secrets.

---

## 18. Health and Operations Requirements

The operations page shall report health for:

- API
- PostgreSQL
- pgvector extension
- LocalStack S3
- LocalStack SQS
- Azure OpenAI embedding deployment
- Azure OpenAI generation deployment
- Document worker
- Agent worker
- SignalR

Workers shall report periodic heartbeats. The dashboard shall identify stale workers, failed jobs, and dead-letter queue messages.

---

## 19. Local Deployment Requirements

Docker Compose shall run:

- React frontend
- ASP.NET Core API
- Document-processing worker
- Agent-execution worker
- PostgreSQL with pgvector
- Keycloak, with its own dedicated Postgres database
- LocalStack

Azure OpenAI remains a remote dependency and is configured through environment variables or secured local secrets.

The local setup shall include scripts that:

- Start containers
- Apply database migrations
- Create S3 buckets
- Create SQS queues and dead-letter queues
- Import the bootstrap Keycloak realm (including the initial Super Admin)
- Validate Azure OpenAI connectivity

---

## 20. Acceptance Criteria

The baseline is complete when:

1. Docker Compose starts all local components, including Keycloak.
2. The bootstrap Super Admin (imported into Keycloak) can log in via SSO.
3. The Super Admin can create an application and assign application roles in Keycloak.
4. An Application Admin can upload multiple TXT and Markdown files.
5. Files are stored in LocalStack S3.
6. Jobs pass through LocalStack SQS.
7. The document worker generates Azure OpenAI embeddings.
8. Chunks and vectors are stored in PostgreSQL with pgvector.
9. SignalR shows processing progress.
10. An Application Admin can create an agent with a large prompt and advanced settings.
11. An Application Viewer can run an enabled agent when permitted.
12. Retrieval only uses authorized application content.
13. Azure OpenAI generates Markdown grounded in retrieved evidence.
14. The output is safely rendered and citations can be inspected.
15. Failed processing and generation jobs can be investigated and retried.
16. Every run preserves its effective prompt, model, parameter, and evidence snapshot.
17. Cross-application access tests fail.
18. Restarting API, frontend, or workers does not lose persisted state.
19. No Ollama, Redis, or cache dependency exists.

---

## 21. References

- Microsoft Learn, Azure OpenAI client library for .NET: https://learn.microsoft.com/dotnet/api/overview/azure/ai.openai-readme
- Microsoft Learn, Azure OpenAI embeddings: https://learn.microsoft.com/azure/foundry/openai/how-to/embeddings
- Microsoft Learn, ASP.NET Core SignalR: https://learn.microsoft.com/aspnet/core/signalr/introduction
- LocalStack S3 documentation: https://docs.localstack.cloud/aws/services/s3/
- LocalStack SQS documentation: https://docs.localstack.cloud/aws/services/sqs/
- pgvector: https://github.com/pgvector/pgvector
