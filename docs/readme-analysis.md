# README Analysis

Working notes that back the top-level [README.md](../README.md). This document
records what was verified from the repository, which sources were trusted when
they disagreed, and what could not be confirmed. It is a maintainer artifact,
not user-facing documentation.

Verification date: 2026-08-18. Source-of-truth priority used throughout:
working implementation → executable config/scripts → automated tests →
requirements/architecture docs → existing docs → labeled assumptions.

---

## Repository Summary

Enterprise Knowledge Agent is a locally runnable, containerized
retrieval-augmented generation (RAG) platform. Users create isolated
"applications", upload `.txt`/`.md` files, have them chunked and embedded into a
`pgvector` store, configure prompt-driven "agents", run those agents (or chat)
against the knowledge, and review source-grounded Markdown output with citations.

The stack is a .NET 10 backend (ASP.NET Core minimal API + two background
workers), a React 19 + Vite SPA, PostgreSQL with pgvector, LocalStack (S3 + SQS),
and Azure OpenAI for embeddings and chat. Everything runs via Docker Compose.

The repository currently has **no git commits** (`git log` reports the branch has
no commits yet), so there is no revision history to mine and no rollback safety
net — a fact worth noting for maintainers.

---

## Project Purpose

Provide a working end-to-end RAG path with a simple UX and a fully local
development environment:

1. Create an application (isolation boundary).
2. Upload TXT/Markdown knowledge files.
3. Watch background processing (parse → chunk → embed → index).
4. Configure a prompt-driven agent with generation/retrieval parameters.
5. Run the agent asynchronously, or chat against the knowledge.
6. Review grounded Markdown output plus the evidence/citations behind it.

Source: [docs/Functional Requirements Document.md](Functional%20Requirements%20Document.md)
§1, cross-checked against the implemented API and UI.

---

## Intended Audiences

| Audience | Needs | Read first |
| --- | --- | --- |
| Evaluator / stakeholder | What it does, current maturity, what works today | Overview, Key Features, Screenshots |
| End user | How to create an app, upload knowledge, run agents, chat | Usage Guide |
| New developer | Prereqs, quick start, repo structure, how to run/test | Prerequisites, Quick Start, Repository Structure, Testing |
| Experienced developer | Architecture, data model, background processing, API | Architecture, Data Model, Background Processing, API |
| Contributor | Local checks that mirror CI, workflow | Development Workflow, Testing |
| Administrator / operator | Health checks, queues, workers, reset/backup | Health & Operations, Troubleshooting, Backup & Restore |
| Tester | Unit/integration test commands and prerequisites | Testing |
| Security reviewer | Auth status, secret handling, headers/CSP, isolation | Security |

---

## Primary Workflows

- **Application lifecycle**: create → update → archive → restore → delete.
- **Knowledge ingestion**: upload (multipart) → background parse/chunk/embed →
  Ready/Failed → disable/enable/reprocess/replace/delete.
- **Agent authoring**: create/edit prompt + generation + retrieval settings →
  enable/disable.
- **Agent execution**: submit run (config snapshotted) → Retrieving → Generating
  → Saving → Completed/Failed → output + citations; retry eligible on failure.
- **Chat**: create conversation (settings snapshotted) → send message →
  server-sent-events token stream → grounded answer with evidence.
- **Observability**: platform/application dashboards, processing queue, agent
  runs, audit trail, worker/queue operations — kept live via SignalR.

---

## Verified Features (Implemented)

Confirmed from source and/or the running app at `http://localhost:8080`.

- Application CRUD + archive/restore/delete (`src/api/ApplicationsEndpoints.cs`).
- Knowledge file upload/list/preview/disable/enable/reprocess/replace/delete
  (`src/api/KnowledgeFilesEndpoints.cs`), 10 MiB limit (`KnowledgeFiles:MaxFileSizeBytes`).
- Document processing pipeline: Markdig-based parse → token-aware chunk
  (target 700 / max 1100 / overlap 100) → Azure OpenAI embeddings → pgvector
  persist (`src/document-worker`, `src/application/Processing`, `src/application/Chunking`).
- Agent CRUD + enable/disable (`src/api/AgentsEndpoints.cs`).
- Asynchronous agent runs with config snapshotting, stages, progress, cancel,
  retry (`src/api/RunsEndpoints.cs`, `src/agent-worker`).
- Generated outputs: list/detail/edit (markdown)/regenerate/delete + citations
  (`src/api/OutputsEndpoints.cs`).
- Chat conversations with SSE token streaming and evidence
  (`src/api/ChatEndpoints.cs`).
- Retrieval: semantic + hybrid over pgvector + full-text
  (`src/application` retrieval services, migration `AddKnowledgeChunkVectorAndFts`).
- Citations/evidence snapshotting and `[SOURCE-NNN]` validation.
- Platform + application dashboards (`src/api/DashboardsEndpoints.cs`).
- Processing-job and audit queries (`src/api/ProcessingEndpoints.cs`, `AuditEndpoints.cs`).
- Operations status: worker heartbeats + queue depths (`src/api/OperationsEndpoints.cs`).
- Realtime: SignalR hub `/hubs/realtime` + internal worker relay
  `/internal/realtime/publish` (`src/api/RealtimeEndpoints.cs`, `Program.cs`).
- Transactional outbox + job reconciler + lease reaper hosted services (`Program.cs`).
- Health checks `/health/live`, `/health/ready` (`Program.cs`).
- Security headers + strict CSP (API `Program.cs`, frontend `nginx.conf`).
- React SPA with 18 routes, dark/light theme, command palette, Monaco editor,
  data tables, live updates (`src/frontend`).
- 15 EF Core migrations present (`src/infrastructure/Persistence/Migrations`).
- CI: backend build/test/format, frontend lint/typecheck/format/test/build,
  Docker image builds, gitleaks secret scan (`.github/workflows/ci.yml`).

---

## Partially Implemented Features

- **Authorization model (Partial)**: A 13-flag capability model exists in the
  frontend (`src/frontend/src/app/authz/capabilities.ts`) and the API enforces
  application-scoped isolation and read-only-state guards, but there is **no
  authentication**. `useCapabilities` is a stub returning `FULL_CAPABILITIES`,
  and every API call runs as a single hardcoded actor `local-user`. Roles are
  defined conceptually but not enforced by a login/identity system.
- **`allowViewerExecution` flag (Partial)**: Stored per application and surfaced
  in the UI, but with no authenticated roles it does not gate anything at runtime.
- **`deploy/postgres/backup|restore` scripts (Partial/unverified)**: Present as
  `.ps1`/`.sh` pairs but not exercised during this analysis (see Unverified).

---

## Planned Features

Described in requirements/architecture/tasks docs but **not** in the current build:

- Keycloak-based authentication, four fixed roles, seeded bootstrap Super Admin
  (superseded by decision D20 — Keycloak removed; single `local-user` actor).
- PDF/DOCX/image/audio/video ingestion; DOCX/PDF export.
- Multiple LLM providers / automatic model fallback; Redis/cache; dedicated
  vector database; billing.
- `rag-generated-exports` S3 bucket is provisioned by LocalStack init but is not
  yet used by an implemented export feature.

---

## Technology Stack

| Layer | Technology | Responsibility |
| --- | --- | --- |
| Frontend | React 19, Vite 8, TypeScript 6, Tailwind v4, Radix/shadcn, TanStack Query/Table, Monaco, `@microsoft/signalr` | SPA UI, live updates, editor |
| API | ASP.NET Core (.NET 10) minimal APIs | HTTP endpoints, SignalR hub, hosted services |
| Workers | .NET 10 `BackgroundService` (document-worker, agent-worker) | SQS consumers: ingestion + agent execution |
| Data | PostgreSQL 16 + pgvector, EF Core 10 + Npgsql | Relational + vector storage, migrations |
| Object store | LocalStack S3 (AWSSDK.S3) | Source file storage |
| Messaging | LocalStack SQS (AWSSDK.SQS) | Async jobs + DLQs |
| AI | Azure OpenAI (Azure.AI.OpenAI, Azure.Identity) | Chat + embeddings |
| Observability | Serilog, OpenTelemetry (console exporters) | Structured logs, metrics, traces |
| Orchestration | Docker Compose v2 | Local multi-service runtime |
| CI | GitHub Actions | Build/test/lint/format/image build/secret scan |

---

## Architecture Summary

Layered backend (domain ← application ← infrastructure ← api/workers) plus an
independent SPA. Requests hit the API; mutations write domain rows and an outbox
message in one transaction; the outbox publisher pushes SQS messages; workers
consume them, run the pipeline, and relay progress back to the API's internal
endpoint, which fans out over SignalR to the SPA. Postgres (pgvector) is the
system of record; S3 stores raw files; Azure OpenAI provides embeddings/chat.

---

## Repository Entry Points

- API host: `src/api/Program.cs` (image `eka-api:local`).
- Document worker: `src/document-worker/Program.cs` (`eka-document-worker:local`).
- Agent worker: `src/agent-worker/Program.cs` (`eka-agent-worker:local`).
- Frontend: `src/frontend` (Vite dev `main.tsx`; prod nginx, image `eka-frontend:local`).
- Solution: `EnterpriseKnowledgeAgent.slnx`.
- Compose: `docker-compose.yml` (+ `docker-compose.override.yml` diagnostics).

---

## Main Modules

- `src/domain` — entities/aggregates/value objects/enums.
- `src/application` — use cases, DTOs, orchestrators (Processing, Chunking,
  Execution, Generation, Retrieval, Outputs, Chat, Jobs, Audit, Dashboards).
- `src/infrastructure` — EF Core DbContext + migrations, repositories, S3/SQS
  clients, Azure OpenAI clients, realtime publishers.
- `src/shared` — cross-process contracts (message DTOs, correlation id, enums).
- `src/api` — endpoint groups, health checks, hosted services, SignalR hub.
- `src/frontend` — SPA (api client, routes, pages, realtime, theme, design system).

---

## Configuration Sources

- `.env` (copied from `.env.example`) — auto-loaded by Docker Compose.
- `appsettings.json` / `appsettings.Development.json` per backend project.
- `dotnet user-secrets` — real Azure OpenAI key (never in `.env`).
- Compose env blocks map `.env` values to `Section__Key` config keys.
- Frontend build-time: `VITE_API_BASE_URL`; dev-time: `VITE_DEV_API_PROXY_TARGET`.

---

## Environment Variables

Names only (no secret values). Full table with safe examples lives in the README.

Compose/topology: `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`,
`API_HTTP_PORT`, `FRONTEND_HTTP_PORT`, `POSTGRES_HOST_PORT`,
`LOCALSTACK_HOST_PORT`, `VITE_API_BASE_URL`.

Postgres: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`.

LocalStack/AWS: `LOCALSTACK_DEBUG`, `LOCALSTACK_ENDPOINT`, `AWS_REGION`,
`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `S3_SOURCE_FILES_BUCKET`,
`S3_GENERATED_EXPORTS_BUCKET`, `SQS_DOCUMENT_PROCESSING_QUEUE`,
`SQS_AGENT_EXECUTION_QUEUE`, `SQS_DELETION_JOBS_QUEUE`.

Azure OpenAI: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_AUTH_MODE`,
`AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_CHAT_DEPLOYMENT`,
`AZURE_OPENAI_EMBEDDING_DEPLOYMENT`, `AZURE_OPENAI_EMBEDDING_DIMENSIONS`,
`AZURE_OPENAI_MAX_CONTEXT_TOKENS`, `AZURE_OPENAI_MAX_OUTPUT_TOKENS`,
`AZURE_OPENAI_REQUEST_TIMEOUT_SECONDS`, `AZURE_OPENAI_MAX_RETRIES`,
`AZURE_OPENAI_CHAT_SUPPORTS_TEMPERATURE`, `AZURE_OPENAI_CHAT_SUPPORTS_MAX_TOKENS`,
`AZURE_OPENAI_RESOURCE_NAME`, `AZURE_OPENAI_RESOURCE_GROUP`.

Realtime: `REALTIME_API_BASE_URL`, `REALTIME_INTERNAL_TOKEN`.

Embedding/chunking (worker): `EMBEDDING_MAX_INPUTS_PER_BATCH`,
`EMBEDDING_MAX_TOKENS_PER_BATCH`, `EMBEDDING_MAX_CONCURRENT_BATCHES`.

---

## Verified Commands

Validated by executing them or by direct inspection of the scripts/manifests.

- `docker compose up -d --build` / `docker compose ps` / `docker compose logs -f <svc>`
- `docker compose --profile diagnostics up -d`
- `docker compose down` / `docker compose down -v`
- `./deploy/postgres/migrate.ps1` / `.sh`
- `./deploy/scripts/check-aoai-connectivity.ps1` / `.sh`
- Backend: `dotnet build EnterpriseKnowledgeAgent.slnx`,
  `dotnet test EnterpriseKnowledgeAgent.slnx`,
  `dotnet format EnterpriseKnowledgeAgent.slnx --verify-no-changes` (all in CI).
- Frontend (`src/frontend`): `npm ci`, `npm run dev`, `npm run build`,
  `npm run lint`, `npm run typecheck`, `npm run format:check`, `npm test`
  (all in CI; scripts confirmed in `package.json`).

During this analysis, `npm run dev` was run successfully (Vite on 5173) and the
running compose stack answered `/api/applications` and dashboard endpoints.

---

## Services and Ports

| Service | Image | Host port (default) | Container port | Notes |
| --- | --- | --- | --- | --- |
| frontend | `eka-frontend:local` | `FRONTEND_HTTP_PORT` = 8080 | 8080 | nginx; proxies `/api`, `/hubs`, `/health` |
| api | `eka-api:local` | `API_HTTP_PORT` = 8081 | 8080 | minimal API + SignalR |
| document-worker | `eka-document-worker:local` | none | — | SQS consumer |
| agent-worker | `eka-agent-worker:local` | none | — | SQS consumer |
| postgres | `pgvector/pgvector:pg16` | 5432 (diagnostics only) | 5432 | named volume `eka-postgres-data` |
| localstack | `localstack/localstack:3` | 4566 (diagnostics only) | 4566 | named volume `eka-localstack-data` |
| postgres-diagnostics | `alpine:3.20` | `POSTGRES_HOST_PORT` = 5432 | — | socat sidecar, `diagnostics` profile |
| localstack-diagnostics | `alpine:3.20` | `LOCALSTACK_HOST_PORT` = 4566 | — | socat sidecar, `diagnostics` profile |

App URL: `http://localhost:8080`. API direct: `http://localhost:8081`.

---

## Authentication and Authorization

- **No authentication.** No login route in the SPA; API endpoints are open.
- **Single actor**: all writes/audit attributed to `local-user`.
- **Capabilities**: `useCapabilities` stub returns all 13 flags true.
- **Enforced at API**: application-scoped isolation (cross-app access → 404) and
  read-only-state guards (archived/deleted apps reject mutations).
- Conflict vs requirements docs (Keycloak + 4 roles): see Documentation Conflicts.

---

## Data Storage

- **PostgreSQL 16 + pgvector**, 7 schemas: `platform`, `knowledge`, `agents`,
  `execution`, `outputs`, `operations`, `audit`.
- **EF Core 10** with 15 migrations; embeddings stored as `vector(1536)` with a
  full-text index for hybrid search.
- **S3 (LocalStack)** bucket `rag-source-files` for raw uploads; object keys are
  server-generated. `rag-generated-exports` provisioned but unused.
- Migrations are **not** auto-applied at startup; run the migrate script (uses
  the `diagnostics` profile to reach Postgres from the host).

---

## External Dependencies

- **Azure OpenAI** (real external service): chat + embedding deployments. Entra
  ID auth (preferred) or API key. Placeholder defaults let the stack boot, but
  ingestion/generation fail at call time without a real endpoint/deployments.
- **Docker Desktop / Compose v2** required for the stack and for integration
  tests (Testcontainers spins up a real Postgres).
- **Azure CLI (`az`)** only for the optional AOAI connectivity check.

---

## Background Processing

- **Outbox pattern**: domain write + outbox row in one transaction;
  `OutboxPublisherService` (API) polls and publishes to SQS.
- **Queues**: `document-processing`, `agent-execution`, `deletion-jobs`, each
  with a `-dlq` and redrive after `MaxReceiveCount` (default 5).
- **Workers**: claim/lease/heartbeat idempotency; visibility timeout 60s,
  heartbeat ~20s. Document worker: parse→chunk→embed→persist. Agent worker:
  retrieve→generate→cite→save.
- **Safety nets**: `JobReconcilerService` re-drives stuck queued work;
  `LeaseReaperService` recovers leases from crashed workers.
- **Progress**: workers relay to the API internal endpoint (shared-secret
  header), which broadcasts over SignalR.

---

## Tests

- Backend unit test projects: `Api.Tests`, `Application.Tests`,
  `Architecture.Tests`, `Domain.Tests`, `Shared.Tests` (under `tests/unit`).
- Backend integration: `Infrastructure.IntegrationTests` (Testcontainers
  Postgres + a LocalStack fixture) under `tests/integration` — **requires Docker**.
- `tests/security` and `tests/end-to-end` directories exist but are **empty**.
- Frontend: 41 Vitest + Testing Library test files under `src/frontend/src/test`.
- All wired into CI (`.github/workflows/ci.yml`).

---

## Build and Deployment

- Docker images built by Compose or CI matrix (api, agent-worker,
  document-worker, frontend).
- No production deployment manifests (no k8s/helm/Terraform). The only supported,
  verified path is local Docker Compose. Production hardening (real secrets,
  auth, TLS) is out of scope for the current repo.

---

## Existing Documentation

- [docs/Architecture and Solution Design Document.md](Architecture%20and%20Solution%20Design%20Document.md)
- [docs/Functional Requirements Document.md](Functional%20Requirements%20Document.md)
- [docs/tasks.md](tasks.md) — task/backlog log (contains superseded decisions).
- `deploy/localstack/init/README.md`, `deploy/postgres/*` script headers.
- Prior top-level `README.md` (now replaced; it described an earlier P0 state).

---

## Existing Screenshots

- Prior QA PNGs in the repo root were removed in an earlier cleanup and are not
  reusable. No screenshots existed under `docs/` before this task. All README
  screenshots in this task were freshly captured from the running app.

---

## Existing Diagrams

- No committed diagram sources existed before this task. Architecture diagrams
  are authored as Mermaid embedded in the README; editable copies are mirrored
  under `docs/diagrams/`.

---

## Documentation Conflicts

1. **Old README maturity vs reality.** The previous README described "Phase P0":
   no EF Core `DbContext`, migrate script a no-op, login not usable. Reality:
   `Microsoft.EntityFrameworkCore.Design` is referenced, 15 migrations exist, the
   DB has data, and the app is fully usable without login. **Followed the working
   implementation.** README rewritten accordingly.

2. **Requirements (Keycloak auth + 4 roles + seeded Super Admin) vs reality.**
   `docs/Functional Requirements Document.md` and `docs/tasks.md` describe
   Keycloak authentication and role seeding. The implementation removed Keycloak
   (decision D20) and has no authentication; a single `local-user` acts for all
   calls. **Followed the working implementation.** README documents "no auth" and
   labels roles as Planned, with a user-facing note in Security.

3. **`migrate.ps1` inline comment vs behavior.** The script's NOTES comment says
   "until TASK-PG-01 adds the DbContext… step 3 has nothing to apply." That
   comment is stale, but the script's runtime check keys off the EF Design
   reference (now present), so it will actually apply migrations. **Followed the
   executable script behavior.** README documents the script as applying migrations.

4. **Knowledge API path.** The UI route is `/applications/:id/knowledge`, but the
   HTTP endpoint group is `/api/applications/{applicationId}/files`. **Followed the
   code**; README uses `/files` for the API and `/knowledge` for the UI route.

5. **No seed/demo-data provisioning.** Docs reference "seeded Super Admin". No
   application seed script exists; the demo data in the running container was
   created manually via the UI. README does not claim seeded data; it explains a
   fresh stack starts empty and users create the first application in the UI.

---

## Missing Information

- No `LICENSE`, `CONTRIBUTING.md`, `SECURITY.md`, or `CODE_OF_CONDUCT.md` files.
  README links to CI-derived contribution steps and marks license as pending.
- No documented production deployment target.
- `backup.ps1`/`restore.ps1` behavior not executed here (documented as Partial).

---

## Unverified Information

- Exact behavior of `deploy/postgres/backup.*` and `restore.*` (not run).
- Azure OpenAI live behavior (no real endpoint configured in this environment;
  runs that need embeddings/generation depend on it).
- Performance/scalability numbers — none measured; README makes no claims.

---

## Screenshot Requirements

Captured from the running app at 1440×1000 (see
[docs/screenshots/README.md](screenshots/README.md) for the full inventory and
status). Planned set: platform dashboard, applications list, operations, agent
detail (Monaco), run detail (evidence), output detail (citations), chat thread,
knowledge list. All use the existing "Test" application demo data (no secrets;
actor is `local-user`).

---

## Diagram Requirements

- **System context** (users ↔ SPA ↔ API ↔ Postgres/S3/SQS/Azure OpenAI) — Mermaid.
- **Component/service topology** (compose services + volumes) — Mermaid.
- **Ingestion processing flow** (upload → outbox → SQS → document-worker → pgvector) — Mermaid.
- **Agent run / request flow** (submit → worker retrieve/generate/cite → SignalR) — Mermaid.
- **Trust boundaries** (browser ↔ nginx ↔ api ↔ internal services) — Mermaid.

Mermaid is sufficient for all; editable copies mirrored under `docs/diagrams/`.

---

## Documentation Risks

- The requirements/tasks docs are partly aspirational; readers who trust them
  over the README may expect auth/roles that do not exist. Mitigated by explicit
  Planned labels and a Security note.
- No git history and no license — contribution/ownership expectations are unclear.
- Azure OpenAI is required for the "interesting" parts; without it the UI works
  but ingestion/generation fail. Called out in Troubleshooting and Prerequisites.
