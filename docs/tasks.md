# Implementation Backlog — Enterprise Knowledge Agent Platform

**Version:** 1.0
**Status:** Implementation-ready engineering backlog
**Derived from:** `docs/Functional Requirements Document.md` (FRD v1.1) and `docs/Architecture and Solution Design Document.md` (ASDD v1.1)
**Architecture style:** Modular monolith API + two dedicated background workers
**Frontend design direction:** Dark-first "AI Operations Workspace" — calm, information-dense, evidence-first, one restrained accent, purposeful motion only (decisions D13–D19; Module 37)
**Stack:** React, TypeScript, Vite, React Router, TanStack Query, SignalR JS client, shadcn/ui + Radix UI, Tailwind CSS (semantic tokens), Motion, TanStack Table, Recharts, react-hook-form + zod, cmdk, Sonner, react-dropzone, react-resizable-panels, Monaco, react-markdown + remark-gfm + sanitizer, Lucide (Magic UI for isolated accents only), ASP.NET Core (.NET 10), EF Core, Keycloak (OIDC SSO, BFF pattern — decision D20), PostgreSQL + pgvector + PostgreSQL FTS, LocalStack (S3 + SQS), Azure OpenAI (embeddings + chat), Docker Compose

---

## How To Read This Document

This backlog is organized as:

```text
Phase → Module → Epic → User Story → Task → Subtasks
```

- **Phases** group modules into a dependency-ordered delivery sequence.
- **Modules** map 1:1 to the 36 required functional areas, plus one cross-cutting frontend design-system module (Module 37).
- **Epics** group related stories inside a module.
- **User stories** describe user/system value and list their child task IDs.
- **Tasks** use the full implementation contract (objective, requirements covered, detailed description, technical approach, contracts, data changes, security, error/recovery, acceptance criteria, tests, definition of done, notes, out of scope).

**Task ID convention:** `TASK-<MODULE-ABBR>-<NN>` (e.g. `TASK-KNW-03`).
**Story ID convention:** `STORY-<MODULE-ABBR>-<NN>`.
**Requirement references:** `FR-XXX-NNN` refer to FRD sections; `ASDD §N.N` refer to design sections.

**Module abbreviations:**

| # | Module | Abbr |
|---|--------|------|
| 1 | Repository & Development Foundation | FND |
| 2 | Identity & Authentication | IDN |
| 3 | Role-Based Authorization | AUTHZ |
| 4 | Application Management | APP |
| 5 | User Management (REMOVED — superseded by Keycloak/D20; module number retained for traceability only) | USR |
| 6 | Knowledge File Management | KNW |
| 7 | LocalStack S3 Integration | S3 |
| 8 | LocalStack SQS Integration | SQS |
| 9 | Background Job Infrastructure | JOB |
| 10 | TXT & Markdown Processing | PRC |
| 11 | Chunking | CHK |
| 12 | Azure OpenAI Configuration | AOI |
| 13 | Embedding Pipeline | EMB |
| 14 | PostgreSQL & pgvector | PG |
| 15 | File Replacement & Deletion | REP |
| 16 | Retrieval Service | RET |
| 17 | Agent Management | AGT |
| 18 | Agent Testing | AGTT |
| 19 | Agent Execution | RUN |
| 20 | Prompt Assembly & Safety | PRMT |
| 21 | Markdown Generation & Validation | GEN |
| 22 | Citation Management | CIT |
| 23 | Output Management | OUT |
| 24 | Secure Markdown Rendering | MDR |
| 25 | SignalR | SIG |
| 26 | Platform Dashboard | PDSH |
| 27 | Application Dashboard | ADSH |
| 28 | Processing Dashboard | PRDSH |
| 29 | Agent Run Dashboard | RDSH |
| 30 | Audit | AUD |
| 31 | Health & Operations | OPS |
| 32 | Error Handling | ERR |
| 33 | Logging & Observability | OBS |
| 34 | Testing | TST |
| 35 | Local Deployment & Developer Experience | DEP |
| 36 | Final Integration & Release Readiness | REL |
| 37 | Frontend Design System & Experience | UX |

---

## Phase Overview & Dependency Sequence

| Phase | Theme | Modules | Rationale |
|-------|-------|---------|-----------|
| P0 | Foundation | 1 | Repo, Docker Compose, solution skeleton, CI, local scripts must exist first. |
| P1 | Data, Identity & Frontend Foundation | 2, 14 (core), 37 | Database, migrations, Identity, and the frontend design system + application shell are prerequisites for everything. |
| P2 | AuthZ & Apps | 3, 4 | Roles (from Keycloak claims), application isolation gate all resource access. Module 5 (User Management) is removed — see decision D20; users/roles are provisioned entirely in Keycloak. |
| P3 | Storage & Messaging | 7, 8, 9 | S3, SQS, and job/outbox infrastructure enable async processing. |
| P4 | Ingestion | 6, 10, 11 | Upload → parse → chunk pipeline. |
| P5 | Vectors | 12, 13, 14 (vector) | Azure OpenAI config, embeddings, pgvector indexing. |
| P6 | Knowledge Lifecycle & Retrieval | 15, 16 | Safe replacement/deletion + application-scoped retrieval. |
| P7 | Agents | 17, 18 | Agent CRUD, prompt editor, agent test harness. |
| P8 | Execution & Generation | 19, 20, 21 | Run pipeline, prompt assembly, grounded generation. |
| P9 | Outputs & Citations | 22, 23, 24 | Citations, output library, safe Markdown rendering. |
| P10 | Realtime & Dashboards | 25, 26, 27, 28, 29 | SignalR + all dashboards. |
| P11 | Cross-cutting | 30, 31, 32, 33 | Audit, health/ops, error contract, observability. |
| P12 | Quality & Release | 34, 35, 36 | Test suites, deployment DX, release readiness. |

**Critical path:** P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7 → P8 → P9. Dashboards (P10) and cross-cutting (P11) run partly in parallel once their producing modules exist. Testing (34) is embedded per-task but consolidated in P12. **Module 37 (frontend design system)** is documented after Module 36 to preserve the 1–36 module numbering, but it is sequenced in **P1** (immediately after `TASK-FND-02`) and feeds every later UI module.

---

## Conflict Resolution & Assumptions

### Conflicts identified between FRD and ASDD

| # | Conflict | Resolution (prefer simpler design supporting main flow) | Where recorded |
|---|----------|--------------------------------------------------------|----------------|
| C1 | ASDD §12.3 offers **outbox** (preferred) vs **reconciler** (acceptable first milestone) for DB/queue consistency. | Implement the **transactional outbox** as the baseline (`TASK-JOB-04`) because it prevents permanently-stuck jobs; reconciler is folded in as a periodic safety net task (`TASK-JOB-05`). No conflict for the user; both are compatible. | Decision D1 |
| C2 | FRD FR-RUN-003 lists a **Cancelled** terminal state; ASDD §16 does not detail cancellation UX. | Persist the `Cancelled` state and support **cooperative cancellation before generation starts** only (`TASK-RUN-08`). Mid-generation cancellation is deferred (documented in Deferred Scope). | Decision D2 |
| C3 | ASDD §16.5 defers token streaming; FRD FR-RUN-007 asks for "meaningful run-stage changes". | Ship **stage-level progress** only for the baseline; token streaming deferred. | Decision D3 |
| C4 | ASDD §17.2 allows **structured output OR constrained citation markers**; deployments vary. | Implement **constrained citation-marker parsing** as the guaranteed path (`TASK-CIT-03`) and use structured output opportunistically when the deployment supports it (`TASK-GEN-04`). | Decision D4 |
| C5 | ASDD §10 starts with **exact cosine** search; HNSW deferred. | Baseline = exact cosine (`TASK-PG-06`); HNSW index is a deferred optimization task flagged but not implemented. | Decision D5 |

### Assumptions & Decisions (authoritative log)

- **D1 — Consistency:** Transactional outbox is the baseline job-publication mechanism; a reconciler backstops it.
- **D2 — Cancellation:** Only pre-generation cancellation is supported in the baseline.
- **D3 — Streaming:** Stage progress only; no token streaming in baseline.
- **D4 — Citations:** Server-side citation-marker parsing is authoritative; structured output is opportunistic.
- **D5 — Vector index:** Exact cosine baseline; HNSW deferred until latency/corpus justifies it.
- **D6 — Auth to Azure OpenAI:** `DefaultAzureCredential` (Entra ID) preferred; API-key path supported for pure local dev, server-side only.
- **D7 — Session model:** Browser uses secure HTTP-only cookie auth with CSRF + SameSite; no SPA bearer tokens in baseline (ASDD §19.2). Superseded in detail (not in spirit) by D20: the cookie session is now established via a Keycloak OIDC handshake (Backend-for-Frontend pattern) instead of local ASP.NET Core Identity credentials, but tokens still never reach browser JS.
- **D8 — Modular monolith:** One API deployable with internal modules; only the two workers are separate processes.
- **D9 — No cache / no Redis / no Ollama / no general versioning** — enforced as scope guards; any task proposing them is rejected.
- **D10 — Embedding config immutability:** Retrieval MUST embed queries with the exact deployment + dimensions used to build stored vectors (FR-RET-003); a corpus is treated as invalid for retrieval if the embedding config changes until re-indexed.
- **D11 — Idempotency:** All queue consumers are idempotent by design (at-least-once SQS delivery, ASDD §12.1).
- **D12 — .NET 10 / EF Core:** Target `net10.0`; use Npgsql + `pgvector-dotnet` for vector mapping.
- **D13 — UI foundation:** shadcn/ui (source-owned components) on Radix primitives + Tailwind semantic tokens is the single design system; **no second component framework** is introduced. Magic UI/Aceternity are accent-only (D18).
- **D14 — Visual identity:** Dark-first "AI Operations Workspace" — deep neutral background, one restrained indigo accent, semantic tokens; gradients limited to login, logo, and the active-AI processing indicator. No animated backgrounds behind data.
- **D15 — Motion policy:** `motion` (MIT) is used only for purposeful spatial-continuity, state-transition, and feedback animation (≈100–350 ms); always honor `prefers-reduced-motion`; **no decorative animation** in tables, forms, audit, or error/security screens.
- **D16 — Data & charts:** TanStack Table (headless) backs every data grid; Recharts is used only for actionable trends. No decorative/synthetic charts, no pie charts for two values, no 3D.
- **D17 — Navigation:** a permission-aware command palette (`cmdk`, Ctrl/Cmd+K) is the primary global action/search surface; no always-on large search field. Commands respect role/membership.
- **D18 — Forms & editors:** react-hook-form + zod for all forms; Monaco for large prompt/Markdown editing; react-resizable-panels for split layouts. Magic UI/Aceternity used only for isolated, license-verified accents (login, empty states, run-state).
- **D19 — Evidence-first UX:** the flagship surfaces are the processing stage-timeline and the evidence-first output/run workspace; Markdown always renders via the hardened `<SafeMarkdown>` (raw HTML disabled), never `dangerouslySetInnerHTML` (aligns with `TASK-MDR-01`).
- **D20 — Identity provider (supersedes the original ASP.NET Core Identity design):** Keycloak is the sole identity provider and system of record for user accounts and credentials, integrated via OpenID Connect using the Backend-for-Frontend (BFF) pattern — the ASP.NET Core API performs the Authorization Code + PKCE handshake server-side and issues its own HTTP-only session cookie; tokens never reach browser JS. The two platform roles (`SuperAdmin`, `SuperViewer`) are Keycloak realm roles, exposed as an `eka_platform_role` token claim. Application-scoped roles (`ApplicationAdmin`, `ApplicationViewer`) are Keycloak user-attribute claims formatted `"{applicationId}:{Role}"`, exposed as a multivalued `eka_app_role` claim — there is no local Postgres membership table and no local user CRUD (Module 5 / User Management is removed; see the Module table and `TASK-APP-05`). Local dev runs Keycloak as its own Compose service with its own Postgres database, realm auto-imported from a checked-in JSON file that also seeds a bootstrap Super Admin (replacing the former `TASK-FND-10` seeding step). This mirrors the proven pattern in the sibling `poc-with-ai` repository's Authorization Control Plane (realm roles + `acp_app_role`-style attribute claim), adapted to this platform's 4-role model.

### Clarification tasks (raised where a conflict could materially affect security/data integrity)

- `TASK-GEN-04` includes a clarification note on structured-output reliability per deployment.
- `TASK-PG-06` includes a clarification note on when to introduce HNSW (corpus-size threshold to be confirmed with representative data).

---

# PHASE 0 — Foundation

## MODULE 1 — Repository & Development Foundation (FND)

**Epics**

- EPIC-FND-A: Repository, solution & frontend scaffolding
- EPIC-FND-B: Containerization & local orchestration
- EPIC-FND-C: Environment, secrets & bootstrap scripts
- EPIC-FND-D: Code quality, CI & developer documentation

### STORY-FND-01: Establish repository and solution skeleton

**As a:** development team
**I want:** a mono-repo with the agreed folder layout, an ASP.NET Core solution, and a Vite React app
**So that:** every subsequent module has a consistent, buildable home

#### Business Value
Removes structural ambiguity; all later tasks slot into known locations, reducing rework and merge conflicts.

#### Story Acceptance Criteria
- Repo matches ASDD §26 layout (`src/`, `tests/`, `deploy/`, `docs/`, compose files, `.env.example`).
- `dotnet build` and `npm run build` both succeed on a clean clone.
- Shared contracts project is referenced by API and workers.

#### Dependencies
None (root of the dependency tree).

#### Tasks
`TASK-FND-01`, `TASK-FND-02`, `TASK-FND-03`

---

### TASK-FND-01: Create repository structure and .NET solution
**Module:** 1 — Foundation
**Epic:** EPIC-FND-A
**Story:** STORY-FND-01
**Priority:** Critical
**Implementation order:** 1
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** None
**Blocks:** Every other task
**Primary role:** Architecture / DevOps

#### Objective
Create the top-level repository layout and an ASP.NET Core solution containing API, two workers, a shared contracts library, and test project stubs, matching ASDD §26.

#### Requirements Covered
ASDD §26 (repository structure), §5.1 (compose services), §8 (backend layering), FR (implicit foundation for all).

#### Detailed Description
Create directories: `src/frontend`, `src/api`, `src/document-worker`, `src/agent-worker`, `src/shared`, `tests/{unit,integration,security,end-to-end}`, `deploy/{localstack,postgres,docker}`, `docs/`. Create a solution `EnterpriseKnowledgeAgent.sln` referencing five projects: `Api` (ASP.NET Core web), `DocumentWorker` (worker service), `AgentWorker` (worker service), `Shared` (class library for DTOs/contracts/enums), and `Domain`/`Application`/`Infrastructure` class libraries (per ASDD §8 layering) — or fold layering into the Api project with folder-based modules for the monolith. Target `net10.0` for all. Establish `Directory.Build.props` with common settings (`Nullable=enable`, `TreatWarningsAsErrors` for release, `LangVersion=latest`). Add `.editorconfig`, `.gitignore`, `.gitattributes`.

Business rules: workers reference `Shared` + `Infrastructure` but never the Api web project. The `Shared` project holds no infrastructure dependencies. No task may introduce Redis/Ollama packages.

#### Technical Approach
- `dotnet new sln`; `dotnet new webapi -o src/api`; `dotnet new worker -o src/document-worker`; `dotnet new worker -o src/agent-worker`; `dotnet new classlib -o src/shared`.
- Add layered class libs `Domain`, `Application`, `Infrastructure` under `src/api` or `src/` and wire references API→Application→Domain, Infrastructure→Domain, API/Workers→Infrastructure.
- Commit a `Directory.Packages.props` for central package version management.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Ensure `.gitignore` excludes `.env`, `*.user`, `appsettings.*.Local.json`, and any secret files so credentials cannot be committed (FR-SEC-002).

#### Error and Recovery Behavior
Not applicable (build-time). CI must fail if any project fails to restore/build.

#### Acceptance Criteria
- Given a clean clone, when running `dotnet build EnterpriseKnowledgeAgent.sln`, then all projects build with zero errors.
- Given the solution, when inspecting references, then workers do not reference the Api web project.
- Given `.gitignore`, when adding a `.env` file, then git does not track it.

#### Test Requirements
- Architecture test stub (`tests/unit`) asserting layering references (expanded in `TASK-TST-03`).
- CI smoke build (see `TASK-FND-11`).

#### Definition of Done
- [ ] Directory layout matches ASDD §26.
- [ ] Solution builds clean.
- [ ] Central package/version management configured.
- [ ] `.editorconfig`, `.gitignore`, `Directory.Build.props` committed.

#### Implementation Notes
Prefer folder-based modules inside the Api project (Identity, Applications, Knowledge, …) per ASDD §3.1 rather than one assembly per module to keep the monolith simple.

#### Out of Scope
Business logic, EF entities, Docker files (separate tasks).

---

### TASK-FND-02: Scaffold Vite + React + TypeScript frontend
**Module:** 1 — Foundation
**Epic:** EPIC-FND-A
**Story:** STORY-FND-01
**Priority:** Critical
**Implementation order:** 2
**Estimated size:** M
**Can run in parallel:** Yes (with FND-03)
**Dependencies:** TASK-FND-01
**Blocks:** All frontend tasks
**Primary role:** Frontend

#### Objective
Create the React SPA shell with Vite, TypeScript, React Router, TanStack Query, and the SignalR client, plus a base layout and routing skeleton matching ASDD §7.2.

#### Requirements Covered
ASDD §7 (frontend design), FR-DSH-004 (reconnection UI), FR-OUT-001 (views), general SPA foundation.

#### Detailed Description
Initialize `src/frontend` with Vite React-TS template. Install `react-router-dom`, `@tanstack/react-query`, `@microsoft/signalr`, a Markdown stack (`react-markdown`, `remark-gfm`), a sanitizer (`rehype-sanitize` / DOMPurify), an editor (`@monaco-editor/react` or CodeMirror), and a UI component library. Configure a typed API client wrapper (fetch with credentials `include` for cookie auth per D7), a `QueryClient` provider, a router with the ASDD §7.2 route table (using placeholder pages), a global app shell (nav, connection indicator placeholder), and environment config via `import.meta.env` (`VITE_API_BASE_URL`).

Business rules: no Azure OpenAI credentials or secrets in the frontend (FR-SEC-002); all API calls include credentials for cookie session.

#### Technical Approach
- Route table: `/login`, `/platform/*`, `/applications/:applicationId/*` per ASDD §7.2 as lazy-loaded route stubs.
- `apiClient.ts`: base fetch wrapper adding `credentials: 'include'`, JSON handling, Problem Details parsing (aligns with `TASK-ERR-01`).
- `queryClient.ts`: default staleness, retry policy; SignalR event handlers will invalidate queries (`TASK-SIG-06`).

#### API or Event Contracts
Not applicable (consumes contracts defined later).

#### Data Changes
Not applicable.

#### Security and Authorization
No secrets in bundle; CSP + sanitizer wiring stubbed (finished in `TASK-MDR-01`). Cookie-based auth only.

#### Error and Recovery Behavior
Global error boundary component; API wrapper surfaces Problem Details `title`/`correlationId`.

#### Acceptance Criteria
- Given a clean clone, when running `npm ci && npm run build`, then the build succeeds.
- Given the running dev server, when navigating to each top-level route, then a placeholder page renders without crash.
- Given the bundle, when inspecting output, then no secret/env credential value is present.

#### Test Requirements
- Component render smoke test for the app shell (Vitest + Testing Library).
- Lint passes.

#### Definition of Done
- [ ] Vite/TS app builds and serves.
- [ ] Router, QueryClient, SignalR client wired.
- [ ] Typed API wrapper with cookie credentials.
- [ ] Placeholder pages for all ASDD §7.2 routes.

#### Implementation Notes
Keep pages thin; feature teams fill them in their modules. This task only scaffolds the SPA; design tokens, shadcn/Radix foundation, the application shell, command palette, and shared components are built in **Module 37 (UX)** and consumed by every feature module.

#### Out of Scope
Real feature pages, auth flow logic (module 2), Markdown sanitizer finalization (module 24), design system + app shell + shared components (module 37).

---

### TASK-FND-03: Define shared contracts and enums library
**Module:** 1 — Foundation
**Epic:** EPIC-FND-A
**Story:** STORY-FND-01
**Priority:** High
**Implementation order:** 3
**Estimated size:** S
**Can run in parallel:** Yes (with FND-02)
**Dependencies:** TASK-FND-01
**Blocks:** Queue message tasks, API contract tasks
**Primary role:** Backend / Architecture

#### Objective
Establish the `Shared` project's canonical enums, queue message DTOs, and correlation primitives used across API and both workers.

#### Requirements Covered
ASDD §11.3 (SQS messages contain identifiers only), §18.3 (event contract), §23.1 (correlation), FR-PRC-001, FR-RUN-002.

#### Detailed Description
Define enums for user-facing file states (`Queued, Processing, Ready, Failed, Disabled, Deleting`), processing stages, run states/stages (FR-RUN-003), error categories (ASDD §22), and roles. Define queue message records: `DocumentProcessingMessage`, `AgentExecutionMessage`, `DeletionMessage` — each carrying only IDs + `CorrelationId` + `AttemptNumber` + `MessageId` (no content, no secrets). Define a `CorrelationId` value type and a `SignalREvent<T>` envelope (event id, type, applicationId, entityId, timestamp UTC, sequence number, payload) per ASDD §18.3.

#### Technical Approach
- Immutable `record` types; `System.Text.Json` source-generation-friendly.
- Central enum for `UserFacingFileStatus` and internal `ProcessingStage` (FR-PRC-002) kept distinct.

#### API or Event Contracts
Queue: `DocumentProcessingMessage { JobId, FileId, ApplicationId, ReplacementId?, CorrelationId, AttemptNumber, MessageId }`; `AgentExecutionMessage { RunId, AgentId, ApplicationId, CorrelationId, AttemptNumber, MessageId }`; `DeletionMessage { FileId, ApplicationId, CorrelationId, MessageId }`. SignalR envelope as above.

#### Data Changes
Not applicable.

#### Security and Authorization
Messages carry no file content, prompts, or secrets (FR-SEC-002, FR-PRC-001).

#### Error and Recovery Behavior
`AttemptNumber` supports retry classification (`TASK-JOB-09`).

#### Acceptance Criteria
- Given a queue message type, when serialized, then it contains only identifiers/correlation and no content fields.
- Given both workers, when referencing message types, then they compile against `Shared`.

#### Test Requirements
- Serialization round-trip unit tests; assertion that no `Content`/`Prompt`/`Secret` property exists (reflection test).

#### Definition of Done
- [ ] Enums and message DTOs defined and referenced by API + workers.
- [ ] Reflection test guards against content leakage in messages.

#### Implementation Notes
Keep this project dependency-free (no EF, no AWS SDK).

#### Out of Scope
Actual queue send/receive (module 8).

---

### STORY-FND-02: Containerize and orchestrate all local services

**As a:** development team
**I want:** Dockerfiles and a Docker Compose stack for frontend, API, both workers, Postgres+pgvector, and LocalStack
**So that:** the whole platform runs locally with a single command (ASDD §5.1, FR-19)

#### Business Value
Enables reproducible local environments and satisfies acceptance criterion "Docker Compose starts all local components".

#### Story Acceptance Criteria
- `docker compose up` starts all six services on one network (ASDD §6).
- Only frontend + API expose host ports by default; Postgres/LocalStack exposed under a dev profile.
- Healthchecks gate dependent service startup.

#### Dependencies
STORY-FND-01.

#### Tasks
`TASK-FND-04`, `TASK-FND-05`, `TASK-FND-06`

---

### TASK-FND-04: Author Dockerfiles for API, workers, and frontend
**Module:** 1 — Foundation
**Epic:** EPIC-FND-B
**Story:** STORY-FND-02
**Priority:** Critical
**Implementation order:** 4
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-01, TASK-FND-02
**Blocks:** TASK-FND-05
**Primary role:** DevOps

#### Objective
Produce multi-stage Dockerfiles that build and run each service image reproducibly.

#### Requirements Covered
ASDD §5.1, FR-19 (Docker Compose services).

#### Detailed Description
Create `.NET` SDK→runtime multi-stage Dockerfiles for API and both workers (restore, build, publish, non-root runtime user, expose 8080 for API). Create a frontend Dockerfile (node build stage → static server / nginx) serving the Vite build; inject `VITE_API_BASE_URL` at build/runtime. Images must run as non-root and contain no secrets.

#### Technical Approach
- `mcr.microsoft.com/dotnet/sdk:10.0` build, `aspnet:10.0` runtime.
- Layer caching: copy csproj + restore before copying source.
- Frontend served behind nginx with SPA fallback and baseline security headers (finalized in `TASK-MDR-05`).

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Non-root user; no secrets baked into images; `.dockerignore` excludes `.env`, `bin`, `obj`, `node_modules`.

#### Error and Recovery Behavior
Build fails fast on restore/build error.

#### Acceptance Criteria
- Given each Dockerfile, when built, then an image is produced that starts and responds to its health endpoint (API) or serves the SPA (frontend).
- Given the images, when inspected, then they run as non-root and contain no `.env`.

#### Test Requirements
- CI builds all images.

#### Definition of Done
- [ ] Four Dockerfiles (api, document-worker, agent-worker, frontend) build successfully.
- [ ] `.dockerignore` files present.
- [ ] Non-root runtime.

#### Implementation Notes
Keep worker images identical base; differ only in entrypoint project.

#### Out of Scope
Compose wiring (FND-05).

---

### TASK-FND-05: Compose stack, network, healthchecks and dev profile
**Module:** 1 — Foundation
**Epic:** EPIC-FND-B
**Story:** STORY-FND-02
**Priority:** Critical
**Implementation order:** 5
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-FND-04
**Blocks:** TASK-FND-07, all runtime tasks
**Primary role:** DevOps

#### Objective
Author `docker-compose.yml` + `docker-compose.override.yml` wiring all six services on a private network with healthchecks and dependency ordering (ASDD §5.1, §6).

#### Requirements Covered
ASDD §5.1, §6 (network design), FR-19.

#### Detailed Description
Define services: `frontend`, `api`, `document-worker`, `agent-worker`, `postgres` (pgvector image), `localstack`. One user-defined bridge network; Docker DNS names `api:8080`, `postgres:5432`, `localstack:4566`. Expose only `frontend` and `api` to host by default; put Postgres/LocalStack host ports under a `dev`/`diagnostics` profile. Add healthchecks: Postgres `pg_isready`, LocalStack readiness endpoint, API `/health/ready`. Use `depends_on` with `condition: service_healthy`. Wire environment via `.env` (see FND-07). Named volume for Postgres data; LocalStack init hook mounts the init scripts (FND-08 uses `TASK-S3/SQS` init).

#### Technical Approach
- Postgres image `pgvector/pgvector:pg16` (or equivalent supporting pgvector).
- LocalStack `SERVICES=s3,sqs`; mount `deploy/localstack/init` to `/etc/localstack/init/ready.d`.
- Restart policy `unless-stopped` for infra; workers `on-failure`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Named volume `postgres-data`.

#### Security and Authorization
No secrets in compose file literals; reference `.env`. Postgres/LocalStack not exposed to host in default profile.

#### Error and Recovery Behavior
Dependent services wait for `service_healthy`; document the known LocalStack-restart transient (workers should retry SQS connect).

#### Acceptance Criteria
- Given a clean machine with `.env`, when running `docker compose up -d`, then all six services reach healthy state.
- Given default profile, when checking host ports, then only frontend and API are published.

#### Test Requirements
- Manual/CI smoke: `docker compose up` then poll `api:/health/ready`.

#### Definition of Done
- [ ] Compose + override author all six services on one network.
- [ ] Healthchecks + `depends_on` conditions set.
- [ ] Dev profile exposes diagnostics ports only when requested.

#### Implementation Notes
Recall (repo memory): OPA-style containers don't auto-reload; not relevant here, but LocalStack init only runs on fresh container — document `docker compose down -v` to re-seed.

#### Out of Scope
LocalStack bucket/queue creation scripts (modules 7/8), migrations (FND-09).

---

### TASK-FND-06: Local HTTPS/dev reverse-proxy and CORS baseline
**Module:** 1 — Foundation
**Epic:** EPIC-FND-B
**Story:** STORY-FND-02
**Priority:** Medium
**Implementation order:** 6
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-05
**Blocks:** SignalR cookie auth (module 25)
**Primary role:** DevOps / Backend

#### Objective
Ensure the SPA and API share an origin (or correctly configured CORS + cookie settings) so HTTP-only cookie auth and SignalR work locally.

#### Requirements Covered
ASDD §19.2 (secure browser session), FR-SEC-005, FR-DSH-003/004 (SignalR).

#### Detailed Description
Configure either a single-origin dev proxy (Vite dev server proxy `/api` → `api:8080`) or explicit CORS allowing the frontend origin with `AllowCredentials`, plus cookie `SameSite`/`Secure` settings appropriate for local dev. Ensure SignalR negotiate requests carry cookies.

#### Technical Approach
- Prefer Vite proxy in dev to keep same-origin (simplifies CSRF/SameSite).
- In compose, nginx proxies `/api` and `/hubs` to API.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
CORS must not use wildcard with credentials; explicit origin only (FR-SEC-005).

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given the SPA, when it calls the API, then cookies are sent and CORS does not block credentialed requests.
- Given SignalR negotiate, when connecting, then the auth cookie is included.

#### Test Requirements
- Manual verification of a credentialed request + SignalR handshake.

#### Definition of Done
- [ ] Single-origin proxy (dev) and explicit CORS (compose) configured.
- [ ] Credentialed requests and SignalR handshake succeed.

#### Implementation Notes
Same-origin proxy is the least error-prone choice for cookie + CSRF.

#### Out of Scope
CSRF token issuance (module 2), production TLS.

---

### STORY-FND-03: Environment config, secrets, and bootstrap automation

**As a:** development team
**I want:** an `.env.example`, secret handling guidance, and scripts that migrate the DB, create buckets/queues, and seed the Super Admin
**So that:** a new developer reaches a working system by following the ASDD §27 bootstrap sequence

#### Business Value
Turns a checkout into a running platform, satisfying FR-19 setup-script requirements and acceptance criterion 2 (seeded Super Admin can log in).

#### Story Acceptance Criteria
- `.env.example` documents every required variable; no real secrets committed.
- One documented sequence brings up infra, applies migrations, provisions S3/SQS, and seeds Super Admin.
- Azure OpenAI connectivity check script exists.

#### Dependencies
STORY-FND-02, plus later hooks into modules 2/7/8/12.

#### Tasks
`TASK-FND-07`, `TASK-FND-08`, `TASK-FND-09`, `TASK-FND-10`

---

### TASK-FND-07: Environment template and local secret strategy
**Module:** 1 — Foundation
**Epic:** EPIC-FND-C
**Story:** STORY-FND-03
**Priority:** Critical
**Implementation order:** 7
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-05
**Blocks:** TASK-FND-10, module 12
**Primary role:** DevOps / Security

#### Objective
Provide `.env.example` and a documented secret strategy (env vars, .NET user-secrets, Docker secrets) so no credential is ever committed.

#### Requirements Covered
ASDD §20.4 (secret storage), §27, FR-AOI-002, FR-SEC-002.

#### Detailed Description
Enumerate all variables: Postgres connection, LocalStack endpoint/region/fake creds, S3 bucket names, SQS queue names, Azure OpenAI endpoint, auth mode (`entra`|`apikey`), chat/embedding deployment names, context/output limits, embedding dimensions, request timeout, retry count, ASP.NET cookie/data-protection keys, seed Super Admin email. Document that Azure OpenAI keys go into `.env` (git-ignored) or user-secrets, never source. Provide `dotnet user-secrets` init instructions for the API + workers.

#### Technical Approach
- `.env.example` with safe placeholder values and inline comments.
- README section mapping each var to the config binding.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
`.env` git-ignored; example contains no real secrets; Azure keys never returned to frontend (FR-AOI-002).

#### Error and Recovery Behavior
Startup config validation (in module 12) fails fast on missing required values.

#### Acceptance Criteria
- Given `.env.example`, when a developer copies it to `.env` and fills values, then services read all required config.
- Given the repo, when scanning for secrets, then none are present in tracked files.

#### Test Requirements
- Optional secret-scan step in CI (gitleaks-style) — see `TASK-FND-11`.

#### Definition of Done
- [ ] `.env.example` complete and documented.
- [ ] Secret strategy documented.
- [ ] No secrets tracked.

#### Implementation Notes
Recall (user memory): GitHub Models free endpoint can 410 (retirement brownout) — not used here; Azure OpenAI is the provider.

#### Out of Scope
Actual Azure config binding/validation (module 12).

---

### TASK-FND-08: LocalStack init scripts for S3 buckets and SQS queues
**Module:** 1 — Foundation
**Epic:** EPIC-FND-C
**Story:** STORY-FND-03
**Priority:** Critical
**Implementation order:** 8
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-05
**Blocks:** Modules 7, 8
**Primary role:** DevOps

#### Objective
Provide idempotent LocalStack init scripts that create the S3 buckets and SQS queues (incl. DLQs) from ASDD §11.

#### Requirements Covered
ASDD §11.1 (buckets), §11.3 (queues), FR-19 (bucket/queue creation scripts).

#### Detailed Description
Under `deploy/localstack/init`, add a `ready.d` shell script using `awslocal` to create buckets `rag-source-files`, `rag-generated-exports` (optional), and queues `document-processing`, `document-processing-dlq`, `agent-execution`, `agent-execution-dlq`, `deletion-jobs`, `deletion-jobs-dlq` with redrive policies (maxReceiveCount) wiring each main queue to its DLQ. Script must be idempotent (ignore "already exists").

#### Technical Approach
- `awslocal s3 mb`, `awslocal sqs create-queue` with `RedrivePolicy` referencing DLQ ARNs.
- Buckets private (no public ACL).

#### API or Event Contracts
Queue names/ARNs as above.

#### Data Changes
Not applicable (infra provisioning).

#### Security and Authorization
Buckets private; server-generated keys enforced later (module 7).

#### Error and Recovery Behavior
Idempotent; re-run safe. Note: init only runs on fresh container — document `down -v` to re-run.

#### Acceptance Criteria
- Given a fresh LocalStack, when it becomes ready, then all buckets and queues (with DLQ redrive) exist.
- Given a re-run, when the script executes, then it does not error on existing resources.

#### Test Requirements
- Smoke: `awslocal s3 ls` / `awslocal sqs list-queues` after startup.

#### Definition of Done
- [ ] Buckets + queues + DLQ redrive created idempotently.
- [ ] Mounted via compose init hook.

#### Implementation Notes
Keep queue names in `.env` and echo them so app config and scripts stay in sync.

#### Out of Scope
App-side S3/SQS clients (modules 7/8).

---

### TASK-FND-09: Database migration bootstrap and apply script
**Module:** 1 — Foundation
**Epic:** EPIC-FND-C
**Story:** STORY-FND-03
**Priority:** Critical
**Implementation order:** 9
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-FND-05, TASK-PG-01 (schema/DbContext exists)
**Blocks:** Any runtime task needing DB
**Primary role:** Database / DevOps

#### Objective
Provide a repeatable way to apply EF Core migrations to the Postgres container, including the pgvector extension enablement.

#### Requirements Covered
ASDD §27 (apply migrations), §10 (schema), FR-19.

#### Detailed Description
Add a migration-apply script (`deploy/postgres/migrate.ps1`/`.sh`) invoking `dotnet ef database update` against the compose Postgres, or an API startup migrator behind an explicit flag for local dev. Ensure `CREATE EXTENSION IF NOT EXISTS vector` runs before vector columns (in the first migration or an init SQL). Support a clean re-init path.

#### Technical Approach
- Prefer explicit script over auto-migrate-on-startup to avoid surprising schema changes; provide both, default to script.
- First migration includes schemas (ASDD §10) and pgvector extension.

#### API or Event Contracts
Not applicable.

#### Data Changes
Creates all schemas/tables from module 14 migrations; enables pgvector.

#### Security and Authorization
Migration uses a dedicated app DB user; least privilege documented.

#### Error and Recovery Behavior
Script exits non-zero on failure; safe to re-run (EF migration idempotency).

#### Acceptance Criteria
- Given a running Postgres, when running the migrate script, then all schemas/tables exist and pgvector is enabled.
- Given an already-migrated DB, when re-running, then it is a no-op.

#### Test Requirements
- Integration test spins Postgres + applies migrations (module 14 tests).

#### Definition of Done
- [ ] Migrate script applies all migrations incl. pgvector.
- [ ] Re-run safe.

#### Implementation Notes
Depends on module 14 defining the DbContext; sequence FND-09 after PG-01 in practice.

#### Out of Scope
Entity definitions (module 14).

---

### TASK-FND-10: Bootstrap Super Admin (via Keycloak realm import) and Azure OpenAI connectivity check
**Module:** 1 — Foundation
**Epic:** EPIC-FND-C
**Story:** STORY-FND-03
**Priority:** Critical
**Implementation order:** 10
**Estimated size:** S
**Can run in parallel:** No
**Dependencies:** TASK-FND-09, TASK-IDN-01, TASK-AOI-02
**Blocks:** Acceptance criteria 2, 10 (login/create)
**Primary role:** Backend / DevOps

#### Objective
Confirm a fresh clone has a working bootstrap Super Admin login (via the Keycloak realm import performed in `TASK-IDN-01` — no application-level seeding step) and provide a script that validates Azure OpenAI embedding + generation deployment reachability.

#### Requirements Covered
ASDD §27 (bootstrap Super Admin, connectivity checks — revised for decision D20), FR-19, FR-AOI-004, acceptance criteria #2.

#### Detailed Description
No application-side seeding is needed for the Super Admin: `deploy/keycloak/realm-export.json` (imported by Keycloak on first boot, per `TASK-IDN-01`) already contains a bootstrap Super Admin user with the `SuperAdmin` realm role. This task's remaining scope is the Azure OpenAI connectivity check: a script/endpoint calling `IEmbeddingService.CheckHealthAsync()` and `ITextGenerationService.CheckHealthAsync()` and reporting reachability without consuming large quota.

#### Technical Approach
- Connectivity check performs a minimal embedding/`models`-style call; respects timeouts (module 12).
- A README/runbook note documents that the Super Admin is bootstrapped by Keycloak, not by a CLI seed command.

#### API or Event Contracts
Optional admin endpoint `POST /api/operations/aoai-check` (Super Admin only) returning per-deployment reachability.

#### Data Changes
None (Keycloak owns the bootstrap user in its own database; no local Postgres row).

#### Security and Authorization
Bootstrap Super Admin credentials live only in the local-dev realm-import JSON (not committed with real secrets in non-local environments); connectivity endpoint restricted to Super Admin.

#### Error and Recovery Behavior
Connectivity check returns a structured unreachable result (does not crash startup) mapped to `DependencyUnavailable`.

#### Acceptance Criteria
- Given a fresh clone, when Keycloak imports its realm, then the bootstrap Super Admin can log in with no separate seed step.
- Given misconfigured Azure OpenAI, when the connectivity check runs, then it reports unreachable clearly without throwing.

#### Test Requirements
- Integration test: connectivity check against a fake Azure OpenAI (module 34); Keycloak realm-import verified as part of `TASK-IDN-01`'s integration test.

#### Definition of Done
- [ ] Bootstrap Super Admin login confirmed via Keycloak realm import (no app-side seed code).
- [ ] Azure OpenAI connectivity check script/endpoint.

#### Implementation Notes
Supersedes the original ASP.NET Core Identity–based `UserManager` seeding (decision D20); the bootstrap user now lives entirely in `deploy/keycloak/realm-export.json`, owned by `TASK-IDN-01`.

#### Out of Scope
Any local user management (Module 5 — removed).

---

### STORY-FND-04: Code quality, CI, and developer documentation

**As a:** development team
**I want:** linting/formatting, a CI pipeline, and a README/runbook
**So that:** contributions stay consistent and the project is self-explanatory

#### Business Value
Reduces defects and onboarding time; enforces the scope guards (no Redis/Ollama/cache).

#### Story Acceptance Criteria
- CI builds API, workers, frontend; runs unit tests and lint.
- README documents bootstrap, reset, and troubleshooting.

#### Dependencies
STORY-FND-01..03.

#### Tasks
`TASK-FND-11`, `TASK-FND-12`

---

### TASK-FND-11: Code-quality tooling and CI pipeline
**Module:** 1 — Foundation
**Epic:** EPIC-FND-D
**Story:** STORY-FND-04
**Priority:** High
**Implementation order:** 11
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-01, TASK-FND-02
**Blocks:** None (quality gate)
**Primary role:** DevOps

#### Objective
Configure formatting/linting for both stacks and a CI pipeline that builds, tests, lints, and scans for secrets.

#### Requirements Covered
General engineering quality; FR-SEC-002 (secret scan).

#### Detailed Description
Backend: `dotnet format`, analyzers, nullable warnings-as-errors in CI. Frontend: ESLint + Prettier + `tsc --noEmit`. CI (GitHub Actions) jobs: restore/build/test .NET; install/build/test/lint frontend; build Docker images; optional secret scan. Cache dependencies.

#### Technical Approach
- Single workflow with matrix/jobs; fail on lint or test failure.
- Recall (user memory): GitHub Actions parser does NOT support YAML merge keys (`<<: *anchor`); spell out each job's `env` fully — do not use `<<:` merge keys.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Secret scan step; no secrets in workflow logs.

#### Error and Recovery Behavior
CI fails the PR on any gate failure.

#### Acceptance Criteria
- Given a PR, when CI runs, then build+test+lint execute and block on failure.
- Given the workflow YAML, when parsed by GitHub, then it is valid (no merge-key usage).

#### Test Requirements
- The pipeline runs the existing unit test stubs.

#### Definition of Done
- [ ] Lint/format configured for both stacks.
- [ ] CI builds, tests, lints, scans; valid workflow YAML (no `<<:` merge keys).

#### Implementation Notes
Whole-node YAML anchors (`&x`/`*x` with no extra keys) are fine in GH Actions; keyed merges are not.

#### Out of Scope
Deployment/CD.

---

### TASK-FND-12: Developer README, runbook, and troubleshooting guide
**Module:** 1 — Foundation
**Epic:** EPIC-FND-D
**Story:** STORY-FND-04
**Priority:** High
**Implementation order:** 12
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-05, TASK-FND-08, TASK-FND-09, TASK-FND-10
**Blocks:** None
**Primary role:** DevOps / Documentation

#### Objective
Document the ASDD §27 bootstrap sequence, reset/backup/restore procedures, and common troubleshooting.

#### Requirements Covered
ASDD §27, FR-19; module 35 overlaps (final consolidation in `TASK-DEP-06`).

#### Detailed Description
README covering: prerequisites, copy `.env.example`, start compose, apply migrations, provision S3/SQS, seed Super Admin, run connectivity check, start frontend, log in. Include reset (`down -v` + re-init), and known-gotchas (LocalStack init only on fresh container; Postgres named-volume wipe requires explicit `docker volume rm`).

#### Technical Approach
- Markdown in repo root + `docs/`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Document secret handling; never paste real secrets.

#### Error and Recovery Behavior
Troubleshooting for failed healthchecks, migration errors, LocalStack re-seed.

#### Acceptance Criteria
- Given a new developer, when following the README, then they reach a login screen with a seeded Super Admin.

#### Test Requirements
- Doc-lint (markdownlint) optional.

#### Definition of Done
- [ ] README + runbook + troubleshooting committed.

#### Implementation Notes
Recall (docker memory): named volumes are not removed by `compose rm -v`; document explicit `docker volume rm <project>_postgres-data` to wipe seed data.

#### Out of Scope
Full operational runbook (module 36).

---

# PHASE 1 — Data & Identity

## MODULE 2 — Identity & Authentication (IDN)

**Epics**

- EPIC-IDN-A: Keycloak realm + OIDC (BFF) foundation — decision D20
- EPIC-IDN-B: Login/logout & secure session
- EPIC-IDN-C: Account protection & auth audit

### STORY-IDN-01: SSO via Keycloak with a secure cookie session

**As a:** platform user
**I want:** to sign in and out through the organization's identity provider (Keycloak), with the app maintaining a protected session
**So that:** only authenticated users access the platform, and credential/password lifecycle is owned by the IdP (FR-SEC-005, ASDD §19, decision D20)

#### Business Value
Gates all functionality; satisfies acceptance criterion #2 (Super Admin can log in) and security baseline, without the app taking on password/credential management.

#### Story Acceptance Criteria
- Keycloak (not the app) hashes passwords, enforces lockout, and owns credential lifecycle.
- Login issues an HTTP-only, `Secure`, `SameSite` cookie (BFF pattern) with CSRF protection; tokens never reach browser JS.
- Platform roles (`SuperAdmin`/`SuperViewer`) and application-scoped roles (`ApplicationAdmin`/`ApplicationViewer`) both arrive as token claims (`eka_platform_role`, `eka_app_role`) with no local user-management CRUD.

#### Dependencies
STORY-FND-01, TASK-FND-09.

#### Tasks
`TASK-IDN-01`, `TASK-IDN-02`, `TASK-IDN-03`, `TASK-IDN-04`

---

### TASK-IDN-01: Configure Keycloak realm, OIDC client, and claim mapping
**Module:** 2 — Identity & Authentication
**Epic:** EPIC-IDN-A
**Story:** STORY-IDN-01
**Priority:** Critical
**Implementation order:** 13
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-FND-01
**Blocks:** TASK-IDN-02..04, TASK-FND-10, all authz
**Primary role:** Backend / Security / DevOps

#### Objective
Stand up Keycloak as the external identity provider for local dev (a new Compose service with its own Postgres database), define a realm with the two platform roles as Keycloak realm roles, and register a confidential OIDC client for the ASP.NET Core backend using the Backend-for-Frontend (BFF) pattern.

#### Requirements Covered
ASDD §19 (revised — decision D20), FR-SEC-005, FR-3 (roles).

#### Detailed Description
Add a `keycloak` Compose service (`quay.io/keycloak/keycloak`) with its own `keycloak-postgres` database, realm auto-imported from a checked-in `deploy/keycloak/realm-export.json` at container start (`start-dev --import-realm`). The realm defines:
- Two **realm roles**: `SuperAdmin`, `SuperViewer` (platform-level, FRD §3.1/§3.2).
- A confidential OIDC client (`eka-api`, `publicClient=false`, `standardFlowEnabled=true`, PKCE `S256`) used by the ASP.NET Core API for the Authorization Code flow. The redirect URI points at the API's own OIDC callback route, never directly at the SPA — tokens never reach browser JS (BFF pattern).
- A client scope bundling two protocol mappers so ID/access tokens carry: `eka_platform_role` (realm-role mapper, multivalued) and `eka_app_role` (user-attribute mapper reading a multivalued `eka_app_role` user attribute formatted `"{applicationId}:{ApplicationAdmin|ApplicationViewer}"`, multivalued). Application-scoped role assignment therefore lives in Keycloak user attributes, not a local Postgres membership table (see the revised `TASK-APP-05`).
- A bootstrap Super Admin user imported directly in the realm JSON (`realmRoles: ["SuperAdmin"]`) so a fresh clone has a working login with no separate seeding step (replaces the former Identity-based seed in `TASK-FND-10`).

On the ASP.NET Core side, register `AddAuthentication(cookieScheme).AddCookie(...).AddOpenIdConnect(...)`: `Authority` = the realm issuer, `ClientId`/`ClientSecret` from configuration/`dotnet user-secrets`, `ResponseType = "code"`, PKCE enabled, and an `OnTokenValidated` event that copies the `eka_platform_role`/`eka_app_role` claims onto the `ClaimsPrincipal` persisted in the app's own cookie (the durable session artifact — raw tokens are not retained).

This design mirrors the proven pattern already used in the sibling `poc-with-ai` repository's Authorization Control Plane (Keycloak realm roles + an `acp_app_role`-style multivalued attribute claim formatted `"{id}:{role}"`), adapted to this platform's 4-role model.

#### Technical Approach
- `docker-compose.yml`: new `keycloak` + `keycloak-postgres` services with a healthcheck on Keycloak's HTTP port.
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + `Microsoft.AspNetCore.Authentication.Cookies`.
- Realm JSON checked into `deploy/keycloak/realm-export.json`, documented in the README.

#### API or Event Contracts
Not applicable (internal auth wiring; endpoints defined in `TASK-IDN-02`).

#### Data Changes
None in the application's own Postgres database — Keycloak owns its own `keycloak-postgres` database exclusively; `TASK-PG-01`'s schema list no longer includes local `identity.*` credential tables.

#### Security and Authorization
Client secret via `dotnet user-secrets` (never `.env`); PKCE mandatory; tokens never exposed to the browser (BFF); redirect/web-origins locked to known local dev URLs.

#### Error and Recovery Behavior
Keycloak unreachable at startup → OIDC discovery failure surfaces as a clear `DependencyUnavailable` error on the first login attempt, not a crash of the API itself.

#### Acceptance Criteria
- Given a fresh clone, when `docker compose up` runs, then Keycloak starts, imports the realm, and the bootstrap Super Admin can authenticate.
- Given a user with the `eka_app_role` attribute `"app-1:ApplicationAdmin"`, when they authenticate, then the resulting cookie principal carries that claim.
- Given the realm roles, when a Super Admin authenticates, then `eka_platform_role` includes `SuperAdmin`.

#### Test Requirements
- Integration: OIDC handshake against a real Keycloak container (Testcontainers or the Compose service) resulting in a cookie carrying the expected claims.

#### Definition of Done
- [ ] Keycloak Compose service + realm import (roles, client, claim mappers, bootstrap Super Admin).
- [ ] ASP.NET Core cookie + OIDC scheme registered; claims copied onto the cookie principal.

#### Implementation Notes
Supersedes the original ASP.NET Core Identity design (decision D20). No local `identity` Postgres schema tables are created by this task.

#### Out of Scope
Login/logout/me endpoints (`TASK-IDN-02`); local user CRUD (removed — see Module 5 note).

---

### TASK-IDN-02: OIDC login/callback/logout endpoints and CSRF
**Module:** 2 — Identity & Authentication
**Epic:** EPIC-IDN-B
**Story:** STORY-IDN-01
**Priority:** Critical
**Implementation order:** 14
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-IDN-01, TASK-FND-06
**Blocks:** All authenticated endpoints, frontend login
**Primary role:** Backend / Security

#### Objective
Expose the endpoints the frontend needs to drive the BFF login/logout flow and read the current session, backed by Keycloak/OIDC rather than local credentials.

#### Requirements Covered
ASDD §19.2 (revised — decision D20), FR-SEC-005, FR-AUTHN error categories (§22).

#### Detailed Description
`GET /api/auth/login` issues an OIDC `ChallengeResult` (redirects the browser to Keycloak's login page), preserving a validated `returnUrl` for the post-login redirect. The OIDC middleware's own callback path (registered in `TASK-IDN-01`) completes the handshake and signs the user into the cookie scheme, then redirects back to the SPA. `GET /api/auth/me` returns the current principal's profile (subject id, display name/email from claims, `eka_platform_role`/`eka_app_role` claims) or `401`. `POST /api/auth/logout` signs out of the cookie scheme **and** redirects to Keycloak's end-session endpoint (front-channel logout) so the Keycloak session itself ends, not just the local cookie. Issue an anti-forgery token (`GET /api/auth/csrf`, double-submit header `X-CSRF-TOKEN`) for state-changing requests, exactly as originally designed.

#### Technical Approach
- Minimal API/controller endpoints wrapping `HttpContext.ChallengeAsync`/`SignOutAsync` for both the cookie and OIDC schemes.
- `IAntiforgery` unchanged from the original design.
- Problem Details on failure (module 32).

#### API or Event Contracts
- `GET /api/auth/login?returnUrl=` → `302` to Keycloak.
- `GET /api/auth/me` → `200 {subjectId,displayName,email,platformRoles[],appRoles[]}` / `401`.
- `POST /api/auth/logout` → `302` to Keycloak end-session, which redirects back to the SPA.
- `GET /api/auth/csrf` → seeds the antiforgery cookie/token.

#### Data Changes
None (no local credential/lockout state — Keycloak owns it).

#### Security and Authorization
HTTP-only + Secure + SameSite cookie (unchanged); CSRF on mutations; no tokens returned to the SPA; `returnUrl` validated against an allow-list to prevent open-redirect.

#### Error and Recovery Behavior
`Authentication` category errors on OIDC failure (e.g. Keycloak unreachable, invalid state); correlation ID on every response.

#### Acceptance Criteria
- Given `GET /api/auth/login`, when called, then the browser is redirected to Keycloak and, after successful authentication there, ends up authenticated with a secure cookie and back on the SPA's `returnUrl`.
- Given a mutation without a CSRF token, when submitted, then the API rejects it.
- Given `POST /api/auth/logout`, when called, then both the local cookie and the Keycloak session end.

#### Test Requirements
- Integration: full OIDC round-trip against a real Keycloak container; CSRF rejection; open-redirect `returnUrl` validation.
- Security: cookie flags asserted; tokens never appear in any API response body.

#### Definition of Done
- [ ] Login/callback (via OIDC middleware)/logout/me endpoints.
- [ ] Secure cookie flags + CSRF enforced.
- [ ] Front-channel Keycloak logout wired.

#### Implementation Notes
No password/lockout/temporary-password logic here — Keycloak owns credential management entirely (decision D20).

#### Out of Scope
Role policies (module 3); local user CRUD (removed).

---

### TASK-IDN-03: Frontend authentication flow and session UX
**Module:** 2 — Identity & Authentication
**Epic:** EPIC-IDN-B
**Story:** STORY-IDN-01
**Priority:** High
**Implementation order:** 15
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-IDN-02, TASK-FND-02
**Blocks:** All authenticated UI
**Primary role:** Frontend

#### Objective
Implement the login entry point (a redirect into the BFF's OIDC challenge, never an in-app credential form), auth context/guarded routes, logout, and 401-driven session expiry handling.

#### Requirements Covered
ASDD §7.2 (`/login`), §19.2 (revised — decision D20), FR-SEC-005.

#### Detailed Description
Auth context loads `/api/auth/me` on boot; protected routes redirect unauthenticated users to `/login`. The `/login` screen's only action is a "Sign in" button that does a full-page navigation to `/api/auth/login?returnUrl=...` — credentials are entered on Keycloak's own hosted login page and never seen by the SPA. After the round-trip, the browser lands back on the SPA already authenticated and the auth context re-fetches `/api/auth/me`. Global fetch interceptor: on `401`, clear auth state and redirect to `/login`. Attach the CSRF header to mutations. Show role-appropriate navigation (finalized in module 3) using the `eka_platform_role`/`eka_app_role` claims returned by `/me`.

#### Technical Approach
- `useAuth()` context + `<RequireAuth>` wrapper.
- TanStack Query for `me`; invalidate on login/logout.

#### API or Event Contracts
Consumes IDN-02 endpoints.

#### Data Changes
Not applicable.

#### Security and Authorization
No credential storage in localStorage; cookie-only; CSRF header attached; the SPA never handles a password field.

#### Error and Recovery Behavior
401 → redirect; display safe error messages from Problem Details.

#### Acceptance Criteria
- Given an unauthenticated user, when visiting a protected route, then they are redirected to `/login`.
- Given a session expiry (401), when any request fails, then the app returns the user to login without a crash.
- Given the "Sign in" action, when clicked, then the browser navigates to Keycloak's hosted login (not an in-app form).

#### Test Requirements
- Component/integration tests with mocked endpoints for the login redirect, guard redirect, and 401 handling.

#### Definition of Done
- [ ] Login redirect entry point, guarded routes, logout, 401 handling.

#### Implementation Notes
Keep nav visibility as UX only — authorization is server-side (FR-3.5). Password reset/change screens are Keycloak's own hosted UI, entirely outside the SPA's control.

#### Out of Scope
Role-based menu rules (module 3); Keycloak's own login/credential-reset screens.

---

### TASK-IDN-04: Authentication audit events
**Module:** 2 — Identity & Authentication
**Epic:** EPIC-IDN-C
**Story:** STORY-IDN-01
**Priority:** Medium
**Implementation order:** 16
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-IDN-02, TASK-AUD-01
**Blocks:** None
**Primary role:** Backend / Security

#### Objective
Emit audit events for login success/failure, logout, and OIDC handshake failure without storing secrets or tokens.

#### Requirements Covered
FR-17 (login success/failure), FR-SEC-002, ASDD §17 audit list.

#### Detailed Description
On each auth outcome (`OnTokenValidated`, `OnRemoteFailure`, the explicit logout endpoint), write an `AuditEvent` (timestamp, user or attempted subject/email hash, action, outcome, correlation ID, IP if available). Never store passwords or tokens. Failed logins (including Keycloak-side lockout, which is opaque to the app — observed only as a failed callback) record outcome=failure with a safe reason category.

#### Technical Approach
- Hook into the OIDC events and the explicit logout endpoint (`TASK-IDN-02`) to call the audit writer (module 30).

#### API or Event Contracts
Not applicable (internal).

#### Data Changes
Inserts `audit.audit_events` rows.

#### Security and Authorization
No secrets/tokens logged; store attempted identifier minimally (hash/partial) per policy.

#### Error and Recovery Behavior
Audit write failure must not block auth; log and continue.

#### Acceptance Criteria
- Given a login attempt, when it succeeds or fails, then an audit event is recorded with outcome and correlation ID.
- Given any auth audit row, when inspected, then it contains no password/token/secret.

#### Test Requirements
- Unit/integration: audit rows created on each auth outcome; no secret fields.

#### Definition of Done
- [ ] Auth audit events for login/logout/OIDC failure.

#### Implementation Notes
Depends on the audit writer (module 30); can be delivered once AUD-01 exists.

#### Out of Scope
Audit UI/search (module 30).

---

# PHASE 2 — Authorization & Applications

## MODULE 3 — Role-Based Authorization (AUTHZ)

**Epics**

- EPIC-AUTHZ-A: Roles, policies & membership model
- EPIC-AUTHZ-B: Resource-based authorization & enforcement
- EPIC-AUTHZ-C: Isolation & SignalR-group authorization tests

### STORY-AUTHZ-01: Fixed roles, policies, and resource-based authorization

**As a:** platform operator
**I want:** four fixed roles enforced by backend policies and per-resource application-boundary checks
**So that:** users can only do what their role and application membership permit (FR-3, FR-SEC-004)

#### Business Value
Core security guarantee; prevents cross-application data exposure and privilege escalation.

#### Story Acceptance Criteria
- Policies `CanViewApplication … CanViewAudit` (ASDD §19.3) exist and are enforced on every relevant endpoint.
- Every resource (file/agent/run/output/citation) is authorized against its `ApplicationId` + caller's `eka_platform_role`/`eka_app_role` claims.
- Frontend navigation reflects roles but is never the control point.

#### Dependencies
STORY-IDN-01 (Keycloak claims).

#### Tasks
`TASK-AUTHZ-01`, `TASK-AUTHZ-02`, `TASK-AUTHZ-03`, `TASK-AUTHZ-04`

---

### TASK-AUTHZ-01: Define roles, authorization policies, and requirements
**Module:** 3 — Authorization
**Epic:** EPIC-AUTHZ-A
**Story:** STORY-AUTHZ-01
**Priority:** Critical
**Implementation order:** 17
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-IDN-01
**Blocks:** Every protected endpoint
**Primary role:** Backend / Security

#### Objective
Implement the four fixed roles and the ASDD §19.3 named policies with authorization requirements + handlers, sourced entirely from Keycloak-issued claims (decision D20).

#### Requirements Covered
FR-3.1..3.5, ASDD §19.3, FR-SEC-004.

#### Detailed Description
Roles: `SuperAdmin`/`SuperViewer` arrive as the multivalued `eka_platform_role` claim (Keycloak realm roles); `ApplicationAdmin`/`ApplicationViewer` arrive as the multivalued `eka_app_role` claim, each entry formatted `"{applicationId}:{Role}"` (Keycloak user attribute). Register policies: `CanViewApplication`, `CanManageKnowledge`, `CanManageAgents`, `CanExecuteAgent`, `CanViewOutput`, `CanManageOutput`, `CanViewPlatformProcessing`, `CanViewAudit`. Each policy maps to a requirement whose handler checks the platform-role claim and/or parses the `eka_app_role` entries for the target application. `CanExecuteAgent` additionally honors the per-application "viewers may run agents" flag (FR-APP-001). `CanManageUsers` is dropped (no local user CRUD — see Module 5 removal note).

#### Technical Approach
- `IAuthorizationRequirement` + `AuthorizationHandler<TReq, TResource>` where the resource carries `ApplicationId`.
- A `ClaimsPrincipal` extension parses `eka_platform_role`/`eka_app_role` claims (mirrors the `poc-with-ai` claims-parsing helper, minus its tenant-scoping concept).

#### API or Event Contracts
Not applicable (cross-cutting).

#### Data Changes
Not applicable (no local membership table to read — decision D20).

#### Security and Authorization
This IS the enforcement layer; SuperViewer is read-only (deny all mutations); ApplicationViewer limited per FR-3.4.

#### Error and Recovery Behavior
Denials return `403` Problem Details with no resource disclosure (`Authorization` category).

#### Acceptance Criteria
- Given a SuperViewer, when attempting any mutation, then it is denied.
- Given a user whose `eka_app_role` claim only contains `"app-B:ApplicationViewer"`, when calling `CanManageKnowledge` for App A, then denied.
- Given the viewer-execution flag off, when an ApplicationViewer runs an agent, then denied.

#### Test Requirements
- Unit: each policy handler truth table across roles/claim combinations.
- Security: privilege-escalation attempts denied.

#### Definition of Done
- [ ] All eight policies + handlers implemented and registered.
- [ ] Viewer-execution flag honored.

#### Implementation Notes
Keep handlers pure/testable; claims parsing centralized in one extension method, not duplicated per handler.

#### Out of Scope
Applying `[Authorize]` to each endpoint (done within owning modules but coordinated by AUTHZ-02).

---

### TASK-AUTHZ-02: Resource-based authorization helper and endpoint enforcement pattern
**Module:** 3 — Authorization
**Epic:** EPIC-AUTHZ-B
**Story:** STORY-AUTHZ-01
**Priority:** Critical
**Implementation order:** 18
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-AUTHZ-01
**Blocks:** All resource endpoints
**Primary role:** Backend / Security

#### Objective
Provide a reusable resource-authorization service/pattern that loads an entity with its `ApplicationId` and authorizes the caller (via `eka_platform_role`/`eka_app_role` claims) before any read/write.

#### Requirements Covered
ASDD §19.4, FR-SEC-004, FR-APP-002.

#### Detailed Description
Introduce `IResourceAuthorizationService.AuthorizeAsync(user, resourceRef, policy)` that (1) loads the resource's `ApplicationId`, (2) evaluates the relevant policy handler against the caller's claims, (3) returns allow/deny. Establish the convention that every resource query/command first resolves `ApplicationId` and authorizes — never "load globally then filter". Provide analyzers/architecture tests to catch endpoints that query resource tables without an application filter.

#### Technical Approach
- Central helper used by handlers; combined with EF global query filters keyed on `ApplicationId` where feasible.
- Architecture test asserts repository methods require an `applicationId` parameter (module 34).

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Guarantees the application boundary at every access point (ASDD §20.1).

#### Error and Recovery Behavior
Missing/foreign resource → `404`/`403` without leaking existence across applications.

#### Acceptance Criteria
- Given a resource in App A, when a user without App A access requests it, then a `403`/`404` is returned and no metadata leaks.
- Given any resource endpoint, when reviewed, then it authorizes via the shared helper before returning data.

#### Test Requirements
- Security: cross-application access denied for files/agents/runs/outputs/citations.
- Architecture: repositories require `applicationId`.

#### Definition of Done
- [ ] Shared resource-authz helper.
- [ ] Enforcement convention documented + architecture test.

#### Implementation Notes
EF global query filters reduce accidental leakage but do not replace explicit authorization.

#### Out of Scope
SignalR group authorization (AUTHZ-03).

---

### TASK-AUTHZ-03: SignalR group authorization
**Module:** 3 — Authorization
**Epic:** EPIC-AUTHZ-B
**Story:** STORY-AUTHZ-01
**Priority:** High
**Implementation order:** 19
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-AUTHZ-01, TASK-SIG-01
**Blocks:** Realtime dashboards
**Primary role:** Backend / Security

#### Objective
Authorize hub connections and group joins so users only receive events for applications/runs they may access (ASDD §18.2), using `eka_platform_role`/`eka_app_role` claims instead of a membership lookup.

#### Requirements Covered
ASDD §18.2, FR-SEC-004, FR-DSH-003.

#### Detailed Description
On hub connect, require authentication. When a client requests to join `application:{id}` or `run:{id}` or `platform:operations`, the server validates the platform-role/app-role claims before adding the connection to the group. `user:{userId}` groups auto-bind to the authenticated principal. Reject unauthorized joins.

#### Technical Approach
- Hub method `JoinApplication(appId)` validates via membership service; `platform:operations` restricted to SuperAdmin/SuperViewer.

#### API or Event Contracts
Hub methods: `JoinApplication`, `LeaveApplication`, `JoinRun`, `LeaveRun`.

#### Data Changes
Not applicable.

#### Security and Authorization
Group membership authorized server-side; no client-trusted group names.

#### Error and Recovery Behavior
Unauthorized join → hub error result; connection not added to group.

#### Acceptance Criteria
- Given a user without App A access, when requesting to join `application:A`, then the join is rejected and no events are delivered.
- Given a non-admin, when joining `platform:operations`, then rejected.

#### Test Requirements
- Security: unauthorized group-join attempts rejected (module 34).

#### Definition of Done
- [ ] Authorized joins for application/run/operations groups.

#### Implementation Notes
Server derives `user:{id}` group from principal, never from client input.

#### Out of Scope
Event contracts/publishing (module 25).

---

### TASK-AUTHZ-04: Frontend role-aware navigation and capability gating
**Module:** 3 — Authorization
**Epic:** EPIC-AUTHZ-C
**Story:** STORY-AUTHZ-01
**Priority:** Medium
**Implementation order:** 20
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-IDN-03, TASK-AUTHZ-01
**Blocks:** None
**Primary role:** Frontend

#### Objective
Show/hide navigation and actions based on roles/membership as a UX convenience, with all enforcement remaining server-side.

#### Requirements Covered
FR-3.5 (frontend visibility is not an authorization control), FR-AGT-006 (prompt visibility).

#### Detailed Description
Derive capabilities from `me` (platform-role + app-role claims returned in one response — no separate membership query needed). Hide admin-only actions from viewers, hide full prompts from ApplicationViewers (FR-AGT-006), and disable the "run agent" control when viewer-execution is off. Always assume the server may still deny; handle 403 gracefully.

#### Technical Approach
- `useCapabilities(applicationId)` hook deriving from the cached `me` response.

#### API or Event Contracts
Consumes the `me` endpoint (`TASK-IDN-02`).

#### Data Changes
Not applicable.

#### Security and Authorization
UX only; never the control point.

#### Error and Recovery Behavior
403 responses surface a safe "not permitted" message.

#### Acceptance Criteria
- Given an ApplicationViewer, when viewing an agent, then the full prompt is not displayed.
- Given a viewer with execution disabled, when viewing an agent, then the run control is disabled.

#### Test Requirements
- Component tests for capability gating across roles.

#### Definition of Done
- [ ] Role-aware nav + action gating; graceful 403 handling.

#### Implementation Notes
Keep capability logic centralized to avoid drift. The `useCapabilities` result drives visibility in the shared application shell (`TASK-UX-04`) and gates command-palette entries (`TASK-UX-05`).

#### Out of Scope
Backend policy logic (AUTHZ-01/02); shell/command-palette implementation (module 37).

---

## MODULE 4 — Application Management (APP)

**Epics**

- EPIC-APP-A: Application lifecycle (create/edit/archive/restore/delete)
- EPIC-APP-B: Membership & viewer-execution setting
- EPIC-APP-C: Application isolation & overview

### STORY-APP-01: Application lifecycle and isolation

**As a:** Super Admin
**I want:** to create, edit, archive, restore, and delete applications with strict data isolation
**So that:** each application's knowledge, agents, runs, and outputs are separated (FR-APP-001..004, FR-SEC-004)

#### Business Value
Applications are the primary tenancy/isolation boundary; correctness here underpins all security guarantees.

#### Story Acceptance Criteria
- Only Super Admin can create/edit/archive/restore/delete.
- Archived applications are read-only (no uploads/agent changes/new runs).
- Every owned entity carries `ApplicationId`; deletion cascades cleanup jobs.

#### Dependencies
STORY-AUTHZ-01, module 14.

#### Tasks
`TASK-APP-01`, `TASK-APP-02`, `TASK-APP-03`, `TASK-APP-04`, `TASK-APP-05`, `TASK-APP-06`

---

### TASK-APP-01: Application entity, schema, and repository
**Module:** 4 — Applications
**Epic:** EPIC-APP-A
**Story:** STORY-APP-01
**Priority:** Critical
**Implementation order:** 21
**Estimated size:** S
**Can run in parallel:** No
**Dependencies:** TASK-PG-01
**Blocks:** All application-scoped features
**Primary role:** Backend / Database

#### Objective
Define the `Application` aggregate (Id, Name, Description, Status Active/Archived, AllowViewerExecution, CreatedBy/At, timestamps) and repository with EF configuration.

#### Requirements Covered
FR-APP-001, FR-APP-002, ASDD §9.

#### Detailed Description
Create the `platform.applications` table and EF entity with valid state transitions (Active↔Archived, →Deleted soft state). Repository exposes create/get/list/update with `applicationId` scoping. Enforce name constraints and uniqueness policy per platform rules.

#### Technical Approach
- Domain entity with methods `Archive()`, `Restore()`, `Delete()` guarding transitions.

#### API or Event Contracts
Not applicable (endpoints in APP-02).

#### Data Changes
Table `platform.applications`; indexes on `status`.

#### Security and Authorization
Writes require SuperAdmin (module 3).

#### Error and Recovery Behavior
Invalid transition → `Conflict` error.

#### Acceptance Criteria
- Given the entity, when archiving an active app, then status becomes Archived; when restoring, Active.
- Given an invalid transition (e.g. restore a non-archived app), then a conflict error is returned.

#### Test Requirements
- Unit: state transition rules.
- Integration: persistence round-trip.

#### Definition of Done
- [ ] Entity + table + repository with guarded transitions.

#### Implementation Notes
Soft-delete keeps historical outputs referenceable.

#### Out of Scope
Endpoints (APP-02), membership (APP-05).

---

### TASK-APP-02: Application CRUD endpoints and lifecycle actions
**Module:** 4 — Applications
**Epic:** EPIC-APP-A
**Story:** STORY-APP-01
**Priority:** Critical
**Implementation order:** 22
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-APP-01, TASK-AUTHZ-02
**Blocks:** Frontend app management, uploads
**Primary role:** Backend

#### Objective
Expose Super-Admin endpoints to create, edit, archive, restore, and delete applications, plus list/get scoped to the caller.

#### Requirements Covered
FR-APP-001, FR-APP-003, FR-APP-005 (delete), ASDD §5.2 (API responsibilities).

#### Detailed Description
CRUD + lifecycle endpoints. Archive makes the app read-only (enforced by a guard checked in upload/agent/run commands). Delete triggers async cleanup (module 15 deletion jobs) and soft-deletes the record. List returns apps the caller may view (SuperAdmin/SuperViewer: all; app users: their memberships).

#### Technical Approach
- Minimal API/controllers; commands via application layer; Problem Details errors.
- Archive/delete emit audit events (module 30).

#### API or Event Contracts
- `POST /api/applications` (SuperAdmin) → `201`.
- `GET /api/applications` → list (scoped).
- `GET /api/applications/{id}` → detail.
- `PUT /api/applications/{id}` → edit.
- `POST /api/applications/{id}/archive` / `/restore` / `DELETE`.

#### Data Changes
Writes to `platform.applications`; delete enqueues cleanup.

#### Security and Authorization
Create/edit/archive/restore/delete: SuperAdmin only. List/get: scoped to membership/platform role.

#### Error and Recovery Behavior
Archived-app mutations elsewhere return `Conflict`; delete is idempotent.

#### Acceptance Criteria
- Given a SuperAdmin, when creating an application, then it is persisted and auditable.
- Given a non-SuperAdmin, when creating an application, then `403`.
- Given an archived application, when uploading a file, then the API rejects with a read-only conflict.

#### Test Requirements
- Integration: CRUD + lifecycle; Security: role enforcement; archived read-only guard.

#### Definition of Done
- [ ] All lifecycle endpoints with authorization + audit.

#### Implementation Notes
Read-only guard should be a shared precondition used by upload/agent/run commands.

#### Out of Scope
Cleanup implementation (module 15).

---

### TASK-APP-03: Application list & overview UI
**Module:** 4 — Applications
**Epic:** EPIC-APP-C
**Story:** STORY-APP-01
**Priority:** High
**Implementation order:** 23
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-APP-02, TASK-AUTHZ-04
**Blocks:** None
**Primary role:** Frontend

#### Objective
Build the platform applications list and per-application overview page (`/platform/applications`, `/applications/:id/overview`).

#### Requirements Covered
ASDD §7.2, FR-APP-004 (overview metrics; detailed dashboard in module 27).

#### Detailed Description
List with create/edit/archive/restore/delete actions gated by role; overview showing name/description/status and quick links to knowledge/agents/runs/outputs. Loading, empty, and error states required.

#### Technical Approach
- TanStack Query for list/detail; optimistic UI optional; confirmation dialogs for archive/delete.

#### API or Event Contracts
Consumes APP-02 endpoints.

#### Data Changes
Not applicable.

#### Security and Authorization
Actions hidden per role but server-enforced.

#### Error and Recovery Behavior
Standard Problem Details surfacing; destructive actions require confirmation.

#### Acceptance Criteria
- Given a SuperAdmin, when opening applications, then all apps list with lifecycle actions.
- Given a SuperViewer, when viewing, then no mutating actions are available and server denies any attempt.

#### Test Requirements
- Component tests for role gating, empty/loading/error states.

#### Definition of Done
- [ ] List + overview with role-aware actions and all UI states.

#### Implementation Notes
Full metric dashboards live in modules 26/27.

#### Out of Scope
Dashboard metrics implementation.

---

### TASK-APP-04: Archive/restore/delete guards and cascade orchestration
**Module:** 4 — Applications
**Epic:** EPIC-APP-A
**Story:** STORY-APP-01
**Priority:** High
**Implementation order:** 24
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-APP-02
**Blocks:** Module 15 deletion
**Primary role:** Backend

#### Objective
Implement the shared read-only guard for archived apps and the orchestration that, on delete, enqueues cleanup of files/vectors/agents/runs.

#### Requirements Covered
FR-APP-003, FR-KNW-008 (cleanup), ASDD §11.3 (deletion queue).

#### Detailed Description
A reusable precondition rejects state-changing operations on archived/deleted apps. On delete, enqueue deletion jobs for each owned file and mark agents disabled; historical outputs are retained (FR-CIT-004).

#### Technical Approach
- Precondition service injected into upload/agent/run/output commands.
- Delete → enqueue `DeletionMessage` per file (module 8/15).

#### API or Event Contracts
Internal enqueues only.

#### Data Changes
Marks app deleted; enqueues cleanup; disables agents.

#### Security and Authorization
SuperAdmin-only trigger.

#### Error and Recovery Behavior
Idempotent enqueue; partial failures retried by workers.

#### Acceptance Criteria
- Given an archived app, when any mutation is attempted, then it is blocked consistently across modules.
- Given a deleted app, when deletion runs, then files/vectors are cleaned and outputs remain with source-unavailable state.

#### Test Requirements
- Integration: archived guard across upload/agent/run; delete cascade enqueues cleanup.

#### Definition of Done
- [ ] Shared archived guard + delete cascade orchestration.

#### Implementation Notes
Keep the guard centralized to avoid inconsistent enforcement.

#### Out of Scope
Deletion worker (module 15).

---

### TASK-APP-05: Application-role claim resolution (Keycloak-backed, no local membership table)
**Module:** 4 — Applications
**Epic:** EPIC-APP-B
**Story:** STORY-APP-01
**Priority:** Critical
**Implementation order:** 25
**Estimated size:** S
**Can run in parallel:** No
**Dependencies:** TASK-APP-01, TASK-IDN-01
**Blocks:** Module 3 handlers
**Primary role:** Backend

#### Objective
Provide a small, read-only helper that parses the caller's `eka_app_role` claim into `(applicationId, role)` pairs for use by authorization handlers and API responses — there is no local membership table and no assign/remove endpoints (decision D20).

#### Requirements Covered
FR-USR-003 (superseded — application-scoped role assignment now lives in Keycloak), FR-APP-002, ASDD §19.4 (revised).

#### Detailed Description
Application-scoped role assignment (`ApplicationAdmin`/`ApplicationViewer` per application) is managed entirely in Keycloak, as a multivalued user attribute `eka_app_role` formatted `"{applicationId}:{Role}"`, exposed to the API as a token/cookie-principal claim (see `TASK-IDN-01`). This task adds a small `ClaimsPrincipal` extension (e.g. `GetApplicationRoles()`) that parses that claim into a lookup used by module 3's authorization handlers and by `GET /api/auth/me` (which already returns the raw claim values). No admin UI or CRUD endpoints are built to add/remove application members — that administration happens in the Keycloak admin console (or realm-import JSON for local dev/tests).

#### Technical Approach
- Pure parsing helper + unit tests; no database access, no endpoints.

#### API or Event Contracts
None (no membership CRUD). `GET /api/auth/me` (TASK-IDN-02) already surfaces `appRoles[]` derived from this claim.

#### Data Changes
None — no `platform.application_memberships` table.

#### Security and Authorization
Read-only parsing of a trusted, IdP-issued claim; no write path exists in the app for this data.

#### Error and Recovery Behavior
Malformed claim entries (unexpected format) are ignored defensively and logged, never thrown, so a bad Keycloak attribute value cannot crash authorization.

#### Acceptance Criteria
- Given a caller whose `eka_app_role` claim contains `"app-1:ApplicationViewer"`, when the helper parses claims, then it resolves scoped viewer access to `app-1` only.
- Given a malformed entry in the claim, when parsed, then it is skipped without throwing.

#### Test Requirements
- Unit: claim-parsing edge cases (empty, malformed, multiple applications).

#### Definition of Done
- [ ] Claims-parsing helper + unit tests; consumed by module 3 handlers and `/api/auth/me`.

#### Implementation Notes
This task replaces the originally planned local `ApplicationMembership` entity/endpoints. Assigning or removing a user's application role is done by editing their Keycloak user attribute (admin console or realm-import JSON), not through this application.

#### Out of Scope
Any local user or membership CRUD (removed — see Module 5 note).

---

### TASK-APP-06: Viewer-execution setting enforcement
**Module:** 4 — Applications
**Epic:** EPIC-APP-B
**Story:** STORY-APP-01
**Priority:** Medium
**Implementation order:** 26
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-APP-02, TASK-AUTHZ-01
**Blocks:** Module 19 execution
**Primary role:** Backend

#### Objective
Wire the per-application "Application Viewers may run active agents" flag into the `CanExecuteAgent` policy and run submission.

#### Requirements Covered
FR-APP-001, FR-3.4, FR-RUN-001.

#### Detailed Description
Persist the flag on the application; the execution policy allows ApplicationViewer to run only active agents when the flag is on. ApplicationAdmin/SuperAdmin always may run.

#### Technical Approach
- Policy handler reads the flag as part of `CanExecuteAgent`.

#### API or Event Contracts
Flag included in application create/edit payloads (APP-02).

#### Data Changes
Column `allow_viewer_execution` on applications.

#### Security and Authorization
Enforced server-side in the execution policy.

#### Error and Recovery Behavior
Denied execution returns `403` with a clear reason.

#### Acceptance Criteria
- Given the flag on, when an ApplicationViewer runs an active agent, then allowed.
- Given the flag off, when the same viewer tries, then denied.

#### Test Requirements
- Unit: policy truth table with flag on/off; Security test.

#### Definition of Done
- [ ] Flag enforced in execution policy + run submission.

#### Implementation Notes
Only active agents are runnable by viewers.

#### Out of Scope
Run pipeline (module 19).

---

## MODULE 5 — User Management (USR) — REMOVED (decision D20)

This module (originally: create user with temporary password, disable/enable + session invalidation, platform/application role assignment, and a filterable user-list UI) is **removed from the backlog**. Per the Keycloak/SSO pivot, user accounts, credentials, and role/attribute assignment (`SuperAdmin`/`SuperViewer` realm roles; `ApplicationAdmin`/`ApplicationViewer` via the `eka_app_role` user attribute) are provisioned and managed directly in Keycloak — via its admin console for real usage, or the checked-in realm-import JSON for local dev/tests (see `TASK-IDN-01`). No local user CRUD, password/lockout policy, or user-list UI is built by this application; the platform's own scope is the knowledge/agent/output pipeline, not identity administration.

`STORY-USR-01` and `TASK-USR-01` through `TASK-USR-04` are void. Any remaining cross-reference elsewhere in this document to those IDs (e.g. in the traceability matrix) should be read as superseded by this note.

Former dependents are updated as follows:
- `STORY-IDN-01` no longer depends on any USR task (see Module 2).
- `TASK-APP-05` no longer models a local membership table; it now only parses the `eka_app_role` claim (see Module 4).
- Downstream tasks that previously listed a USR task as a blocking dependency should treat that dependency as already satisfied (Keycloak claims are available as soon as `TASK-IDN-01`/`TASK-IDN-02` are done).

---


## MODULE 6 — Knowledge File Management (KNW)

**Epics**

- EPIC-KNW-A: Upload & validation
- EPIC-KNW-B: Storage, hashing & duplicate detection
- EPIC-KNW-C: Listing, preview, disable, replace, delete, reprocess

### STORY-KNW-01: Upload and validate TXT/Markdown knowledge files

**As an:** Application Admin
**I want:** to upload one or more `.txt`/`.md` files that are validated, hashed, stored, and queued for processing
**So that:** the application's knowledge can be turned into searchable content (FR-KNW-001..005, FR-PRC-001)

#### Business Value
Entry point of the RAG pipeline; primary-flow step "upload files".

#### Story Acceptance Criteria
- Multiple-file upload; each file independently validated (extension, size, non-empty, encoding, binary detection, safe filename, permission).
- Accepted files stored in private S3 under a server-generated key; content hash computed; duplicates warned and not duplicated by default.
- A processing job is created and an identifier-only SQS message is sent per accepted file.

#### Dependencies
STORY-AUTHZ-01, module 7 (S3), module 8/9 (queue+jobs).

#### Tasks
`TASK-KNW-01`, `TASK-KNW-02`, `TASK-KNW-03`, `TASK-KNW-04`, `TASK-KNW-05`, `TASK-KNW-06`

---

### TASK-KNW-01: KnowledgeFile entity, states, and repository
**Module:** 6 — Knowledge
**Epic:** EPIC-KNW-A
**Story:** STORY-KNW-01
**Priority:** Critical
**Implementation order:** 40
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-PG-01, TASK-APP-01
**Blocks:** Upload, processing, retrieval
**Primary role:** Backend / Database

#### Objective
Define `KnowledgeFile` (ASDD §9.1) with states and replacement fields, plus repository scoped by application.

#### Requirements Covered
FR-KNW-*, FR-PRC-002 (states), ASDD §9.1.

#### Detailed Description
Fields per ASDD §9.1 (Id, ApplicationId, DisplayName, ObjectKey, ContentHash, FileSize, MimeType, Encoding, Status, IsEnabled, UploadedBy/At, LastProcessedAt, ReplacementObjectKey?, ReplacementStatus?). User-facing states: Queued, Processing, Ready, Failed, Disabled, Deleting. Guard valid transitions.

#### Technical Approach
- Domain entity with transition methods; EF config; indexes `(application_id, status, is_enabled)` and `(application_id, content_hash)` (ASDD §10).

#### API or Event Contracts
Not applicable.

#### Data Changes
Table `knowledge.knowledge_files` + indexes.

#### Security and Authorization
All queries scoped by `application_id`.

#### Error and Recovery Behavior
Invalid transition → `Conflict`.

#### Acceptance Criteria
- Given a file, when it completes processing, then Status=Ready and LastProcessedAt is set.
- Given a disabled file, then it is excluded from retrieval (enforced in module 16).

#### Test Requirements
- Unit: transitions; Integration: persistence + indexes.

#### Definition of Done
- [ ] Entity, states, repository, indexes.

#### Implementation Notes
Replacement fields are transient operational state, not versioning (ASDD §9.1).

#### Out of Scope
Replacement logic (module 15).

---

### TASK-KNW-02: File validation service (extension, size, encoding, binary, filename)
**Module:** 6 — Knowledge
**Epic:** EPIC-KNW-A
**Story:** STORY-KNW-01
**Priority:** Critical
**Implementation order:** 41
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-KNW-01
**Blocks:** Upload endpoint
**Primary role:** Backend / Security

#### Objective
Implement pre-storage validation: extension `.txt`/`.md`, configured max size, non-empty, readable text, supported encoding, binary-content detection, safe filename, and application permission.

#### Requirements Covered
FR-KNW-001, FR-KNW-003, FR-SEC-003, failure scenarios (empty/binary/encoding).

#### Detailed Description
A validator inspects the upload stream: reject unsupported extensions, oversized files, empty content, undecodable/unsupported encodings (detect BOM/UTF-8/UTF-16), and files with binary/control-byte signatures despite a `.txt`/`.md` name. Sanitize/validate the display filename (no path traversal). Return a structured validation result with per-file reasons.

#### Technical Approach
- Streaming validation to avoid loading huge files fully; encoding sniff; NUL/control-byte heuristic for binary detection.
- Configurable max size from settings.

#### API or Event Contracts
Not applicable (used by KNW-03).

#### Data Changes
Not applicable.

#### Security and Authorization
Filename sanitization (no traversal); permission check delegated to endpoint; validated before storage.

#### Error and Recovery Behavior
Validation failures are non-retryable (`Validation`) with clear per-file reasons.

#### Acceptance Criteria
- Given a `.exe` renamed to `.txt` (binary), when uploaded, then it is rejected as binary content.
- Given an empty file, then rejected as empty.
- Given an unsupported encoding, then rejected with an encoding reason.

#### Test Requirements
- Unit: extension/size/empty/encoding/binary/filename cases (positive + negative).

#### Definition of Done
- [ ] Validator covering all FR-KNW-003 checks with per-file reasons.

#### Implementation Notes
Keep detection heuristics conservative to avoid rejecting valid UTF text.

#### Out of Scope
Parsing (module 10).

---

### TASK-KNW-03: Multi-file upload endpoint with hashing, storage, and job creation
**Module:** 6 — Knowledge
**Epic:** EPIC-KNW-B
**Story:** STORY-KNW-01
**Priority:** Critical
**Implementation order:** 42
**Estimated size:** L
**Can run in parallel:** No
**Dependencies:** TASK-KNW-02, TASK-S3-02, TASK-JOB-02, TASK-SQS-02
**Blocks:** Processing pipeline
**Primary role:** Backend

#### Objective
Accept multiple files in one request, validate each, compute content hash, store bytes in S3 under a server-generated key, create a processing job, and publish an identifier-only SQS message via the outbox.

#### Requirements Covered
FR-KNW-002, FR-KNW-004, FR-KNW-005, FR-PRC-001, ASDD §11.2 (S3 key), §12.3 (outbox).

#### Detailed Description
For each file: run validation; on success compute SHA-256; check for an identical active hash in the same application (FR-KNW-005) → warn and skip by default (allow override flag); stream bytes to S3 at `applications/{applicationId}/files/{fileId}/source`; create `KnowledgeFile` (Status=Queued) + `ProcessingJob` and enqueue `DocumentProcessingMessage` through the transactional outbox (module 9). Return per-file results (accepted/warned/rejected) with reasons.

#### Technical Approach
- Multipart upload; per-file try/catch producing a batch result.
- Hash computed while streaming to S3.
- Job + outbox row committed in one DB transaction (D1).

#### API or Event Contracts
- `POST /api/applications/{appId}/files` (multipart) → `207`/`200 {results:[{fileName,status,fileId?,reason?}]}`.
- Emits `DocumentProcessingMessage` (via outbox).

#### Data Changes
Inserts `knowledge_files`, `processing_jobs`, outbox rows.

#### Security and Authorization
`CanManageKnowledge` + app boundary; archived-app guard; server-generated keys (FR-SEC-003); size limits enforced.

#### Error and Recovery Behavior
S3 failure → `StorageFailure`; if storage succeeds but job commit fails, no orphan (transaction ordering); duplicate → warning result. Correlation ID on all.

#### Acceptance Criteria
- Given an ApplicationAdmin of App A, when uploading valid `.md`, then it is stored under App A's key and a processing job + queue message are created.
- Given a duplicate active hash, when uploaded, then a warning is returned and no duplicate is created (unless override).
- Given a Viewer of App B, when uploading to App A, then `403` and nothing is stored.

#### Test Requirements
- Integration: multi-file upload → S3 + job + message; duplicate warning; Security: cross-app denial; failure/rollback.

#### Definition of Done
- [ ] Multi-file upload with validation, hashing, S3 storage, job + outbox enqueue, per-file results.

#### Implementation Notes
Recall (docker memory): to reset stale seed/corrupt data during dev, explicit volume removal may be needed.

#### Out of Scope
Worker processing (module 10), replacement (module 15).

---

### TASK-KNW-04: File listing and readable preview
**Module:** 6 — Knowledge
**Epic:** EPIC-KNW-C
**Story:** STORY-KNW-01
**Priority:** High
**Implementation order:** 43
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-KNW-01, TASK-S3-03
**Blocks:** None
**Primary role:** Full Stack

#### Objective
List files with search/status filters and metadata (FR-KNW-009), and provide a readable source preview.

#### Requirements Covered
FR-KNW-009, FR-3.4 (viewer preview), ASDD §7.2.

#### Detailed Description
`GET /api/applications/{appId}/files?search=&status=` returns name, type, size, status, current stage, progress, uploadedBy, uploadedAt, lastProcessedAt. Preview endpoint streams decoded text from S3 for authorized users (viewers may preview readable source).

#### Technical Approach
- List backed by repository; preview downloads from S3 and returns sanitized text (no execution).

#### API or Event Contracts
- `GET /api/applications/{appId}/files` (filtered).
- `GET /api/applications/{appId}/files/{fileId}/preview` → text.

#### Data Changes
Not applicable.

#### Security and Authorization
App boundary; preview requires app membership; object keys server-side only.

#### Error and Recovery Behavior
Missing/deleting file → appropriate status; S3 errors mapped to `StorageFailure`.

#### Acceptance Criteria
- Given an app member, when listing files, then all required columns render with live status.
- Given a viewer, when previewing a ready file, then readable text is shown.
- Given a cross-app request, then denied.

#### Test Requirements
- Integration: list + preview; Security: cross-app denial.

#### Definition of Done
- [ ] File list (filtered) + preview with authorization.

#### Implementation Notes
Live status/progress refreshed via SignalR (module 25/28).

#### Out of Scope
Processing dashboard (module 28).

---

### TASK-KNW-05: Disable, delete, and reprocess file actions
**Module:** 6 — Knowledge
**Epic:** EPIC-KNW-C
**Story:** STORY-KNW-01
**Priority:** High
**Implementation order:** 44
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-KNW-01, TASK-JOB-02, TASK-SQS-02
**Blocks:** Module 15 deletion
**Primary role:** Backend

#### Objective
Implement disable (exclude from retrieval, keep visible), delete (async cleanup of S3/vectors), and reprocess (re-queue processing).

#### Requirements Covered
FR-KNW-007, FR-KNW-008, FR-PRC-007 (retry), ASDD §11.3 (deletion queue).

#### Detailed Description
Disable sets `IsEnabled=false` (Status shown Disabled) and removes from retrieval while remaining admin-visible. Delete sets Status=Deleting and enqueues a `DeletionMessage` to clean S3 object + chunks/vectors; historical outputs remain with source shown unavailable. Reprocess re-creates a processing job for a Ready/Failed file.

#### Technical Approach
- Disable/delete/reprocess commands; delete/reprocess enqueue via outbox (module 9).

#### API or Event Contracts
- `POST …/files/{id}/disable` / `/enable`.
- `DELETE …/files/{id}` (→ Deleting + deletion job).
- `POST …/files/{id}/reprocess`.

#### Data Changes
Status/enabled updates; enqueues deletion/processing jobs.

#### Security and Authorization
`CanManageKnowledge`; confirmation for destructive delete (UI); audited.

#### Error and Recovery Behavior
Deletion is idempotent and retryable; disable/reprocess idempotent.

#### Acceptance Criteria
- Given a disabled file, when retrieval runs, then it is excluded but still visible to admins.
- Given a delete, when confirmed, then Status=Deleting and cleanup jobs run; outputs remain.
- Given a failed file, when reprocessed, then a new processing job is queued.

#### Test Requirements
- Integration: disable/delete/reprocess flows; Security: role/app enforcement; deletion idempotency.

#### Definition of Done
- [ ] Disable/enable/delete/reprocess with cleanup + audit.

#### Implementation Notes
Deletion worker implemented in module 15.

#### Out of Scope
Deletion worker + replacement (module 15).

---

### TASK-KNW-06: Knowledge management UI (upload, list, actions)
**Module:** 6 — Knowledge
**Epic:** EPIC-KNW-C
**Story:** STORY-KNW-01
**Priority:** High
**Implementation order:** 45
**Estimated size:** L
**Can run in parallel:** Yes
**Dependencies:** TASK-KNW-03, TASK-KNW-04, TASK-KNW-05, TASK-AUTHZ-04
**Blocks:** None
**Primary role:** Frontend

#### Objective
Build the knowledge screens (`/applications/:id/knowledge`, `/knowledge/:fileId`) with multi-file upload, per-file status, preview, and admin actions.

#### Requirements Covered
FR-KNW-002/006/007/008/009, ASDD §7.2.

#### Detailed Description
Drag-and-drop multi-file upload with per-file validation feedback and duplicate warnings; file list with search/status filter and live status/progress; detail view with preview and disable/replace/delete/reprocess actions gated by role; confirmations for destructive actions; loading/empty/error states.

#### Technical Approach
- TanStack Query + SignalR invalidation for live status; role-aware actions.

#### API or Event Contracts
Consumes KNW-03/04/05 endpoints; subscribes to file SignalR events.

#### Data Changes
Not applicable.

#### Security and Authorization
Admin actions hidden from viewers (server-enforced); viewers can preview.

#### Error and Recovery Behavior
Surfaces per-file upload results and Problem Details.

#### Acceptance Criteria
- Given an admin, when uploading several files, then per-file accepted/warned/rejected states display and processing status updates live.
- Given a viewer, when opening knowledge, then admin actions are absent and preview works.

#### Test Requirements
- Component: upload results, role gating, live-status handling.

#### Definition of Done
- [ ] Upload + list + detail UI with live status and role-aware actions.

#### Implementation Notes
Replacement UI shares this screen (module 15 finalizes behavior).

#### Out of Scope
Processing dashboard (module 28), replacement backend (module 15).

---

# PHASE 3 — Storage, Messaging & Job Infrastructure

## MODULE 7 — LocalStack S3 Integration (S3)

**Epics**

- EPIC-S3-A: S3 client, bucket config & health
- EPIC-S3-B: Object operations with application-boundary keys

### STORY-S3-01: Private object storage with server-generated keys

**As the:** processing system
**I want:** a private S3 abstraction with server-generated, application-scoped keys
**So that:** file bytes are stored/retrieved securely and cannot be accessed across applications (FR-KNW-004, FR-SEC-003)

#### Business Value
Secure durable storage for source files backing the whole pipeline.

#### Story Acceptance Criteria
- Buckets exist (from FND-08); objects are private; keys are server-generated per ASDD §11.2.
- Upload/download/delete work against LocalStack; health check reports S3 availability.
- Object access requires application authorization; no client-supplied keys.

#### Dependencies
TASK-FND-08.

#### Tasks
`TASK-S3-01`, `TASK-S3-02`, `TASK-S3-03`, `TASK-S3-04`

---

### TASK-S3-01: S3 client configuration and abstraction
**Module:** 7 — S3
**Epic:** EPIC-S3-A
**Story:** STORY-S3-01
**Priority:** Critical
**Implementation order:** 31
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-07
**Blocks:** Upload/preview/delete
**Primary role:** Backend / DevOps

#### Objective
Configure the AWS SDK S3 client for LocalStack (endpoint, path-style, region, fake creds) behind an `IObjectStorage` interface.

#### Requirements Covered
ASDD §11 (S3), §5.2, FR-KNW-004.

#### Detailed Description
`IObjectStorage` with `PutAsync(key,stream,contentType)`, `GetAsync(key)`, `DeleteAsync(key)`, `ExistsAsync(key)`, `CheckHealthAsync()`. Configure `ForcePathStyle=true`, `ServiceURL=http://localstack:4566`. Bucket names from config.

#### Technical Approach
- `AmazonS3Client` with custom `ServiceURL`; DI-registered singleton.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Keys always server-generated; no public ACLs; creds from config only.

#### Error and Recovery Behavior
Transient S3 errors classified retryable (`StorageFailure`); timeouts configured.

#### Acceptance Criteria
- Given the client, when putting/getting/deleting an object, then operations succeed against LocalStack.
- Given LocalStack down, when checking health, then unavailable is reported (not a crash).

#### Test Requirements
- Integration (LocalStack): put/get/delete/exists; health.

#### Definition of Done
- [ ] `IObjectStorage` + LocalStack client + health.

#### Implementation Notes
Path-style is required for LocalStack.

#### Out of Scope
Key generation policy (S3-02).

---

### TASK-S3-02: Application-scoped object key generation and upload
**Module:** 7 — S3
**Epic:** EPIC-S3-B
**Story:** STORY-S3-01
**Priority:** Critical
**Implementation order:** 32
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-S3-01
**Blocks:** TASK-KNW-03
**Primary role:** Backend / Security

#### Objective
Generate canonical, server-side object keys `applications/{applicationId}/files/{fileId}/source` (and replacement keys) and store uploads privately.

#### Requirements Covered
ASDD §11.2, FR-SEC-003, FR-KNW-006 (replacement keys).

#### Detailed Description
A key factory produces source and replacement keys embedding `applicationId`; clients never supply keys. All reads validate the key's `applicationId` matches the authorized application to prevent key manipulation (failure scenario "S3 key manipulation").

#### Technical Approach
- `ObjectKeyFactory.Source(appId,fileId)` / `.Replacement(appId,fileId,replacementId)`.
- Read guard: parse appId from key and compare to authorized app.

#### API or Event Contracts
Not applicable.

#### Data Changes
`ObjectKey` persisted on `KnowledgeFile`.

#### Security and Authorization
Server-generated keys; read guard blocks cross-application key access.

#### Error and Recovery Behavior
Mismatched key/app → authorization failure.

#### Acceptance Criteria
- Given an upload, when stored, then the key matches the app-scoped pattern.
- Given a manipulated key referencing another app, when accessed, then denied.

#### Test Requirements
- Security: key-manipulation denial; Unit: key factory.

#### Definition of Done
- [ ] Key factory + read guard.

#### Implementation Notes
Never trust a key from the client.

#### Out of Scope
Upload endpoint (module 6).

---

### TASK-S3-03: Object download for preview/processing
**Module:** 7 — S3
**Epic:** EPIC-S3-B
**Story:** STORY-S3-01
**Priority:** High
**Implementation order:** 33
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-S3-01, TASK-S3-02
**Blocks:** Processing, preview
**Primary role:** Backend

#### Objective
Provide authorized streaming download used by preview (module 6) and the document worker (module 10).

#### Requirements Covered
FR-KNW (preview), FR-PRC (download source), ASDD §13.

#### Detailed Description
`GetAsync(key)` returns a stream; callers must have resolved the file's application and authorized before download. Support hash verification during processing.

#### Technical Approach
- Stream to consumer; optional hashing wrapper for integrity checks (module 10).

#### API or Event Contracts
Not applicable (internal).

#### Data Changes
Not applicable.

#### Security and Authorization
Authorization enforced by callers + key guard (S3-02).

#### Error and Recovery Behavior
Missing object → not-found; transient → retryable `StorageFailure`.

#### Acceptance Criteria
- Given an authorized preview, when downloading, then decoded text streams back.
- Given the worker, when downloading source, then bytes stream for parsing.

#### Test Requirements
- Integration: download path; missing-object handling.

#### Definition of Done
- [ ] Authorized streaming download available.

#### Implementation Notes
Reuse for replacement bytes (module 15).

#### Out of Scope
Parsing (module 10).

---

### TASK-S3-04: Delete objects and S3 health check
**Module:** 7 — S3
**Epic:** EPIC-S3-A
**Story:** STORY-S3-01
**Priority:** Medium
**Implementation order:** 34
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-S3-01
**Blocks:** Module 15, module 31
**Primary role:** Backend

#### Objective
Implement idempotent object deletion (for file/replacement cleanup) and expose S3 readiness for health checks.

#### Requirements Covered
FR-KNW-008, ASDD §24 (readiness), FR-18.

#### Detailed Description
`DeleteAsync(key)` tolerant of missing objects; `CheckHealthAsync()` performs a lightweight bucket head/list to report availability for readiness.

#### Technical Approach
- Idempotent delete; health via `ListBuckets`/`HeadBucket`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Removes objects during cleanup.

#### Security and Authorization
Key guard applies; server-side only.

#### Error and Recovery Behavior
Delete idempotent; health returns structured status.

#### Acceptance Criteria
- Given a delete of a missing object, then it succeeds (idempotent).
- Given LocalStack up, when health runs, then S3 reports available.

#### Test Requirements
- Integration: delete idempotency; health up/down.

#### Definition of Done
- [ ] Idempotent delete + S3 health.

#### Implementation Notes
Used by deletion jobs and readiness endpoint.

#### Out of Scope
Deletion orchestration (module 15).

---

## MODULE 8 — LocalStack SQS Integration (SQS)

**Epics**

- EPIC-SQS-A: Queue client, contracts & init
- EPIC-SQS-B: Send/receive, DLQ, retry & duplicate handling

### STORY-SQS-01: Reliable identifier-only messaging with DLQs

**As the:** processing system
**I want:** typed SQS producers/consumers with DLQs, retry, and duplicate-safe handling
**So that:** jobs are delivered reliably without putting content in messages (FR-PRC-001, FR-PRC-006, ASDD §11.3, §12)

#### Business Value
Decouples long-running work from HTTP and provides reliable retry/dead-lettering.

#### Story Acceptance Criteria
- Producers send identifier-only messages; consumers are idempotent and handle duplicates.
- DLQs configured with redrive; poison messages land in DLQ after max receives.
- Queue health reported for readiness.

#### Dependencies
TASK-FND-08, TASK-FND-03 (message contracts).

#### Tasks
`TASK-SQS-01`, `TASK-SQS-02`, `TASK-SQS-03`, `TASK-SQS-04`

---

### TASK-SQS-01: SQS client, queue resolution, and health
**Module:** 8 — SQS
**Epic:** EPIC-SQS-A
**Story:** STORY-SQS-01
**Priority:** Critical
**Implementation order:** 35
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-08
**Blocks:** Producers/consumers
**Primary role:** Backend / DevOps

#### Objective
Configure the SQS client for LocalStack, resolve queue URLs by name, and expose queue health/depth for readiness and dashboards.

#### Requirements Covered
ASDD §11.3, §24, FR-18, FR-PRC-001.

#### Detailed Description
`IMessageQueue` with `SendAsync<T>(queue,message)`, `ReceiveAsync`, `DeleteAsync(handle)`, `GetApproximateDepthAsync(queue)`, `CheckHealthAsync()`. Resolve queue URLs from names at startup. Provide DLQ depth for dashboards.

#### Technical Approach
- `AmazonSQSClient` with `ServiceURL=localstack:4566`; cache queue URLs.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Messages carry no secrets/content; server-side only.

#### Error and Recovery Behavior
Transient connect errors retried; recall (docker memory): right after a LocalStack restart the endpoint may be briefly unavailable — consumers must retry connect, and a burst of transient errors shortly after restart should not be treated as a real failure.

#### Acceptance Criteria
- Given the client, when sending/receiving/deleting, then it works against LocalStack.
- Given queue depth query, when called, then approximate counts (incl. DLQ) return.

#### Test Requirements
- Integration: send/receive/delete; depth; health.

#### Definition of Done
- [ ] SQS abstraction + queue resolution + health/depth.

#### Implementation Notes
Expose DLQ depth for platform dashboard (module 26).

#### Out of Scope
Outbox (module 9).

---

### TASK-SQS-02: Typed producers for processing/execution/deletion
**Module:** 8 — SQS
**Epic:** EPIC-SQS-B
**Story:** STORY-SQS-01
**Priority:** Critical
**Implementation order:** 36
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-SQS-01, TASK-FND-03
**Blocks:** Upload, run submission, deletion
**Primary role:** Backend

#### Objective
Provide typed send methods for `DocumentProcessingMessage`, `AgentExecutionMessage`, and `DeletionMessage`, invoked via the outbox (module 9).

#### Requirements Covered
FR-PRC-001, FR-RUN-002, ASDD §11.3.

#### Detailed Description
Serialize identifier-only messages with correlation + attempt + message id. Producers are called by the outbox publisher (not directly from request handlers) to preserve DB/queue consistency (D1).

#### Technical Approach
- Thin wrappers over `SendAsync<T>`; JSON contracts from `Shared`.

#### API or Event Contracts
Queue messages per FND-03.

#### Data Changes
Not applicable (outbox writes the intent row).

#### Security and Authorization
No content/secrets in messages (reflection-tested in FND-03).

#### Error and Recovery Behavior
Send failure handled by outbox publisher (retry/reconcile).

#### Acceptance Criteria
- Given a job, when the outbox publishes, then a well-formed identifier-only message lands on the correct queue.

#### Test Requirements
- Integration: message shape + correct queue routing.

#### Definition of Done
- [ ] Typed producers for all three queues.

#### Implementation Notes
Direct sends from handlers are disallowed (consistency).

#### Out of Scope
Outbox mechanics (module 9).

---

### TASK-SQS-03: Consumer base with idempotency, visibility, and DLQ handling
**Module:** 8 — SQS
**Epic:** EPIC-SQS-B
**Story:** STORY-SQS-01
**Priority:** Critical
**Implementation order:** 37
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-SQS-01, TASK-JOB-03
**Blocks:** Workers (modules 10, 19)
**Primary role:** Backend

#### Objective
Provide a reusable worker consumer loop: long-poll receive, visibility-timeout extension via heartbeat, idempotent processing, delete-on-success, and DLQ after max receives.

#### Requirements Covered
FR-PRC-006 (idempotency), FR-PRC-007 (retry), ASDD §12.1/§12.2, failure scenarios (duplicate delivery, worker crash/lease).

#### Detailed Description
Base consumer receives messages, claims the job atomically (module 9), processes idempotently, extends visibility while working (lease/heartbeat), deletes the message on success, and lets redrive move poison messages to DLQ after `maxReceiveCount`. Duplicate delivery for an already-completed/claimed job is ignored safely.

#### Technical Approach
- Background service loop; visibility extension timer; try/catch → classify (module 9) → retry or abandon.

#### API or Event Contracts
Consumes queue messages.

#### Data Changes
Updates job claim/lease/attempt/heartbeat (module 9).

#### Security and Authorization
Validates message references belong to a real job/application (ASDD §11.3 "SQS job validation").

#### Error and Recovery Behavior
Retryable → let message reappear; non-retryable → mark failed + delete (or send to DLQ per policy); duplicate → no-op.

#### Acceptance Criteria
- Given a duplicate delivery of a completed job, when received, then no duplicate work occurs.
- Given a worker crash mid-processing, when the lease expires, then the message reappears and another worker safely reprocesses.
- Given repeated failures, when max receives reached, then the message lands in the DLQ.

#### Test Requirements
- Integration: duplicate handling, lease expiry re-delivery, DLQ routing.

#### Definition of Done
- [ ] Reusable idempotent consumer with lease + DLQ.

#### Implementation Notes
Recall (docker memory): after a LocalStack restart, expect a brief 503-like window; rerun tests once before concluding failure.

#### Out of Scope
Job claim internals (module 9).

---

### TASK-SQS-04: Retry configuration, DLQ redrive, and queue health surfacing
**Module:** 8 — SQS
**Epic:** EPIC-SQS-B
**Story:** STORY-SQS-01
**Priority:** Medium
**Implementation order:** 38
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-08, TASK-SQS-01
**Blocks:** Dashboards (module 26)
**Primary role:** DevOps / Backend

#### Objective
Finalize redrive policies (`maxReceiveCount`), visibility timeouts, and expose DLQ counts for operations dashboards.

#### Requirements Covered
ASDD §11.3, §21.1 (DLQ counts), FR-18.

#### Detailed Description
Set sensible `maxReceiveCount` and visibility timeouts per queue; ensure DLQ depth is queryable for the platform dashboard and operations health.

#### Technical Approach
- Config-driven values; surfaced via `GetApproximateDepthAsync`.

#### API or Event Contracts
Consumed by operations/dashboards.

#### Data Changes
Not applicable.

#### Security and Authorization
Operations data restricted to platform roles.

#### Error and Recovery Behavior
DLQ visibility enables manual investigation.

#### Acceptance Criteria
- Given a poison message, when it exceeds max receives, then it appears in DLQ and DLQ depth increments in operations data.

#### Test Requirements
- Integration: redrive to DLQ; depth reporting.

#### Definition of Done
- [ ] Redrive + visibility configured; DLQ depth surfaced.

#### Implementation Notes
Keep values in `.env` for tuning.

#### Out of Scope
Dashboard UI (module 26).

---

## MODULE 9 — Background Job Infrastructure (JOB)

**Epics**

- EPIC-JOB-A: Job & run records, states, claims
- EPIC-JOB-B: Outbox, reconciliation & idempotency
- EPIC-JOB-C: Heartbeats, retry classification & recovery

### STORY-JOB-01: Durable, idempotent job execution with DB/queue consistency

**As the:** processing system
**I want:** durable job/run records, atomic claims, an outbox, heartbeats, and retry classification
**So that:** work is never lost or duplicated even with at-least-once delivery and crashes (ASDD §12, FR-PRC-006/007)

#### Business Value
Guarantees reliability of the whole async platform; prevents stuck or duplicated jobs.

#### Story Acceptance Criteria
- Outbox commits job + message intent in one transaction (D1).
- Workers atomically claim jobs; duplicates/expired leases handled safely.
- Retry classification separates retryable vs terminal; heartbeats detect stale workers.

#### Dependencies
STORY-S3-01, STORY-SQS-01, module 14.

#### Tasks
`TASK-JOB-01`, `TASK-JOB-02`, `TASK-JOB-03`, `TASK-JOB-04`, `TASK-JOB-05`, `TASK-JOB-06`

---

### TASK-JOB-01: Processing-job and agent-run record schema
**Module:** 9 — Job Infra
**Epic:** EPIC-JOB-A
**Story:** STORY-JOB-01
**Priority:** Critical
**Implementation order:** 46
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-PG-01
**Blocks:** Upload, workers, dashboards
**Primary role:** Database / Backend

#### Objective
Define `ProcessingJob`, `ProcessingAttempt`, and `AgentRun` tables with states, attempts, worker/lease fields, and correlation IDs.

#### Requirements Covered
ASDD §9, §12.2, FR-PRC-002, FR-PRC-008, FR-RUN-004.

#### Detailed Description
`ProcessingJob` (Id, ApplicationId, FileId, Status, Stage, Progress, WorkerId?, StartedAt?, LeaseExpiresAt?, AttemptNumber, HeartbeatAt?, FailureCategory?, FailureMessage?, CorrelationId). `ProcessingAttempt` records each attempt. `AgentRun` per ASDD §9.4 (snapshots, params JSON, stage, progress, failure). Indexes per ASDD §10.

#### Technical Approach
- EF entities + indexes `processing_jobs(application_id,status,created_at)`, `agent_runs(application_id,status,created_at)`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Tables in `operations`/`execution` schemas + indexes.

#### Security and Authorization
All rows carry `ApplicationId`.

#### Error and Recovery Behavior
Fields support retry/lease/heartbeat logic.

#### Acceptance Criteria
- Given a job/run, when persisted, then all lifecycle fields exist and are queryable by app + status.

#### Test Requirements
- Integration: persistence + indexes.

#### Definition of Done
- [ ] Job/attempt/run schema + indexes.

#### Implementation Notes
Keep run params as JSON columns (requested/effective/retrieval).

#### Out of Scope
Claim logic (JOB-03).

---

### TASK-JOB-02: Job/run creation commands
**Module:** 9 — Job Infra
**Epic:** EPIC-JOB-A
**Story:** STORY-JOB-01
**Priority:** Critical
**Implementation order:** 47
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-JOB-01, TASK-JOB-04
**Blocks:** Upload, run submission
**Primary role:** Backend

#### Objective
Provide commands to create processing jobs and agent runs together with their outbox message intent in one transaction.

#### Requirements Covered
FR-PRC-001, FR-RUN-002, ASDD §12.3.

#### Detailed Description
`CreateProcessingJob` and `CreateAgentRun` insert the record + an outbox row atomically; the outbox publisher later sends the SQS message. Set initial Status=Queued and correlation ID.

#### Technical Approach
- Single EF transaction writing job/run + outbox row.

#### API or Event Contracts
Not applicable (used by modules 6, 19).

#### Data Changes
Inserts job/run + outbox row.

#### Security and Authorization
App boundary; caller authorized upstream.

#### Error and Recovery Behavior
If commit fails, nothing persists; publisher handles send.

#### Acceptance Criteria
- Given a job creation, when committed, then both the job and an outbox row exist atomically.

#### Test Requirements
- Integration: atomic job+outbox creation.

#### Definition of Done
- [ ] Create-job/run commands writing outbox atomically.

#### Implementation Notes
Never send SQS directly from these commands.

#### Out of Scope
Publisher (JOB-04).

---

### TASK-JOB-03: Atomic job claim, lease, and heartbeat
**Module:** 9 — Job Infra
**Epic:** EPIC-JOB-A
**Story:** STORY-JOB-01
**Priority:** Critical
**Implementation order:** 48
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-JOB-01
**Blocks:** Workers (10, 19)
**Primary role:** Backend / Database

#### Objective
Atomically transition a queued job to running (recording worker id, start, lease, attempt, heartbeat) and support heartbeats + lease renewal; ignore already-claimed/completed jobs.

#### Requirements Covered
ASDD §12.2, FR-PRC-006, failure scenarios (worker crash, lease expiration).

#### Detailed Description
`TryClaimAsync(jobId,workerId)` uses a conditional UPDATE (status=Queued→Running) returning success only if it changed a row. Heartbeat updates `HeartbeatAt` + extends `LeaseExpiresAt`. A reaper re-queues jobs whose lease expired (worker died). Claiming an already-completed/claimed job returns false (safe no-op for duplicates).

#### Technical Approach
- `UPDATE … WHERE id=@id AND status='Queued'` optimistic claim; periodic heartbeat; lease-expiry reaper (JOB-06).

#### API or Event Contracts
Not applicable.

#### Data Changes
Updates claim/lease/heartbeat/attempt fields.

#### Security and Authorization
Internal worker operation; app boundary preserved.

#### Error and Recovery Behavior
Duplicate claim → false; crashed worker → lease expiry → re-queue.

#### Acceptance Criteria
- Given two workers, when both receive the same message, then only one claims the job.
- Given a claimed job whose worker dies, when the lease expires, then it becomes re-claimable.

#### Test Requirements
- Integration: concurrent claim; lease-expiry re-queue.

#### Definition of Done
- [ ] Atomic claim + heartbeat + lease renewal.

#### Implementation Notes
Pairs with SQS visibility timeout (module 8).

#### Out of Scope
Reaper scheduling (JOB-06).

---

### TASK-JOB-04: Transactional outbox publisher
**Module:** 9 — Job Infra
**Epic:** EPIC-JOB-B
**Story:** STORY-JOB-01
**Priority:** Critical
**Implementation order:** 49
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-JOB-01, TASK-SQS-02
**Blocks:** All async work
**Primary role:** Backend

#### Objective
Implement the outbox pattern (D1): a background publisher reads unsent outbox rows and sends the corresponding SQS message, marking them delivered.

#### Requirements Covered
ASDD §12.3 (preferred outbox), FR-PRC-001.

#### Detailed Description
Outbox table stores queue name + serialized message + status. A publisher polls unsent rows, sends via typed producers (SQS-02), marks delivered, and retries transient send failures. Guarantees a committed job always eventually gets a message even if SQS was briefly down.

#### Technical Approach
- `operations.outbox` table; publisher background service with backoff; mark delivered after successful send.

#### API or Event Contracts
Not applicable.

#### Data Changes
`operations.outbox` table; status updates.

#### Security and Authorization
Messages identifier-only.

#### Error and Recovery Behavior
Send failure → retry later; idempotent (dedupe by message id at consumer).

#### Acceptance Criteria
- Given a committed job with SQS temporarily down, when SQS recovers, then the publisher delivers the message and marks it delivered.
- Given a delivered row, when polled again, then it is not re-sent (or is safely deduped downstream).

#### Test Requirements
- Integration: outbox delivery incl. SQS-outage recovery.

#### Definition of Done
- [ ] Outbox table + publisher with retry + delivered marking.

#### Implementation Notes
Consumers remain idempotent regardless (belt-and-braces).

#### Out of Scope
Reconciler (JOB-05).

---

### TASK-JOB-05: Reconciler for undelivered jobs
**Module:** 9 — Job Infra
**Epic:** EPIC-JOB-B
**Story:** STORY-JOB-01
**Priority:** Medium
**Implementation order:** 50
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-JOB-04
**Blocks:** None
**Primary role:** Backend

#### Objective
Add a periodic reconciler that republishes queued jobs lacking a delivered marker (ASDD §12.3 acceptable-milestone safety net).

#### Requirements Covered
ASDD §12.3, FR-PRC-007.

#### Detailed Description
Periodically scan for jobs in Queued state older than a threshold whose outbox row is not delivered, and re-enqueue delivery. Backstops the outbox in edge cases.

#### Technical Approach
- Scheduled scan with a grace window; republish via outbox.

#### API or Event Contracts
Not applicable.

#### Data Changes
Updates outbox/job as needed.

#### Security and Authorization
Internal.

#### Error and Recovery Behavior
Idempotent; safe to run repeatedly.

#### Acceptance Criteria
- Given a stuck queued job with no delivered message, when the reconciler runs, then delivery is retried.

#### Test Requirements
- Integration: stuck-job recovery.

#### Definition of Done
- [ ] Periodic reconciler backstop.

#### Implementation Notes
Keep threshold conservative to avoid double-sends.

#### Out of Scope
Primary publishing (JOB-04).

---

### TASK-JOB-06: Retry classification, heartbeat reaper, and failed-job recovery
**Module:** 9 — Job Infra
**Epic:** EPIC-JOB-C
**Story:** STORY-JOB-01
**Priority:** High
**Implementation order:** 51
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-JOB-03
**Blocks:** Workers, dashboards, retry actions
**Primary role:** Backend

#### Objective
Centralize retry classification (retryable vs terminal per ASDD §12.4), a lease-expiry reaper, worker heartbeats, and a manual retry action for failed jobs.

#### Requirements Covered
ASDD §12.4, FR-PRC-007, FR-PRC-008, FR-AOI-005, failure scenarios (throttling/timeout/crash).

#### Detailed Description
A classifier maps exceptions/status to categories: retryable (AOAI throttling/temporary failure, S3 timeout, DB blip, SQS transient) vs terminal (invalid file, unsupported encoding, empty content, context overflow, unauthorized, invalid deployment, embedding-dimension mismatch). The reaper re-queues jobs with expired leases and records `WorkerHeartbeat`. A manual retry endpoint re-queues a Failed job (SuperAdmin/ApplicationAdmin).

#### Technical Approach
- `RetryClassifier` used by both workers; reaper background service; `WorkerHeartbeat` table (ASDD §9).

#### API or Event Contracts
- `POST /api/applications/{appId}/processing/{jobId}/retry` (authorized).
- `POST /api/applications/{appId}/runs/{runId}/retry`.

#### Data Changes
`operations.worker_heartbeats`; job/run status updates.

#### Security and Authorization
Retry restricted to SuperAdmin/ApplicationAdmin.

#### Error and Recovery Behavior
Terminal errors are not auto-retried; manual retry allowed where eligible.

#### Acceptance Criteria
- Given AOAI throttling, when it occurs, then the job is classified retryable and retried within limits.
- Given an invalid file, when processing fails, then it is terminal and not auto-retried.
- Given a Failed job, when an admin retries, then it re-queues.

#### Test Requirements
- Unit: classifier truth table; Integration: reaper re-queue; retry endpoint authorization.

#### Definition of Done
- [ ] Classifier + reaper + heartbeat + manual retry.

#### Implementation Notes
Shared classifier keeps worker behavior consistent.

#### Out of Scope
Dashboard visualization (modules 28/29).

---

# PHASE 4 — Ingestion: Processing & Chunking

## MODULE 10 — TXT & Markdown Processing (PRC)

**Epics**

- EPIC-PRC-A: Document worker & pipeline stages
- EPIC-PRC-B: Text integrity, normalization & parsing

### STORY-PRC-01: Process uploaded files into normalized parsed content

**As the:** document processing worker
**I want:** to download, verify, validate, normalize, and parse TXT/Markdown with meaningful stage updates
**So that:** files become clean structured input for chunking and embedding (FR-PRC-002..004, ASDD §13)

#### Business Value
Transforms raw uploads into structured content; core of the ingestion pipeline.

#### Story Acceptance Criteria
- Worker claims jobs, downloads source, verifies hash/size, validates encoding, normalizes text, and parses TXT/Markdown preserving structure.
- Stage/progress updates are meaningful (not fake) and persisted before SignalR notification.
- Malformed/empty/unsupported inputs fail with terminal errors and clear reasons.

#### Dependencies
STORY-JOB-01, STORY-S3-01, STORY-SQS-01.

#### Tasks
`TASK-PRC-01`, `TASK-PRC-02`, `TASK-PRC-03`, `TASK-PRC-04`

---

### TASK-PRC-01: Document worker host and pipeline orchestration
**Module:** 10 — Processing
**Epic:** EPIC-PRC-A
**Story:** STORY-PRC-01
**Priority:** Critical
**Implementation order:** 52
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-SQS-03, TASK-JOB-03, TASK-JOB-06
**Blocks:** Chunking/embedding
**Primary role:** Backend

#### Objective
Implement the document worker background host that consumes `document-processing` messages and orchestrates the ASDD §13 pipeline with stage/progress persistence.

#### Requirements Covered
ASDD §13, FR-PRC-002, FR-PRC-003, FR-PRC-006, FR-PRC-007.

#### Detailed Description
Consumer loop (from SQS-03) claims the job, then runs: download → verify hash/size → validate encoding → normalize → parse → chunk (module 11) → embed (module 13) → save vectors → validate counts → mark Ready → notify. Each stage persists Status/Stage/Progress to Postgres before emitting a SignalR event (ASDD §18.4 source of truth). Idempotent: re-processing a completed job is a no-op.

#### Technical Approach
- Orchestrator invoking stage services; progress from completed stages + embedding-batch counts (FR-PRC-003).
- Failures classified (JOB-06) → terminal vs retryable.

#### API or Event Contracts
Consumes `DocumentProcessingMessage`; emits `FileStageChanged`/`FileCompleted`/`FileFailed` (module 25).

#### Data Changes
Updates job/file Status/Stage/Progress; writes chunks/vectors (modules 11/13/14).

#### Security and Authorization
Validates the job/file belongs to a real application; no content in logs (FR-SEC/§20.5).

#### Error and Recovery Behavior
Retryable → message reappears; terminal → mark Failed with category + correlation; idempotent duplicates.

#### Acceptance Criteria
- Given a queued file, when processed, then it advances through stages to Ready with meaningful progress.
- Given a duplicate message for a completed file, then no duplicate chunks/vectors are created.
- Given a transient AOAI error, then the job retries; given an invalid file, it fails terminally.

#### Test Requirements
- Integration (LocalStack + Postgres + fake AOAI): full pipeline; duplicate handling; retry vs terminal.

#### Definition of Done
- [ ] Worker host orchestrating all stages with persisted progress + idempotency.

#### Implementation Notes
Persist state before notifying (SignalR is not the source of truth).

#### Out of Scope
Chunking/embedding internals (modules 11/13).

---

### TASK-PRC-02: Source download, hash/size verification, and encoding validation
**Module:** 10 — Processing
**Epic:** EPIC-PRC-B
**Story:** STORY-PRC-01
**Priority:** High
**Implementation order:** 53
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-S3-03, TASK-KNW-02
**Blocks:** Parsing
**Primary role:** Backend

#### Objective
Download source bytes, verify content hash and size against stored metadata, and confirm supported encoding before parsing.

#### Requirements Covered
ASDD §13 (verify hash/size, validate encoding), FR-KNW-003, failure scenarios (encoding).

#### Detailed Description
Stream bytes from S3, recompute hash and compare to `ContentHash`, verify size, and decode using detected/declared encoding; reject undecodable content as terminal. Guards against corrupted/altered objects.

#### Technical Approach
- Hash while streaming; encoding detection (BOM/UTF-8/UTF-16); mismatch → terminal `Validation`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Records verified encoding on the file.

#### Security and Authorization
Key guard (S3-02); no content logged.

#### Error and Recovery Behavior
Hash/size/encoding mismatch → terminal with clear reason.

#### Acceptance Criteria
- Given tampered/corrupt bytes (hash mismatch), when processed, then it fails terminally with an integrity error.
- Given unsupported encoding, then terminal encoding error.

#### Test Requirements
- Unit/Integration: hash mismatch, size mismatch, encoding failure.

#### Definition of Done
- [ ] Download + hash/size + encoding verification.

#### Implementation Notes
Reuse the validator heuristics from KNW-02.

#### Out of Scope
Parsing (PRC-03).

---

### TASK-PRC-03: Text normalization and structure-preserving Markdown/TXT parsing
**Module:** 10 — Processing
**Epic:** EPIC-PRC-B
**Story:** STORY-PRC-01
**Priority:** Critical
**Implementation order:** 54
**Estimated size:** L
**Can run in parallel:** Yes
**Dependencies:** TASK-PRC-02
**Blocks:** Chunking
**Primary role:** Backend

#### Objective
Normalize text (line endings, whitespace, BOM) and parse Markdown preserving headings, paragraphs, lists, tables, block quotes, and code fences with heading paths and source offsets; TXT parsed as plain blocks.

#### Requirements Covered
FR-PRC-004, ASDD §13.1, §15.6 (heading path).

#### Detailed Description
Produce a structured document model: a sequence of blocks each with type, text, heading path (H1>H2>…), and character start/end offsets. Markdown parsed via a GFM-capable parser AST; code fences kept intact (never split); tables/lists/quotes preserved. TXT treated as paragraph blocks. Offsets enable citation surrounding-text later.

#### Technical Approach
- Use `Markdig` (GFM) to build an AST; walk it to emit blocks with heading-path context + offsets.
- Normalize before parsing to keep offsets stable.

#### API or Event Contracts
Not applicable.

#### Data Changes
Produces in-memory blocks consumed by chunking (persisted as chunks in module 11/14).

#### Security and Authorization
Content treated as untrusted data (no execution); no raw HTML interpretation.

#### Error and Recovery Behavior
Empty/whitespace-only after normalization → terminal empty-content error.

#### Acceptance Criteria
- Given Markdown with headings/lists/tables/code fences, when parsed, then structure and heading paths are preserved and code fences are intact.
- Given a plain TXT, then paragraph blocks with offsets are produced.
- Given effectively empty content, then a terminal empty-content error.

#### Test Requirements
- Unit: representative Markdown structures; code-fence integrity; offset correctness; empty content.

#### Definition of Done
- [ ] Normalization + structure-preserving parser emitting blocks with heading paths + offsets.

#### Implementation Notes
Markdig is the recommended GFM parser for .NET.

#### Out of Scope
Chunk sizing (module 11).

---

### TASK-PRC-04: Stage/progress persistence and processing failure handling
**Module:** 10 — Processing
**Epic:** EPIC-PRC-A
**Story:** STORY-PRC-01
**Priority:** High
**Implementation order:** 55
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-PRC-01, TASK-JOB-06
**Blocks:** Processing dashboard
**Primary role:** Backend

#### Objective
Persist meaningful stage/progress transitions and record failure details (stage, category, attempt, correlation) for diagnosis and retry.

#### Requirements Covered
FR-PRC-003, FR-PRC-008, ASDD §21.3.

#### Detailed Description
Update Stage/Progress at each pipeline step and on embedding-batch completion; on failure, record failed stage, safe error summary, attempt count, last attempt time, retry eligibility, recommended action, and correlation ID.

#### Technical Approach
- Progress = completed-stage weight + (completed batches / total batches) for the embedding stage.

#### API or Event Contracts
Feeds `FileStageChanged`/`FileFailed` events (module 25) and processing dashboard (module 28).

#### Data Changes
Updates job/file progress + failure fields; writes `ProcessingAttempt`.

#### Security and Authorization
No content/secrets in failure messages.

#### Error and Recovery Behavior
Retry eligibility derived from classifier (JOB-06).

#### Acceptance Criteria
- Given processing, when stages complete, then progress reflects real completion (not a fake timer).
- Given a failure, then failed stage + category + attempt + correlation are recorded and shown.

#### Test Requirements
- Integration: progress values across stages; failure detail persistence.

#### Definition of Done
- [ ] Meaningful stage/progress + structured failure details.

#### Implementation Notes
No fake continuously-increasing progress (FR-PRC-003).

#### Out of Scope
Dashboard rendering (module 28).

---

## MODULE 11 — Chunking (CHK)

**Epics**

- EPIC-CHK-A: Token-aware, heading-aware chunking
- EPIC-CHK-B: Chunk metadata, hashing & tests

### STORY-CHK-01: Token-aware, structure-respecting chunking

**As the:** processing system
**I want:** to split parsed content into token-bounded, heading-aware chunks that never break code fences
**So that:** retrieval evidence is coherent and within model limits (FR-PRC-004, ASDD §13.1)

#### Business Value
Chunk quality directly determines retrieval and generation quality.

#### Story Acceptance Criteria
- Chunks respect target/max token sizes with optional overlap; prefer heading/paragraph boundaries; never split code fences.
- Each chunk records sequence, heading path, offsets, token count, and content hash.
- Validated against representative documents.

#### Dependencies
STORY-PRC-01, TASK-AOI-04 (token estimator).

#### Tasks
`TASK-CHK-01`, `TASK-CHK-02`, `TASK-CHK-03`

---

### TASK-CHK-01: Token-aware, heading-aware chunker
**Module:** 11 — Chunking
**Epic:** EPIC-CHK-A
**Story:** STORY-CHK-01
**Priority:** Critical
**Implementation order:** 56
**Estimated size:** L
**Can run in parallel:** No
**Dependencies:** TASK-PRC-03, TASK-AOI-04
**Blocks:** Embedding
**Primary role:** Backend

#### Objective
Implement a chunker that groups parsed blocks into token-bounded chunks (target 700, max 1,100, overlap 100 as starting config) preferring heading/paragraph boundaries and never breaking code fences.

#### Requirements Covered
FR-PRC-004, ASDD §13.1.

#### Detailed Description
Walk blocks accumulating tokens until the target; break at the nearest heading/paragraph boundary; enforce the max; if a single block (e.g. a large code fence) exceeds max, keep it whole or split only at safe internal boundaries without breaking fence markers. Apply controlled overlap only when needed for continuity. Preserve heading path per chunk and carry source offsets.

#### Technical Approach
- Token counts from the estimator (AOI-04); boundary preference algorithm; code-fence-aware guard.
- Config-driven target/max/overlap.

#### API or Event Contracts
Not applicable.

#### Data Changes
Produces chunk models (persisted in CHK-02/module 14).

#### Security and Authorization
Content untrusted; no execution.

#### Error and Recovery Behavior
Degenerate inputs handled (single huge block); empty → no chunks (terminal upstream).

#### Acceptance Criteria
- Given a document with headings and code fences, when chunked, then chunks stay within max tokens, prefer boundaries, and never split a code fence.
- Given overlap enabled, then adjacent chunks share the configured overlap.

#### Test Requirements
- Unit: size bounds, boundary preference, code-fence integrity, overlap.

#### Definition of Done
- [ ] Chunker meeting size/boundary/code-fence/overlap rules.

#### Implementation Notes
Defaults are starting values; validate with representative docs (CHK-03).

#### Out of Scope
Persistence (CHK-02).

---

### TASK-CHK-02: Chunk metadata, sequencing, and content hashing
**Module:** 11 — Chunking
**Epic:** EPIC-CHK-B
**Story:** STORY-CHK-01
**Priority:** High
**Implementation order:** 57
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-CHK-01, TASK-PG-02
**Blocks:** Embedding storage
**Primary role:** Backend / Database

#### Objective
Assign sequence numbers, heading paths, offsets, token counts, and per-chunk content hashes, and prepare chunks for persistence (initially inactive until embeddings succeed).

#### Requirements Covered
ASDD §9.2 (KnowledgeChunk), §13.3 (safe replacement inactive chunks), FR-PRC-004.

#### Detailed Description
Populate `KnowledgeChunk` fields (ApplicationId, FileId, SequenceNumber, HeadingPath, Content, TokenCount, CharacterStart/End, ContentHash, IsActive=false initially). Content hash enables idempotent re-processing (skip identical chunks). Chunks are written inactive and activated after embeddings validate (module 13/15).

#### Technical Approach
- SHA-256 per chunk content; sequential numbering per file.

#### API or Event Contracts
Not applicable.

#### Data Changes
Prepares `knowledge.knowledge_chunks` rows (persisted with vectors in module 13/14).

#### Security and Authorization
App boundary on every chunk.

#### Error and Recovery Behavior
Idempotency via content hash (FR-PRC-006).

#### Acceptance Criteria
- Given chunks, when persisted, then each has sequence, heading path, offsets, token count, and hash, and starts inactive.
- Given re-processing identical content, then no duplicate active chunks are produced.

#### Test Requirements
- Unit: metadata + hashing; Integration: inactive-then-active flow.

#### Definition of Done
- [ ] Chunk metadata + hashing + inactive-until-embedded.

#### Implementation Notes
Inactive chunks underpin safe replacement (module 15).

#### Out of Scope
Embedding + vector columns (module 13/14).

---

### TASK-CHK-03: Chunking unit + representative-document tests
**Module:** 11 — Chunking
**Epic:** EPIC-CHK-B
**Story:** STORY-CHK-01
**Priority:** Medium
**Implementation order:** 58
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-CHK-01, TASK-CHK-02
**Blocks:** None
**Primary role:** QA / Backend

#### Objective
Validate chunking behavior and defaults against representative TXT/Markdown fixtures.

#### Requirements Covered
FR-PRC-004, ASDD §13.1 (validate defaults), §25.1.

#### Detailed Description
Build fixtures (headings, nested lists, tables, long code fences, mixed content) and assert size bounds, boundary preference, code-fence integrity, heading-path correctness, and offset accuracy. Tune defaults if representative data indicates.

#### Technical Approach
- Golden-file/property tests; document tuning outcomes.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Not applicable.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given representative fixtures, when chunked, then all invariants hold and defaults are validated/adjusted with rationale.

#### Test Requirements
- Unit/golden tests across fixtures.

#### Definition of Done
- [ ] Fixture suite validating chunking + documented defaults.

#### Implementation Notes
Keep fixtures small but structurally diverse.

#### Out of Scope
Retrieval-quality tests (module 34).

---

# PHASE 5 — Vectors: Azure OpenAI, Embeddings & pgvector

## MODULE 12 — Azure OpenAI Configuration (AOI)

**Epics**

- EPIC-AOI-A: Configuration, deployment registry & auth
- EPIC-AOI-B: Client adapters, resilience & token estimation

### STORY-AOI-01: Configured, resilient Azure OpenAI access via internal interfaces

**As the:** platform
**I want:** configured chat/embedding deployments accessed through resilient internal interfaces with capability metadata
**So that:** embeddings and generation are reliable, secure, and provider-abstracted (FR-AOI-*, ASDD §14)

#### Business Value
Only model provider in the baseline; correctness/resilience here gates embeddings, retrieval, and generation.

#### Story Acceptance Criteria
- Endpoint, deployments, auth mode, and limits are configured and validated at startup; secrets stay server-side.
- `IEmbeddingService`, `ITextGenerationService`, `ITokenEstimator` implemented with retries/timeouts and safe error mapping.
- A configuration-based deployment registry exposes capabilities; availability checks report reachability.

#### Dependencies
TASK-FND-07.

#### Tasks
`TASK-AOI-01`, `TASK-AOI-02`, `TASK-AOI-03`, `TASK-AOI-04`, `TASK-AOI-05`

---

### TASK-AOI-01: Configuration binding, validation, and deployment registry
**Module:** 12 — Azure OpenAI
**Epic:** EPIC-AOI-A
**Story:** STORY-AOI-01
**Priority:** Critical
**Implementation order:** 59
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-FND-07
**Blocks:** Embeddings, generation, agent params
**Primary role:** Backend / Security

#### Objective
Bind and validate Azure OpenAI configuration (endpoint, auth mode, chat/embedding deployment names, context/output limits, embedding dimensions, timeout, retry count) and expose a configuration-based deployment registry with capabilities.

#### Requirements Covered
FR-AOI-001, FR-AOI-003, FR-PAR-004, ASDD §14.1, §14.4.

#### Detailed Description
Strongly-typed options validated on startup (fail fast on missing required values). The deployment registry lists entries: friendly name, Azure deployment name, purpose (embedding|generation), enabled, context limit, max output, supported parameters (e.g. TopP support), embedding dimensions. Used by agent parameter validation (module 17) and context budgeting (module 20).

#### Technical Approach
- `IOptions` + `IValidateOptions`; registry from config section; expose read model for the frontend (non-secret).

#### API or Event Contracts
- `GET /api/deployments` (authorized) → non-secret registry for agent editor.

#### Data Changes
Not applicable (config-based, D9 no admin module).

#### Security and Authorization
Secrets never in the registry response; endpoint restricted to authorized roles.

#### Error and Recovery Behavior
Startup fails on invalid required config; missing deployment → `Configuration` error at run.

#### Acceptance Criteria
- Given complete config, when the app starts, then options validate and the registry lists deployments with capabilities.
- Given the registry endpoint, when called, then no secrets are present.

#### Test Requirements
- Unit: options validation; Integration: registry endpoint excludes secrets.

#### Definition of Done
- [ ] Validated options + capability registry + non-secret endpoint.

#### Implementation Notes
Registry is configuration-based (ASDD §14.4) — no complex admin module.

#### Out of Scope
Client calls (AOI-02/03).

---

### TASK-AOI-02: Authentication (Entra ID preferred, API key fallback)
**Module:** 12 — Azure OpenAI
**Epic:** EPIC-AOI-A
**Story:** STORY-AOI-01
**Priority:** Critical
**Implementation order:** 60
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-AOI-01
**Blocks:** Client adapters
**Primary role:** Backend / Security

#### Objective
Support `DefaultAzureCredential` (Entra ID) as preferred auth and server-side API-key fallback for local dev; credentials never reach the frontend or persisted tables.

#### Requirements Covered
FR-AOI-002, ASDD §14.2, §20.4, D6.

#### Detailed Description
Auth mode from config: `entra` uses `DefaultAzureCredential`; `apikey` uses a server-side secret. Credentials sourced from env/user-secrets/Docker secrets only. Never logged, never returned in any API/SignalR payload.

#### Technical Approach
- Azure OpenAI .NET client configured per mode; token credential vs key credential.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable (no credentials persisted).

#### Security and Authorization
Server-side only; excluded from logs and responses.

#### Error and Recovery Behavior
Auth failure → `Configuration`/`DependencyUnavailable` mapped safely; failure scenario "AOAI auth failure" covered.

#### Acceptance Criteria
- Given `entra` mode, when calling AOAI, then `DefaultAzureCredential` is used.
- Given `apikey` mode, when calling AOAI, then a server-side key is used and never appears in any response/log.

#### Test Requirements
- Unit: credential selection; Security: no secret in logs/responses.

#### Definition of Done
- [ ] Both auth modes; secrets server-side only.

#### Implementation Notes
Prefer Entra for anything beyond quick local dev.

#### Out of Scope
Client request logic (AOI-03).

---

### TASK-AOI-03: Client adapters with retry, timeout, and safe error mapping
**Module:** 12 — Azure OpenAI
**Epic:** EPIC-AOI-B
**Story:** STORY-AOI-01
**Priority:** Critical
**Implementation order:** 61
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-AOI-02
**Blocks:** Embeddings, generation
**Primary role:** Backend

#### Objective
Implement `IEmbeddingService` and `ITextGenerationService` over the Azure OpenAI client with bounded exponential retry (honoring server retry-after), explicit timeouts, and mapping to error categories; plus `CheckHealthAsync`.

#### Requirements Covered
FR-AOI-005, ASDD §14.3, §14.5, §14.6, §22, failure scenarios (throttling/timeout/invalid deployment).

#### Detailed Description
Retry only transient/throttle (429/5xx/timeouts) respecting `Retry-After`; do not retry invalid requests or context overflow. Map failures to `Throttled`, `Timeout`, `DependencyUnavailable`, `Configuration` (invalid deployment), `ContextOverflow`. Health checks perform minimal reachability probes.

#### Technical Approach
- Polly-style retry with jitter; per-call timeout; classifier feeds JOB-06.

#### API or Event Contracts
Internal interfaces: `GenerateEmbeddingsAsync(inputs)`, `GenerateAsync(request)`, `CheckHealthAsync()`.

#### Data Changes
Not applicable.

#### Security and Authorization
Raw AOAI responses/stack traces stay in restricted logs (§22).

#### Error and Recovery Behavior
Retry transient with backoff + retry-after; terminal on invalid/overflow; timeouts enforced.

#### Acceptance Criteria
- Given a 429 with retry-after, when calling, then the client backs off and retries within limits.
- Given an invalid deployment, then a terminal `Configuration` error (no retry).
- Given a timeout, then it is surfaced as `Timeout` and the job remains durable.

#### Test Requirements
- Unit: retry/timeout/mapping against fake AOAI; Integration: health.

#### Definition of Done
- [ ] Embedding + generation adapters with resilience + safe error mapping + health.

#### Implementation Notes
Respect server-provided retry delay; never retry context overflow.

#### Out of Scope
Batching (module 13), prompt assembly (module 20).

---

### TASK-AOI-04: Token estimator
**Module:** 12 — Azure OpenAI
**Epic:** EPIC-AOI-B
**Story:** STORY-AOI-01
**Priority:** High
**Implementation order:** 62
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-AOI-01
**Blocks:** Chunking, budgeting, agent editor
**Primary role:** Backend

#### Objective
Implement `ITokenEstimator.Estimate(text, deployment)` for chunk sizing, context budgeting, and the agent prompt token display.

#### Requirements Covered
FR-AGT-003 (token display), FR-RET-006 (budget), ASDD §14.3, §15.5.

#### Detailed Description
Provide a deployment-aware token estimate (tokenizer-based where possible, else a calibrated heuristic). Used by chunker (module 11), context budget (module 20), and the frontend token counter (module 17).

#### Technical Approach
- Tokenizer library aligned to the model family; fallback heuristic with documented margin.

#### API or Event Contracts
- Optionally `POST /api/token-estimate {text,deployment}` for the editor (or client-side estimate with server authority at run).

#### Data Changes
Not applicable.

#### Security and Authorization
No content logged.

#### Error and Recovery Behavior
Estimation errors degrade gracefully to heuristic.

#### Acceptance Criteria
- Given text + deployment, when estimating, then a stable token count returns used consistently by chunking and budgeting.

#### Test Requirements
- Unit: estimate stability + boundary behavior.

#### Definition of Done
- [ ] Token estimator used across chunking/budget/editor.

#### Implementation Notes
Server estimate is authoritative for budgeting even if the editor estimates client-side.

#### Out of Scope
Budget computation (module 20).

---

### TASK-AOI-05: Availability checks and operations reporting
**Module:** 12 — Azure OpenAI
**Epic:** EPIC-AOI-B
**Story:** STORY-AOI-01
**Priority:** Medium
**Implementation order:** 63
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-AOI-03
**Blocks:** Module 31
**Primary role:** Backend

#### Objective
Expose embedding/generation deployment reachability for the operations view without consuming large quota.

#### Requirements Covered
FR-AOI-004, ASDD §24 (external dependency health).

#### Detailed Description
Lightweight, user-initiated/scheduled-sparingly reachability checks per deployment surfaced to the operations page (module 31).

#### Technical Approach
- Minimal probe; cache short-lived result to avoid quota use.

#### API or Event Contracts
- `GET /api/operations/aoai-health` (platform roles) → per-deployment status.

#### Data Changes
Not applicable.

#### Security and Authorization
Restricted to platform roles.

#### Error and Recovery Behavior
Unreachable reported clearly; no crash.

#### Acceptance Criteria
- Given a reachable deployment, when checked, then it reports available; given misconfig, unavailable.

#### Test Requirements
- Integration against fake AOAI (up/down).

#### Definition of Done
- [ ] Availability endpoint for operations.

#### Implementation Notes
Deep checks should be sparing to avoid model usage (ASDD §24).

#### Out of Scope
Operations UI (module 31).

---

## MODULE 13 — Embedding Pipeline (EMB)

**Epics**

- EPIC-EMB-A: Batching, concurrency & progress
- EPIC-EMB-B: Response validation & vector persistence

### STORY-EMB-01: Batch-embed chunks and persist validated vectors

**As the:** document processing worker
**I want:** to embed chunks in bounded batches, validate returned vectors, and store them in pgvector
**So that:** knowledge becomes searchable with correct dimensions and no corrupt vectors (FR-PRC-005, ASDD §13.2)

#### Business Value
Produces the vector index that powers retrieval; validation prevents silent corruption.

#### Story Acceptance Criteria
- Chunks embedded in batches within AOAI limits with bounded concurrency; progress from completed batches.
- Vector count and dimensions validated; non-finite vectors rejected.
- Vectors + embedding metadata stored; chunks activated only after validation.

#### Dependencies
STORY-AOI-01, STORY-CHK-01, TASK-PG-02.

#### Tasks
`TASK-EMB-01`, `TASK-EMB-02`, `TASK-EMB-03`

---

### TASK-EMB-01: Batch construction, bounded concurrency, and progress
**Module:** 13 — Embeddings
**Epic:** EPIC-EMB-A
**Story:** STORY-EMB-01
**Priority:** Critical
**Implementation order:** 64
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-AOI-03, TASK-CHK-02
**Blocks:** Vector persistence
**Primary role:** Backend

#### Objective
Batch chunks within AOAI input/request limits, embed with bounded concurrency, and report progress by completed batches.

#### Requirements Covered
FR-PRC-005, FR-PRC-003, ASDD §13.2.

#### Detailed Description
Compute batches respecting max inputs per request and token limits; process with a bounded degree of parallelism; update job progress as batches complete; retry transient batch failures (AOI-03) without duplicating stored vectors.

#### Technical Approach
- Batch planner + `SemaphoreSlim`-bounded concurrency; progress = completed/total batches.

#### API or Event Contracts
Not applicable.

#### Data Changes
Updates job progress; produces vectors for persistence (EMB-03).

#### Security and Authorization
No content in logs.

#### Error and Recovery Behavior
Transient batch failure retried; terminal (dimension mismatch) fails job.

#### Acceptance Criteria
- Given many chunks, when embedding, then batches respect limits, concurrency is bounded, and progress advances per batch.

#### Test Requirements
- Unit: batch planning + concurrency; Integration with fake AOAI: progress.

#### Definition of Done
- [ ] Batching + bounded concurrency + batch progress.

#### Implementation Notes
Progress feeds FR-PRC-003 meaningful progress.

#### Out of Scope
Vector validation (EMB-02).

---

### TASK-EMB-02: Embedding response validation
**Module:** 13 — Embeddings
**Epic:** EPIC-EMB-B
**Story:** STORY-EMB-01
**Priority:** Critical
**Implementation order:** 65
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-EMB-01, TASK-AOI-01
**Blocks:** Persistence
**Primary role:** Backend

#### Objective
Validate returned vector count matches inputs, dimensions match the configured embedding dimensions, and reject non-finite values.

#### Requirements Covered
FR-PRC-005, ASDD §13.2, failure scenario "embedding-dimension mismatch".

#### Detailed Description
After each batch, assert one vector per input, each with the expected dimension count and all-finite values; on mismatch, fail the job terminally (dimension mismatch is non-retryable per ASDD §12.4).

#### Technical Approach
- Validation guard before persistence; record `EmbeddingDeployment`/`EmbeddingDimensions`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Records embedding metadata on chunks.

#### Security and Authorization
No vector/content logging by default.

#### Error and Recovery Behavior
Dimension mismatch/non-finite → terminal `EmbeddingFailure`.

#### Acceptance Criteria
- Given a batch returning wrong dimensions, when validated, then the job fails terminally with a dimension-mismatch error.
- Given non-finite values, then rejected.

#### Test Requirements
- Unit: count/dimension/finite checks; Integration: mismatch handling.

#### Definition of Done
- [ ] Count/dimension/finite validation with terminal failure on mismatch.

#### Implementation Notes
Guards D10 (embedding config integrity).

#### Out of Scope
Persistence (EMB-03).

---

### TASK-EMB-03: Vector persistence and chunk activation
**Module:** 13 — Embeddings
**Epic:** EPIC-EMB-B
**Story:** STORY-EMB-01
**Priority:** Critical
**Implementation order:** 66
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-EMB-02, TASK-PG-02
**Blocks:** Retrieval, replacement
**Primary role:** Backend / Database

#### Objective
Persist validated vectors + embedding metadata to `knowledge_chunks` and activate chunks (or hold inactive for replacement), maintaining idempotency.

#### Requirements Covered
FR-PRC-005, FR-PRC-006, ASDD §9.2, §13.3.

#### Detailed Description
Store `Embedding` (pgvector), `EmbeddingDeployment`, `EmbeddingDimensions`, and populate `SearchVector` (module 14 FTS). For normal processing, activate chunks in a transaction after all batches validate; for replacement, keep inactive until the safe activation step (module 15). Idempotent via content hashes.

#### Technical Approach
- Bulk insert; single transaction to flip `IsActive` after full success.

#### API or Event Contracts
Not applicable.

#### Data Changes
Writes vectors + FTS + activation to `knowledge_chunks`.

#### Security and Authorization
App boundary on all rows.

#### Error and Recovery Behavior
Partial failure leaves chunks inactive (not searchable); re-run idempotent.

#### Acceptance Criteria
- Given validated embeddings, when persisted, then chunks become active with correct vectors + FTS and are retrievable.
- Given a mid-way failure, then no partially-active corpus is searchable.

#### Test Requirements
- Integration: persist + activate; failure leaves inactive; idempotent re-run.

#### Definition of Done
- [ ] Vector + FTS persistence with transactional activation + idempotency.

#### Implementation Notes
Activation transaction is the linchpin of safe replacement.

#### Out of Scope
Replacement swap (module 15).

---

## MODULE 14 — PostgreSQL & pgvector (PG)

**Epics**

- EPIC-PG-A: Schemas, DbContext, migrations & pgvector
- EPIC-PG-B: Vector + FTS queries, indexes, backup/restore

### STORY-PG-01: Authoritative relational + vector + full-text store

**As the:** platform
**I want:** schemas, EF Core mappings, pgvector column, FTS, and indexes with migrations and backup/restore
**So that:** PostgreSQL is the reliable source of truth for state, vectors, and search (ASDD §10, FR-RET)

#### Business Value
Central persistence; correctness/indexing here affects every feature and retrieval latency.

#### Story Acceptance Criteria
- All schemas/tables/indexes from ASDD §10 created via migrations; pgvector extension enabled.
- Cosine similarity + FTS queries scoped by application; exact-search baseline.
- Backup/restore scripts exist.

#### Dependencies
TASK-FND-01, TASK-FND-09.

#### Tasks
`TASK-PG-01`, `TASK-PG-02`, `TASK-PG-03`, `TASK-PG-04`, `TASK-PG-05`, `TASK-PG-06`

---

### TASK-PG-01: DbContext, schemas, and base migrations
**Module:** 14 — PostgreSQL
**Epic:** EPIC-PG-A
**Story:** STORY-PG-01
**Priority:** Critical
**Implementation order:** 12b
**Estimated size:** L
**Can run in parallel:** No
**Dependencies:** TASK-FND-01
**Blocks:** All persistence
**Primary role:** Database / Backend

#### Objective
Create the EF Core `DbContext`(s), schemas (`platform, knowledge, agents, execution, outputs, operations, audit`), and the base migration set enabling pgvector.

#### Requirements Covered
ASDD §10, §9 (data model), FR-19.

#### Detailed Description
Define entities incrementally per owning module but centralize the DbContext + migration pipeline here. First migration creates schemas, `CREATE EXTENSION vector`, and the `platform` tables. Npgsql + pgvector-dotnet mapping registered. There is no local `identity` schema (decision D20) — Keycloak owns all credential/account data in its own separate `keycloak-postgres` database, entirely outside this DbContext.

#### Technical Approach
- `UseNpgsql` + `UseVector()`; per-schema `ToTable(..., schema)`; migrations checked in.

#### API or Event Contracts
Not applicable.

#### Data Changes
Creates schemas + extension + base tables.

#### Security and Authorization
Least-privilege DB user documented.

#### Error and Recovery Behavior
Migrations idempotent via EF.

#### Acceptance Criteria
- Given a fresh DB, when migrations apply, then all schemas exist and pgvector is enabled.

#### Test Requirements
- Integration: apply migrations against a Postgres container.

#### Definition of Done
- [ ] DbContext + schemas + pgvector + base migration.

#### Implementation Notes
Other modules add their tables via follow-on migrations.

#### Out of Scope
Module-specific tables (owned by those modules).

---

### TASK-PG-02: KnowledgeChunk vector + FTS column mapping
**Module:** 14 — PostgreSQL
**Epic:** EPIC-PG-A
**Story:** STORY-PG-01
**Priority:** Critical
**Implementation order:** 39b
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-PG-01, TASK-KNW-01
**Blocks:** Embedding persistence, retrieval
**Primary role:** Database

#### Objective
Map the `KnowledgeChunk` `Embedding vector(N)` column and a maintained `tsvector SearchVector` over content + heading path, with supporting indexes.

#### Requirements Covered
ASDD §9.2, §10 (FTS tsvector), FR-RET-004.

#### Detailed Description
Add the vector column (dimension from config), a generated/maintained `tsvector`, and indexes: `knowledge_chunks(application_id, file_id, is_active)` and a GIN index on `SearchVector`. Vector index (HNSW) deferred (D5).

#### Technical Approach
- `vector(N)` column; `tsvector` via generated column or trigger; GIN on FTS.

#### API or Event Contracts
Not applicable.

#### Data Changes
Adds vector + tsvector columns + indexes to `knowledge_chunks`.

#### Security and Authorization
App-scoped index leading column.

#### Error and Recovery Behavior
Migration idempotent.

#### Acceptance Criteria
- Given chunks, when stored, then vector + tsvector populate and app-scoped/FTS indexes exist.

#### Test Requirements
- Integration: column presence + index usage.

#### Definition of Done
- [ ] Vector + FTS columns + indexes.

#### Implementation Notes
Dimension must match embedding config (D10).

#### Out of Scope
Query implementation (PG-06/module 16).

---

### TASK-PG-03: Core relational tables and indexes
**Module:** 14 — PostgreSQL
**Epic:** EPIC-PG-A
**Story:** STORY-PG-01
**Priority:** High
**Implementation order:** 39c
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-PG-01
**Blocks:** Owning modules' persistence
**Primary role:** Database

#### Objective
Provide the migration coordination + essential indexes listed in ASDD §10 across memberships, files, jobs, agents, runs, outputs, and audit.

#### Requirements Covered
ASDD §10 (essential indexes), §9.

#### Detailed Description
Ensure indexes: `knowledge_files(application_id,status,is_enabled)`, `knowledge_files(application_id,content_hash)`, `processing_jobs(application_id,status,created_at)`, `agents(application_id,status)`, `agent_runs(application_id,status,created_at)`, `generated_outputs(application_id,created_at)`, `audit_events(application_id,occurred_at)`. No `application_memberships` index — application-role membership is a Keycloak claim, not a local table (decision D20).

#### Technical Approach
- Consolidated migration reviewing all module tables' indexes.

#### API or Event Contracts
Not applicable.

#### Data Changes
Adds indexes.

#### Security and Authorization
App-scoped leading columns.

#### Error and Recovery Behavior
Idempotent.

#### Acceptance Criteria
- Given the schema, when inspected, then all ASDD §10 indexes exist.

#### Test Requirements
- Integration: index existence assertions.

#### Definition of Done
- [ ] All essential indexes present.

#### Implementation Notes
Coordinate with owning modules to avoid duplicate index definitions.

#### Out of Scope
Query tuning beyond baseline.

---

### TASK-PG-04: FTS configuration and maintenance
**Module:** 14 — PostgreSQL
**Epic:** EPIC-PG-B
**Story:** STORY-PG-01
**Priority:** Medium
**Implementation order:** 39d
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-PG-02
**Blocks:** Hybrid retrieval
**Primary role:** Database

#### Objective
Configure PostgreSQL full-text search (language config, weighting for heading path vs content) and keep `SearchVector` maintained on writes.

#### Requirements Covered
ASDD §10 (FTS), FR-RET-004.

#### Detailed Description
Choose an FTS config; weight heading path higher than body if beneficial; maintain tsvector via generated column/trigger; ensure GIN index used by keyword queries.

#### Technical Approach
- `to_tsvector` with weights; verify plan uses GIN.

#### API or Event Contracts
Not applicable.

#### Data Changes
FTS config + maintenance.

#### Security and Authorization
App-scoped queries only.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given keyword queries, when executed, then they use FTS with sensible ranking and app scoping.

#### Test Requirements
- Integration: FTS ranking + index usage.

#### Definition of Done
- [ ] FTS configured + maintained + indexed.

#### Implementation Notes
Heading-path weighting improves retrieval relevance.

#### Out of Scope
Rank fusion (module 16).

---

### TASK-PG-05: Backup and restore scripts
**Module:** 14 — PostgreSQL
**Epic:** EPIC-PG-B
**Story:** STORY-PG-01
**Priority:** Medium
**Implementation order:** 39e
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-PG-01
**Blocks:** Module 35/36
**Primary role:** DevOps / Database

#### Objective
Provide `pg_dump`/`pg_restore` scripts for local backup/restore of the platform database.

#### Requirements Covered
ASDD §28 (backup scripts), FR-19, acceptance "restarting does not lose state".

#### Detailed Description
Scripts to back up and restore the Postgres volume/database, documented in the runbook; ensure pgvector data restores correctly.

#### Technical Approach
- `pg_dump -Fc` + `pg_restore`; document volume-level backup as alternative.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable (operational).

#### Security and Authorization
Backups may contain data; document safe handling.

#### Error and Recovery Behavior
Restore verified by a round-trip test.

#### Acceptance Criteria
- Given a populated DB, when backed up and restored to a fresh instance, then data (incl. vectors) matches.

#### Test Requirements
- Manual/CI: backup→restore round-trip.

#### Definition of Done
- [ ] Backup + restore scripts + docs.

#### Implementation Notes
Recall (docker memory): named volumes persist unless explicitly removed; document volume reset separately.

#### Out of Scope
Cloud backup.

---

### TASK-PG-06: Cosine similarity query baseline (exact search)
**Module:** 14 — PostgreSQL
**Epic:** EPIC-PG-B
**Story:** STORY-PG-01
**Priority:** Critical
**Implementation order:** 67
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-PG-02, TASK-EMB-03
**Blocks:** Retrieval
**Primary role:** Database / Backend

#### Objective
Implement application-scoped exact cosine similarity queries over active chunks as the retrieval baseline (D5), returning ranked candidates.

#### Requirements Covered
ASDD §10 (exact cosine baseline), §15.2, FR-RET-001/003.

#### Detailed Description
Parameterized query filtering `application_id = @app AND is_active AND file enabled AND status Ready`, ordering by cosine distance (`<=>`), limited to top-K. Never search globally then filter (FR-RET-001). Include a clarification note: introduce HNSW only after latency/corpus justify it (threshold TBD with representative data).

#### Technical Approach
- pgvector `<=>` cosine; app filter joined to `knowledge_files` for enabled/Ready.

#### API or Event Contracts
Not applicable (used by module 16).

#### Data Changes
Read-only.

#### Security and Authorization
Application boundary embedded in the WHERE clause (FR-SEC-004).

#### Error and Recovery Behavior
Empty result handled by retrieval (module 16).

#### Acceptance Criteria
- Given a query embedding for App A, when searching, then only App A's active/enabled/Ready chunks are candidates, ranked by cosine.
- Given a cross-app attempt, then no App B chunks appear.

#### Test Requirements
- Integration: ranking correctness; Security: cross-app isolation in vector search.

#### Definition of Done
- [ ] App-scoped exact cosine query baseline.

#### Implementation Notes
Clarification (D5): HNSW deferred; revisit with measured latency.

#### Out of Scope
Rank fusion/hybrid (module 16).

---

## MODULE 15 — File Replacement & Deletion (REP)

**Epics**

- EPIC-REP-A: Safe replacement (inactive → activate)
- EPIC-REP-B: Deletion worker & cleanup

### STORY-REP-01: Safe replacement and asynchronous deletion

**As an:** Application Admin
**I want:** file replacement that only swaps searchable content after the new content processes successfully, and deletion that cleans storage/vectors asynchronously
**So that:** a failed replacement never destroys working knowledge and deletions are complete (FR-KNW-006, FR-KNW-008, ASDD §13.3)

#### Business Value
Protects knowledge integrity (a core Definition-of-Done item) and keeps storage consistent.

#### Story Acceptance Criteria
- Replacement stores temporary object + inactive chunks; on success one transaction swaps active content and updates S3/hash/metadata; on failure current content remains and temp artifacts are cleaned.
- Deletion removes S3 object + chunks/vectors; historical outputs remain with source-unavailable state.

#### Dependencies
STORY-EMB-01, STORY-JOB-01, STORY-S3-01.

#### Tasks
`TASK-REP-01`, `TASK-REP-02`, `TASK-REP-03`

---

### TASK-REP-01: Replacement upload and isolated processing
**Module:** 15 — Replacement/Deletion
**Epic:** EPIC-REP-A
**Story:** STORY-REP-01
**Priority:** High
**Implementation order:** 68
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-KNW-03, TASK-EMB-03
**Blocks:** Safe activation
**Primary role:** Backend

#### Objective
Accept replacement content, store it at a replacement key, and process it into inactive chunks/vectors without touching current searchable content.

#### Requirements Covered
FR-KNW-006, ASDD §11.2 (replacement key), §13.3.

#### Detailed Description
Upload replacement bytes to `applications/{appId}/files/{fileId}/replacement/{replacementId}`; set `ReplacementObjectKey`/`ReplacementStatus`; run the standard pipeline producing inactive chunks. Current chunks stay active/searchable throughout.

#### Technical Approach
- Reuse processing pipeline with a replacement flag keeping chunks inactive (module 11/13).

#### API or Event Contracts
- `POST …/files/{id}/replace` (multipart) → replacement job created.

#### Data Changes
Sets replacement fields; writes inactive chunks.

#### Security and Authorization
`CanManageKnowledge`; app boundary; archived guard.

#### Error and Recovery Behavior
Failure leaves current content intact; temp artifacts cleaned (REP-03 cleanup).

#### Acceptance Criteria
- Given a replacement upload, when processing runs, then inactive chunks are produced and current content remains searchable.

#### Test Requirements
- Integration: replacement produces inactive chunks; current content unaffected.

#### Definition of Done
- [ ] Replacement upload + isolated processing to inactive chunks.

#### Implementation Notes
Distinguish replacement runs so activation is deferred.

#### Out of Scope
Activation swap (REP-02).

---

### TASK-REP-02: Safe activation swap and failure cleanup
**Module:** 15 — Replacement/Deletion
**Epic:** EPIC-REP-A
**Story:** STORY-REP-01
**Priority:** Critical
**Implementation order:** 69
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-REP-01
**Blocks:** None
**Primary role:** Backend / Database

#### Objective
On successful replacement processing, atomically deactivate/remove old chunks, activate replacement chunks, update the active S3 object reference + hash/metadata, and mark Ready; on failure, clean temporary chunks/object and retain current content.

#### Requirements Covered
FR-KNW-006, ASDD §13.3.

#### Detailed Description
One DB transaction: remove/deactivate old chunks → activate replacement chunks → update ObjectKey/hash/metadata → Status=Ready. Then delete the old S3 object per cleanup policy. On processing failure: delete temporary chunks + replacement object; keep current content searchable and file Ready.

#### Technical Approach
- Transactional swap; post-commit S3 cleanup; failure path cleanup.

#### API or Event Contracts
Emits `FileCompleted`/`FileFailed`.

#### Data Changes
Chunk activation swap + file metadata update; S3 object replacement/cleanup.

#### Security and Authorization
App boundary; server-generated keys.

#### Error and Recovery Behavior
Swap is all-or-nothing; failure preserves prior state (Definition-of-Done: replacement cannot destroy working knowledge).

#### Acceptance Criteria
- Given successful replacement, when activated, then new chunks are searchable, old removed, metadata updated in one transaction.
- Given a failed replacement, then current content remains searchable and temp artifacts are cleaned.

#### Test Requirements
- Integration: success swap; failure preserves current content; Security: app boundary.

#### Definition of Done
- [ ] Transactional activation + old-content cleanup + failure recovery.

#### Implementation Notes
This directly satisfies the DoD knowledge-integrity guarantee.

#### Out of Scope
Deletion (REP-03).

---

### TASK-REP-03: Deletion worker and cleanup
**Module:** 15 — Replacement/Deletion
**Epic:** EPIC-REP-B
**Story:** STORY-REP-01
**Priority:** High
**Implementation order:** 70
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-KNW-05, TASK-SQS-03, TASK-S3-04
**Blocks:** None
**Primary role:** Backend

#### Objective
Consume `deletion-jobs` to remove S3 objects and chunks/vectors for deleted files (and app-delete cascades), leaving historical outputs intact with source-unavailable state.

#### Requirements Covered
FR-KNW-008, FR-CIT-004, ASDD §11.3.

#### Detailed Description
Idempotent deletion consumer: delete active + replacement objects, delete chunks/vectors, finalize file state; outputs/citations referencing removed sources remain but show source unavailable. Handle duplicate deliveries safely.

#### Technical Approach
- Consumer (SQS-03) → delete vectors/chunks (transaction) → delete S3 objects → finalize.

#### API or Event Contracts
Consumes `DeletionMessage`.

#### Data Changes
Removes chunks/vectors + S3 objects; marks file deleted.

#### Security and Authorization
App boundary; server-side keys.

#### Error and Recovery Behavior
Idempotent; retryable on transient storage/db errors.

#### Acceptance Criteria
- Given a delete job, when processed, then S3 objects + chunks/vectors are removed and outputs remain with source-unavailable.
- Given duplicate delivery, then no error and no double work.

#### Test Requirements
- Integration: deletion cleanup + idempotency; historical output source-unavailable.

#### Definition of Done
- [ ] Idempotent deletion worker + cleanup + output retention.

#### Implementation Notes
Citations must render "source unavailable" (module 22).

#### Out of Scope
Citation UI (module 22).

---

## MODULE 16 — Retrieval Service (RET)

**Epics**

- EPIC-RET-A: Application-scoped semantic + hybrid retrieval
- EPIC-RET-B: Fusion, dedup, budgeting & grounding behavior

### STORY-RET-01: Application-scoped grounded evidence retrieval

**As the:** retrieval service
**I want:** to embed the query, search only the authorized application's ready/enabled chunks, fuse semantic+keyword ranks, dedupe, and apply a token budget
**So that:** agents receive relevant, isolated, budget-fitting evidence (FR-RET-*, ASDD §15)

#### Business Value
Determines answer grounding quality and enforces the security boundary for search.

#### Story Acceptance Criteria
- Every query is application-scoped to ready+enabled files before ranking; never global-then-filter.
- Semantic and optional hybrid retrieval with normalized rank fusion, duplicate/overlap suppression, optional neighbors, threshold, and evidence-token budget.
- Strict-grounding no-evidence returns a controlled no-information result; partial readiness discloses excluded files.

#### Dependencies
STORY-PG-01, STORY-AOI-01.

#### Tasks
`TASK-RET-01`, `TASK-RET-02`, `TASK-RET-03`, `TASK-RET-04`, `TASK-RET-05`

---

### TASK-RET-01: Query embedding and application-scoped semantic retrieval
**Module:** 16 — Retrieval
**Epic:** EPIC-RET-A
**Story:** STORY-RET-01
**Priority:** Critical
**Implementation order:** 71
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-PG-06, TASK-AOI-03
**Blocks:** Agent execution
**Primary role:** Backend

#### Objective
Embed the retrieval query with the same deployment/dimensions used to build stored vectors and run the app-scoped exact cosine search over ready+enabled chunks.

#### Requirements Covered
FR-RET-001, FR-RET-002, FR-RET-003, ASDD §15.1/§15.2.

#### Detailed Description
Build the retrieval query (user request, optionally combined with the agent objective for broad generation — not the entire large prompt, §15.3), embed it via the matching embedding config (D10), and run the app-scoped cosine query. Apply the optional selected-file-IDs scope. Reject retrieval if the corpus embedding config differs from the query config.

#### Technical Approach
- Reuse PG-06 query; embed via AOI-03; enforce file-scope filter.

#### API or Event Contracts
Not applicable (internal to run pipeline).

#### Data Changes
Read-only.

#### Security and Authorization
Application + enabled + Ready + active filters (FR-SEC-004).

#### Error and Recovery Behavior
Embedding failure retryable; empty candidates handled downstream.

#### Acceptance Criteria
- Given a query for App A, when retrieving, then only App A ready+enabled active chunks are candidates.
- Given a mismatched embedding config, then retrieval fails with a clear configuration error.

#### Test Requirements
- Integration: scoped retrieval; Security: cross-app isolation; config-mismatch handling.

#### Definition of Done
- [ ] Query embedding + app-scoped semantic retrieval + file scope.

#### Implementation Notes
Do not embed the whole agent prompt (§15.3).

#### Out of Scope
Hybrid/fusion (RET-02).

---

### TASK-RET-02: Hybrid retrieval and normalized rank fusion
**Module:** 16 — Retrieval
**Epic:** EPIC-RET-A
**Story:** STORY-RET-01
**Priority:** High
**Implementation order:** 72
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-RET-01, TASK-PG-04
**Blocks:** Evidence assembly
**Primary role:** Backend

#### Objective
Add optional PostgreSQL FTS retrieval and fuse semantic + keyword candidates via normalized rank fusion with configurable weights.

#### Requirements Covered
FR-RET-004, ASDD §15.4.

#### Detailed Description
When retrieval mode is hybrid: run app-scoped FTS, normalize each rank independently, combine with configured weights, and produce a fused candidate list. Semantic-only mode skips FTS.

#### Technical Approach
- Min-max/rank normalization; weighted sum; app-scoped FTS query (PG-04).

#### API or Event Contracts
Not applicable.

#### Data Changes
Read-only.

#### Security and Authorization
FTS query app-scoped (FR-SEC-004).

#### Error and Recovery Behavior
FTS failure degrades to semantic-only with a diagnostic note.

#### Acceptance Criteria
- Given hybrid mode, when retrieving, then semantic + keyword ranks fuse with configured weights into one ordered list, all app-scoped.

#### Test Requirements
- Unit: rank fusion; Integration: hybrid scoping.

#### Definition of Done
- [ ] Hybrid retrieval + normalized fusion.

#### Implementation Notes
Keep weights configurable per agent (retrieval params).

#### Out of Scope
Dedup/budget (RET-03).

---

### TASK-RET-03: Duplicate/overlap suppression and neighbor inclusion
**Module:** 16 — Retrieval
**Epic:** EPIC-RET-B
**Story:** STORY-RET-01
**Priority:** High
**Implementation order:** 73
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-RET-02
**Blocks:** Budgeting
**Primary role:** Backend

#### Objective
Reduce duplicate/heavily-overlapping passages and optionally include neighboring chunks for context.

#### Requirements Covered
FR-RET-005, ASDD §15.1 (dedup, neighbor inclusion).

#### Detailed Description
Suppress near-duplicate/overlapping chunks (by content hash + offset overlap + similarity) and, when `IncludeNeighbors` is set, add adjacent chunks (by sequence) to improve coherence, still within the app scope.

#### Technical Approach
- Overlap detection via offsets + hash; neighbor lookup by `(file_id, sequence±1)`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Read-only.

#### Security and Authorization
Neighbors constrained to same app/file.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given overlapping candidates, when deduped, then redundant passages are removed.
- Given neighbor inclusion, then adjacent chunks are added without crossing app/file boundaries.

#### Test Requirements
- Unit: dedup + neighbor logic.

#### Definition of Done
- [ ] Dedup/overlap suppression + neighbor inclusion.

#### Implementation Notes
Dedup before budgeting to maximize useful evidence.

#### Out of Scope
Budget/grounding (RET-04).

---

### TASK-RET-04: Threshold, evidence-token budget, and evidence package
**Module:** 16 — Retrieval
**Epic:** EPIC-RET-B
**Story:** STORY-RET-01
**Priority:** Critical
**Implementation order:** 74
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-RET-03, TASK-AOI-04
**Blocks:** Prompt assembly
**Primary role:** Backend

#### Objective
Apply the similarity threshold and evidence-token budget, then assemble the evidence package with internal source IDs and metadata.

#### Requirements Covered
FR-RET-006, FR-PAR-002, ASDD §15.5/§15.6.

#### Detailed Description
Filter by `MinimumSimilarity`; compute available evidence tokens = model context − platform instructions − agent prompt − user request − output reservation − protocol overhead (module 20 supplies fixed sizes); include top candidates until the budget is reached; assign `SOURCE-001…` IDs; attach filename + heading path + content per source block.

#### Technical Approach
- Greedy fill under token budget using the estimator; produce an ordered evidence list with source IDs.

#### API or Event Contracts
Not applicable (feeds module 20).

#### Data Changes
Produces `RunEvidence` candidates (persisted in module 19/22).

#### Security and Authorization
Only in-scope evidence included; source IDs bound to this run (module 22 validation).

#### Error and Recovery Behavior
If fixed content already exceeds context → surfaced as `ContextOverflow` (module 20 rejects before AOAI).

#### Acceptance Criteria
- Given candidates, when budgeting, then only those above threshold and within the token budget are included with SOURCE IDs and metadata.

#### Test Requirements
- Unit: threshold + budget fill; Integration: evidence package shape.

#### Definition of Done
- [ ] Threshold + token budget + evidence package with source IDs.

#### Implementation Notes
Agent prompt is never silently truncated (§15.5).

#### Out of Scope
No-evidence/partial handling (RET-05).

---

### TASK-RET-05: Strict-grounding no-evidence and partial-readiness handling
**Module:** 16 — Retrieval
**Epic:** EPIC-RET-B
**Story:** STORY-RET-01
**Priority:** High
**Implementation order:** 75
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-RET-04
**Blocks:** Execution behavior
**Primary role:** Backend

#### Objective
Return a controlled no-information result when strict grounding finds no qualifying evidence, and disclose files excluded due to partial readiness.

#### Requirements Covered
FR-RET-007, FR-RET-008, ASDD §16.4.

#### Detailed Description
If strict grounding and no evidence meets the threshold, return a `RetrievalEmpty` outcome (do not ask the model to invent content). When some scoped files are still processing, run with ready files and record the excluded set for UI disclosure.

#### Technical Approach
- Retrieval result carries `Excluded[]` + `NoEvidence` flag consumed by execution + UI.

#### API or Event Contracts
Not applicable.

#### Data Changes
Records excluded files on the run.

#### Security and Authorization
App boundary preserved.

#### Error and Recovery Behavior
No-evidence → controlled result (not a hard failure); partial readiness disclosed.

#### Acceptance Criteria
- Given strict grounding and no qualifying evidence, when retrieving, then a controlled no-information result is returned (no generation).
- Given files still processing, then the run uses ready files and records excluded ones.

#### Test Requirements
- Unit/Integration: no-evidence outcome; partial-readiness disclosure.

#### Definition of Done
- [ ] Strict no-evidence + partial-readiness handling.

#### Implementation Notes
UI warns before execution about excluded files (module 19/23).

#### Out of Scope
Generation (module 21).

---

# PHASE 7 — Agents

## MODULE 17 — Agent Management (AGT)

**Epics**

- EPIC-AGT-A: Agent entity, CRUD & lifecycle
- EPIC-AGT-B: Large prompt editor & parameter configuration
- EPIC-AGT-C: Capability validation & knowledge scope

### STORY-AGT-01: Configure prompt-driven agents with advanced settings

**As an:** Application Admin
**I want:** to create/edit agents with a large prompt, generation/retrieval parameters, and knowledge scope validated against deployment capabilities
**So that:** agents produce grounded Markdown per my configuration (FR-AGT-*, FR-PAR-*, ASDD §7.4, §9.3)

#### Business Value
Agents are the user-facing configuration surface driving all generation.

#### Story Acceptance Criteria
- Agent CRUD with one current editable configuration (no version screens); large prompt with token estimate vs context limit; prompt never silently truncated.
- Generation params (temperature, max output tokens, TopP-when-supported) and retrieval params validated against the deployment registry; unsupported values rejected or explicitly adjusted+recorded.
- Knowledge scope: all ready+enabled files or a selected set.

#### Dependencies
STORY-AUTHZ-01, STORY-AOI-01, module 14.

#### Tasks
`TASK-AGT-01`, `TASK-AGT-02`, `TASK-AGT-03`, `TASK-AGT-04`, `TASK-AGT-05`

---

### TASK-AGT-01: Agent entity, schema, and repository
**Module:** 17 — Agents
**Epic:** EPIC-AGT-A
**Story:** STORY-AGT-01
**Priority:** Critical
**Implementation order:** 76
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-PG-01, TASK-APP-01
**Blocks:** Agent CRUD, execution
**Primary role:** Backend / Database

#### Objective
Define the `Agent` aggregate (ASDD §9.3) with all fields, status, concurrency token, and repository scoped by application.

#### Requirements Covered
FR-AGT-001, FR-AGT-005, ASDD §9.3.

#### Detailed Description
Fields per ASDD §9.3 (Name, Description, Prompt, Status, KnowledgeScopeType, GenerationDeployment, Temperature, MaximumOutputTokens, TopP, RetrievalMode, RetrievalCount, MinimumSimilarity, MaximumEvidenceTokens, IncludeNeighbors, StrictGrounding, CitationsRequired, audit fields, ConcurrencyToken). One current editable config (no versioning). Index `agents(application_id, status)`.

#### Technical Approach
- EF entity + optimistic concurrency token; domain guards for status transitions.

#### API or Event Contracts
Not applicable.

#### Data Changes
Table `agents.agents` + index.

#### Security and Authorization
App-scoped; edits require `CanManageAgents`.

#### Error and Recovery Behavior
Concurrent edit → concurrency conflict.

#### Acceptance Criteria
- Given an agent, when persisted, then all configuration fields are stored with a concurrency token; edits overwrite the single current config.

#### Test Requirements
- Unit: transitions/concurrency; Integration: persistence.

#### Definition of Done
- [ ] Agent entity + schema + repository.

#### Implementation Notes
Snapshots (module 19) provide traceability instead of versioning.

#### Out of Scope
Endpoints (AGT-02).

---

### TASK-AGT-02: Agent CRUD and lifecycle endpoints
**Module:** 17 — Agents
**Epic:** EPIC-AGT-A
**Story:** STORY-AGT-01
**Priority:** Critical
**Implementation order:** 77
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-AGT-01, TASK-AGT-03, TASK-AUTHZ-02
**Blocks:** Execution, agent UI
**Primary role:** Backend

#### Objective
Expose list/create/edit/enable/disable/delete agent endpoints with capability validation and audit.

#### Requirements Covered
FR-AGT-001, FR-AGT-005, FR-AGT-006 (prompt visibility), ASDD §5.2.

#### Detailed Description
CRUD + enable/disable/delete. Create/edit validate params against the deployment registry (AGT-03) and reject archived-app edits. List returns non-secret config; full prompt only to SuperAdmin/ApplicationAdmin (viewers excluded, FR-AGT-006). All mutations audited.

#### Technical Approach
- Application-layer commands; Problem Details; concurrency handling.

#### API or Event Contracts
- `POST /api/applications/{appId}/agents` → `201`.
- `GET /api/applications/{appId}/agents` (prompt redacted for viewers).
- `GET /api/applications/{appId}/agents/{id}`.
- `PUT …/{id}`, `POST …/{id}/enable|disable`, `DELETE …/{id}`.

#### Data Changes
Writes agents; audit events.

#### Security and Authorization
`CanManageAgents` for mutations; prompt redaction for viewers; app boundary.

#### Error and Recovery Behavior
Invalid params → `Validation`; concurrency conflict → `Conflict`.

#### Acceptance Criteria
- Given an admin, when creating a valid agent, then it persists and is auditable.
- Given a viewer, when listing agents, then full prompts are not returned.
- Given unsupported params, then rejected with a clear reason.

#### Test Requirements
- Integration: CRUD + validation; Security: prompt redaction + role enforcement.

#### Definition of Done
- [ ] Agent CRUD/lifecycle with validation, redaction, audit.

#### Implementation Notes
Editing overwrites the single current config (FR-AGT-005).

#### Out of Scope
Capability validation internals (AGT-03).

---

### TASK-AGT-03: Deployment capability validation for parameters
**Module:** 17 — Agents
**Epic:** EPIC-AGT-C
**Story:** STORY-AGT-01
**Priority:** Critical
**Implementation order:** 78
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-AOI-01, TASK-AGT-01
**Blocks:** Agent CRUD, execution
**Primary role:** Backend

#### Objective
Validate agent generation/retrieval parameters against the selected deployment's capabilities; reject or explicitly adjust+record unsupported values (never silently ignore).

#### Requirements Covered
FR-PAR-004, FR-PAR-001/002, ASDD §14.4.

#### Detailed Description
Check temperature/max-output/TopP against deployment support and limits; validate retrieval params ranges (count, similarity, evidence tokens). Unsupported values either rejected (`Validation`) or adjusted with an explicit recorded note (surfaced to the user), matching FR-PAR-004.

#### Technical Approach
- Validator using the registry (AOI-01); returns adjustments + reasons.

#### API or Event Contracts
Validation errors/adjustments in create/edit responses.

#### Data Changes
Records effective/adjusted values.

#### Security and Authorization
Server-authoritative validation.

#### Error and Recovery Behavior
Reject or adjust-and-record; never silent.

#### Acceptance Criteria
- Given a deployment without TopP support, when TopP is set, then it is rejected or explicitly adjusted and recorded (not ignored).
- Given out-of-range max output, then rejected with the limit.

#### Test Requirements
- Unit: capability truth table; Integration via create/edit.

#### Definition of Done
- [ ] Capability validation with reject/adjust+record semantics.

#### Implementation Notes
Same validation reused at run submission (module 19).

#### Out of Scope
Run-time budget (module 20).

---

### TASK-AGT-04: Large prompt editor with token estimate and knowledge scope UI
**Module:** 17 — Agents
**Epic:** EPIC-AGT-B
**Story:** STORY-AGT-01
**Priority:** High
**Implementation order:** 79
**Estimated size:** L
**Can run in parallel:** Yes
**Dependencies:** TASK-AGT-02, TASK-AOI-04, TASK-AUTHZ-04
**Blocks:** None
**Primary role:** Frontend

#### Objective
Build the agent editor (`/applications/:id/agents/:agentId`) with name/description, large prompt editor + char/token counts vs context limit, deployment selection, generation/retrieval params with presets, and file-scope selection (ASDD §7.4).

#### Requirements Covered
FR-AGT-001/002/003/004, FR-PAR-003 (presets), ASDD §7.4.

#### Detailed Description
Monaco/CodeMirror prompt editor with live char + estimated token count against the selected deployment's context limit (warn, never silently truncate). Deployment dropdown from the registry. Presets Precise/Balanced/Creative/Custom with numeric override. Retrieval params controls. Knowledge scope: all ready+enabled or a selected set. Save + Test actions. Loading/empty/error states.

#### Technical Approach
- Registry-driven controls; token estimate from AOI-04; TanStack Query for save.

#### API or Event Contracts
Consumes agent + deployment + token-estimate endpoints.

#### Data Changes
Not applicable.

#### Security and Authorization
Editor available to admins; viewers do not see full prompt (FR-AGT-006).

#### Error and Recovery Behavior
Surfaces validation/adjustment messages from AGT-03.

#### Acceptance Criteria
- Given a large prompt, when edited, then char/token counts show against the context limit and no silent truncation occurs.
- Given presets, when selected, then params update and remain numerically adjustable.
- Given file scope, when choosing selected files, then only ready+enabled files are selectable.

#### Test Requirements
- Component: token counter, presets, scope selection, validation surfacing.

#### Definition of Done
- [ ] Full agent editor with token estimate, presets, scope, save/test.

#### Implementation Notes
Test action wires to module 18. Uses the shared Monaco prompt editor + resizable split layout (`TASK-UX-07`) and the shared form/validation foundation (react-hook-form + zod, decision D18).

#### Out of Scope
Test execution backend (module 18), run submission (module 19), shared editor/panel primitives (module 37).

---

### TASK-AGT-05: Knowledge scope resolution and validation
**Module:** 17 — Agents
**Epic:** EPIC-AGT-C
**Story:** STORY-AGT-01
**Priority:** High
**Implementation order:** 80
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-AGT-01, TASK-KNW-01
**Blocks:** Retrieval scope
**Primary role:** Backend

#### Objective
Resolve an agent's knowledge scope to the concrete set of ready+enabled file IDs at run time, validating selected files belong to the application.

#### Requirements Covered
FR-AGT-004, FR-RET-002, FR-SEC-004.

#### Detailed Description
For `AllReadyEnabled`, resolve to all ready+enabled files; for `Selected`, validate each selected file belongs to the app and is ready+enabled (drop others, disclose as excluded). Feeds retrieval's file-scope filter (module 16).

#### Technical Approach
- Scope resolver returning file IDs + excluded list.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable (reads files).

#### Security and Authorization
Selected files validated against the app boundary.

#### Error and Recovery Behavior
Non-ready/foreign files excluded and disclosed; empty scope handled by retrieval.

#### Acceptance Criteria
- Given selected scope with a foreign file ID, when resolved, then that file is excluded and never searched.
- Given all-ready scope, then only ready+enabled files are used.

#### Test Requirements
- Unit: scope resolution; Security: foreign-file rejection.

#### Definition of Done
- [ ] Scope resolver with validation + exclusion disclosure.

#### Implementation Notes
Partial readiness disclosed to UI (module 16/19).

#### Out of Scope
Retrieval execution (module 16).

---

## MODULE 18 — Agent Testing (AGTT)

**Epics**

- EPIC-AGTT-A: Ephemeral test execution & diagnostics

### STORY-AGTT-01: Test an agent configuration without saving output

**As an:** Application Admin
**I want:** to test the current agent configuration with sample input and see retrieved evidence, generated Markdown, params, and errors
**So that:** I can iterate on prompts/params without creating production outputs (ASDD §18 test action, FR-AGT-002)

#### Business Value
Faster, safer agent iteration; reduces wasted production runs.

#### Story Acceptance Criteria
- Test runs the current (unsaved-or-saved) configuration, returns evidence + Markdown + effective params + errors, and does not persist a production output unless explicitly saved.

#### Dependencies
STORY-AGT-01, STORY-RET-01, module 20/21.

#### Tasks
`TASK-AGTT-01`, `TASK-AGTT-02`

---

### TASK-AGTT-01: Ephemeral agent test execution endpoint
**Module:** 18 — Agent Testing
**Epic:** EPIC-AGTT-A
**Story:** STORY-AGTT-01
**Priority:** High
**Implementation order:** 81
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-RET-04, TASK-PRMT-01, TASK-GEN-01
**Blocks:** Agent editor test action
**Primary role:** Backend

#### Objective
Provide a synchronous (or short-lived) test endpoint that runs retrieval + prompt assembly + generation for a given agent config + test input, returning diagnostics without persisting a `GeneratedOutput`.

#### Requirements Covered
ASDD §18 (test), FR-AGT-002, FR-AGT-003.

#### Detailed Description
Accept the agent id (or an unsaved config payload) + test input; perform context budgeting, retrieval, prompt assembly, and generation; return evidence, generated Markdown, requested vs effective params, and any errors. No production output/citation records are created. Reuses the same pipeline as real runs to keep parity.

#### Technical Approach
- Shared execution core (module 19/20/21) invoked in a "test" mode that skips output persistence.

#### API or Event Contracts
- `POST /api/applications/{appId}/agents/{id}/test {input, configOverride?}` → `200 {markdown, evidence[], requestedParams, effectiveParams, errors?}`.

#### Data Changes
None persisted (unless the user later saves via a separate action).

#### Security and Authorization
`CanManageAgents`; app boundary; retrieval isolation.

#### Error and Recovery Behavior
Errors returned inline (throttling/timeout/context-overflow/no-evidence) with correlation ID.

#### Acceptance Criteria
- Given a test request, when executed, then evidence + Markdown + params + errors return and no `GeneratedOutput` is created.
- Given no evidence in strict grounding, then a controlled no-information result is returned.

#### Test Requirements
- Integration (fake AOAI): test returns diagnostics; no output persisted; Security: cross-app denial.

#### Definition of Done
- [ ] Ephemeral test endpoint with full diagnostics, no persistence.

#### Implementation Notes
Reuse the production pipeline to avoid drift.

#### Out of Scope
Saving as output (module 23 regenerate/save).

---

### TASK-AGTT-02: Agent test panel UI
**Module:** 18 — Agent Testing
**Epic:** EPIC-AGTT-A
**Story:** STORY-AGTT-01
**Priority:** Medium
**Implementation order:** 82
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-AGTT-01, TASK-AGT-04, TASK-MDR-01
**Blocks:** None
**Primary role:** Frontend

#### Objective
Add a test panel to the agent editor showing input, retrieved evidence, safely-rendered Markdown, effective params, and errors.

#### Requirements Covered
ASDD §18 (test action), FR-AGT-002.

#### Detailed Description
Input box + Run Test; display evidence list (filename/heading/snippet), sanitized Markdown preview, requested vs effective params, and error banners. Clarify that testing does not create production outputs.

#### Technical Approach
- Uses AGTT-01 endpoint; sanitized rendering (module 24).

#### API or Event Contracts
Consumes the test endpoint.

#### Data Changes
Not applicable.

#### Security and Authorization
Admin-only; sanitized rendering.

#### Error and Recovery Behavior
Inline error display with correlation ID.

#### Acceptance Criteria
- Given a test run, when complete, then evidence + preview + params + errors display and the UI states nothing was saved.

#### Test Requirements
- Component: renders diagnostics; sanitization applied.

#### Definition of Done
- [ ] Test panel with evidence/preview/params/errors.

#### Implementation Notes
Share the Markdown renderer with outputs (module 24).

#### Out of Scope
Output library (module 23).

---

# PHASE 8 — Execution & Generation

## MODULE 19 — Agent Execution (RUN)

**Epics**

- EPIC-RUN-A: Run submission, snapshot & idempotency
- EPIC-RUN-B: Agent worker pipeline, stages & progress
- EPIC-RUN-C: Cancellation & failure handling

### STORY-RUN-01: Submit and execute agent runs asynchronously with immutable snapshots

**As an:** authorized user
**I want:** to submit a run that snapshots the agent config, executes on a worker with staged progress, and preserves what was used
**So that:** outputs are reproducible/traceable and runs survive navigation/restarts (FR-RUN-*, ASDD §16)

#### Business Value
The execution engine and the traceability mechanism (snapshots) replacing versioning.

#### Story Acceptance Criteria
- Submission is idempotent (idempotency key), snapshots agent prompt/deployment/params/scope, persists a durable run, and enqueues an identifier-only message.
- Worker claims the run, budgets context, retrieves evidence, assembles the prompt, generates, validates, saves output+citations, and marks completed; stages persisted before SignalR.
- Pre-generation cancellation supported; failures classified and recorded.

#### Dependencies
STORY-AGT-01, STORY-RET-01, STORY-JOB-01.

#### Tasks
`TASK-RUN-01`, `TASK-RUN-02`, `TASK-RUN-03`, `TASK-RUN-04`, `TASK-RUN-05`

---

### TASK-RUN-01: Run submission, idempotency, and agent snapshot
**Module:** 19 — Execution
**Epic:** EPIC-RUN-A
**Story:** STORY-RUN-01
**Priority:** Critical
**Implementation order:** 83
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-AGT-01, TASK-JOB-02, TASK-APP-06
**Blocks:** Worker pipeline
**Primary role:** Backend

#### Objective
Implement run submission: authorize, validate the agent is active + params (reuse AGT-03), snapshot the config, create a durable `AgentRun`, and enqueue via outbox using an idempotency key to prevent duplicates.

#### Requirements Covered
FR-RUN-001, FR-RUN-002, FR-RUN-004, FR-RUN-005, FR-AGT-005 (snapshot), ASDD §16.3.

#### Detailed Description
On submit (agent id, user request, optional output title, idempotency key), authorize via `CanExecuteAgent` (honoring viewer-execution flag), snapshot AgentNameSnapshot/AgentPromptSnapshot/DeploymentSnapshot/RequestedParametersJson/RetrievalSettingsJson/KnowledgeScopeSnapshot, persist the run (Status=Queued), and enqueue `AgentExecutionMessage` via outbox. Repeated submits with the same idempotency key return the existing run.

#### Technical Approach
- Idempotency table/unique key; snapshot JSON columns; outbox enqueue (module 9).

#### API or Event Contracts
- `POST /api/applications/{appId}/agents/{id}/runs {userRequest, outputTitle?, idempotencyKey}` → `202 {runId}`.

#### Data Changes
Inserts `agent_runs` + snapshot + outbox row; idempotency record.

#### Security and Authorization
`CanExecuteAgent`; only active agents; app boundary; viewer-execution flag.

#### Error and Recovery Behavior
Duplicate submit → same run (no duplicate); invalid params → `Validation`.

#### Acceptance Criteria
- Given repeated clicks with the same idempotency key, when submitting, then only one run is created.
- Given submission, when persisted, then the agent config is snapshotted and a message enqueued.
- Given a disabled agent, then submission is rejected.

#### Test Requirements
- Integration: submit + snapshot + enqueue; idempotency; Security: role/flag enforcement.

#### Definition of Done
- [ ] Idempotent submission + snapshot + durable run + outbox enqueue.

#### Implementation Notes
Snapshots are the traceability mechanism (no versioning).

#### Out of Scope
Worker execution (RUN-02).

---

### TASK-RUN-02: Agent worker pipeline and stage progression
**Module:** 19 — Execution
**Epic:** EPIC-RUN-B
**Story:** STORY-RUN-01
**Priority:** Critical
**Implementation order:** 84
**Estimated size:** L
**Can run in parallel:** No
**Dependencies:** TASK-RUN-01, TASK-SQS-03, TASK-RET-05, TASK-PRMT-02, TASK-GEN-02, TASK-CIT-03
**Blocks:** Outputs
**Primary role:** Backend

#### Objective
Implement the agent worker consuming run messages and executing the ASDD §16 pipeline with persisted stages and progress.

#### Requirements Covered
FR-RUN-003, FR-RUN-006, FR-RUN-007, ASDD §16.

#### Detailed Description
Claim the run; validate deployment availability; calculate context budget (module 20); retrieve evidence (module 16); assemble prompt (module 20); call AOAI (module 21); validate Markdown + citations (modules 21/22); persist RunEvidence + GeneratedOutput + Citations; mark Completed. Persist each stage (Preparing→Retrieving→Building context→Generating→Validating→Saving→Completed) before SignalR. Idempotent for duplicate delivery.

#### Technical Approach
- Orchestrator invoking stage services; shared with the test path (module 18).

#### API or Event Contracts
Consumes `AgentExecutionMessage`; emits `RunStageChanged`/`RunCompleted`/`RunFailed`.

#### Data Changes
Writes RunEvidence, GeneratedOutput, Citations; updates run stage/progress.

#### Security and Authorization
App-scoped retrieval; snapshot params used (not live agent).

#### Error and Recovery Behavior
Retryable transient AOAI/storage errors; terminal on context overflow/invalid config; duplicate delivery idempotent.

#### Acceptance Criteria
- Given a queued run, when executed, then it advances through stages to Completed with a saved output + citations.
- Given a transient AOAI failure, then the run retries; given context overflow, it fails terminally before calling AOAI.
- Given navigation away/restart, then the run continues and state persists.

#### Test Requirements
- Integration (fake AOAI): full run; retry vs terminal; duplicate handling; persistence after restart.

#### Definition of Done
- [ ] Agent worker pipeline with persisted stages + idempotency.

#### Implementation Notes
Uses snapshot config, guaranteeing reproducibility even after agent edits.

#### Out of Scope
Prompt/generation internals (modules 20/21).

---

### TASK-RUN-03: Run stage/progress persistence and SignalR emission
**Module:** 19 — Execution
**Epic:** EPIC-RUN-B
**Story:** STORY-RUN-01
**Priority:** High
**Implementation order:** 85
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-RUN-02, TASK-SIG-04
**Blocks:** Run dashboard
**Primary role:** Backend

#### Objective
Persist meaningful run-stage transitions and timings, then emit SignalR run events after DB commit.

#### Requirements Covered
FR-RUN-007, ASDD §16.5 (stage progress), §18.4, §23.3 (trace spans).

#### Detailed Description
Record stage timings for the run details view; emit `RunStageChanged`/`RunCompleted`/`RunFailed` after persistence (source-of-truth). No token streaming in baseline (D3).

#### Technical Approach
- Stage timing captured per span; events via the SignalR publisher (module 25).

#### API or Event Contracts
Emits run events (module 25 contract).

#### Data Changes
Records stage timings on the run.

#### Security and Authorization
Events delivered only to authorized run/application groups.

#### Error and Recovery Behavior
Lost events recoverable via REST (module 25).

#### Acceptance Criteria
- Given a run, when stages change, then timings persist and events emit post-commit to authorized groups.

#### Test Requirements
- Integration: stage timings + event emission ordering.

#### Definition of Done
- [ ] Stage timings + post-commit run events.

#### Implementation Notes
Feeds run details (module 29).

#### Out of Scope
Streaming tokens (deferred).

---

### TASK-RUN-04: Run failure handling and diagnostics
**Module:** 19 — Execution
**Epic:** EPIC-RUN-C
**Story:** STORY-RUN-01
**Priority:** High
**Implementation order:** 86
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-RUN-02, TASK-JOB-06, TASK-ERR-01
**Blocks:** Run dashboard
**Primary role:** Backend

#### Objective
Record run failures with category, safe message, retry eligibility, and correlation; support manual retry/regenerate.

#### Requirements Covered
FR-RUN-004, FR-AOI-005, ASDD §22, failure scenarios (throttling/timeout/invalid deployment/no evidence/context overflow).

#### Detailed Description
Map failures to categories (Throttled/Timeout/Configuration/ContextOverflow/RetrievalEmpty/GenerationFailure/…), store safe message + correlation, and expose retry (re-run same snapshot) where eligible. Raw AOAI details stay in restricted logs.

#### Technical Approach
- Reuse classifier (JOB-06) + error contract (module 32).

#### API or Event Contracts
- `POST …/runs/{runId}/retry` (authorized).

#### Data Changes
Failure fields on the run.

#### Security and Authorization
Retry restricted to admins; no secrets in messages.

#### Error and Recovery Behavior
Terminal vs retryable per classifier; correlation on all.

#### Acceptance Criteria
- Given a failed run, when viewed, then category + safe message + correlation display and retry is offered when eligible.
- Given context overflow, then it fails before calling AOAI.

#### Test Requirements
- Integration: each failure category recorded; retry authorization.

#### Definition of Done
- [ ] Failure categorization + diagnostics + retry.

#### Implementation Notes
Regenerate-as-new-run lives in module 23.

#### Out of Scope
Output regenerate UI (module 23).

---

### TASK-RUN-05: Pre-generation cancellation
**Module:** 19 — Execution
**Epic:** EPIC-RUN-C
**Story:** STORY-RUN-01
**Priority:** Medium
**Implementation order:** 87
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-RUN-02
**Blocks:** None
**Primary role:** Backend

#### Objective
Support cooperative cancellation of a run before generation begins, transitioning it to Cancelled (D2).

#### Requirements Covered
FR-RUN-003 (Cancelled state), D2.

#### Detailed Description
A cancel request sets a cancellation flag; the worker checks it before the Generating stage and, if set, transitions to Cancelled without calling AOAI. Mid-generation cancellation is out of scope (deferred).

#### Technical Approach
- Cancellation flag on the run; worker checks pre-generation checkpoints.

#### API or Event Contracts
- `POST …/runs/{runId}/cancel` → `202`.

#### Data Changes
Run status → Cancelled.

#### Security and Authorization
Requester must have run/manage rights.

#### Error and Recovery Behavior
If generation already started, cancel is a no-op with a clear message.

#### Acceptance Criteria
- Given a queued/preparing run, when cancelled, then it transitions to Cancelled without calling AOAI.
- Given a run past the generation checkpoint, then cancel does not stop it (documented).

#### Test Requirements
- Integration: pre-generation cancel; post-checkpoint no-op.

#### Definition of Done
- [ ] Pre-generation cancellation with clear semantics.

#### Implementation Notes
Deferred: mid-generation cancellation (Deferred Scope).

#### Out of Scope
Streaming/mid-generation abort.

---

## MODULE 20 — Prompt Assembly & Safety (PRMT)

**Epics**

- EPIC-PRMT-A: Context budgeting
- EPIC-PRMT-B: Layered, injection-resistant prompt assembly

### STORY-PRMT-01: Safe, budgeted, layered prompt assembly

**As the:** agent execution worker
**I want:** to budget context and assemble clearly-separated platform/agent/evidence/user layers with a citation protocol
**So that:** generation is grounded, injection-resistant, and never overflows or silently truncates the agent prompt (FR-RET-006, FR-RUN-006, ASDD §16.1/§16.2, §20.2)

#### Business Value
Security and correctness core of generation; prevents prompt-injection and overflow failures.

#### Story Acceptance Criteria
- Budget computed from model context minus fixed layers + output reservation; impossible configs rejected before AOAI (ContextOverflow); agent prompt never silently truncated.
- Assembled request separates platform safety instructions, agent prompt, untrusted evidence (delimited), user request, and output/citation protocol.

#### Dependencies
STORY-RET-01, STORY-AOI-01.

#### Tasks
`TASK-PRMT-01`, `TASK-PRMT-02`

---

### TASK-PRMT-01: Context budget calculation and overflow rejection
**Module:** 20 — Prompt Assembly
**Epic:** EPIC-PRMT-A
**Story:** STORY-PRMT-01
**Priority:** Critical
**Implementation order:** 88
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-AOI-04, TASK-AGT-03
**Blocks:** Retrieval budget, generation
**Primary role:** Backend

#### Objective
Compute available evidence tokens = context capacity − platform instructions − agent prompt − user request − output reservation − protocol overhead, and reject impossible configurations before calling AOAI.

#### Requirements Covered
FR-RET-006, FR-AGT-003 (no silent truncation), ASDD §15.5, §16.2.

#### Detailed Description
Estimate each fixed layer's tokens; reserve MaximumOutputTokens; if fixed content alone exceeds the context window, fail the run with `ContextOverflow` (clear configuration error) before any AOAI call. Provide the evidence budget to retrieval (module 16). The agent prompt is never truncated.

#### Technical Approach
- Token estimator (AOI-04) per layer; deployment context/output limits from the registry.

#### API or Event Contracts
Not applicable.

#### Data Changes
Records effective budget on the run.

#### Security and Authorization
No secrets in the prompt.

#### Error and Recovery Behavior
Impossible config → terminal `ContextOverflow` pre-AOAI.

#### Acceptance Criteria
- Given fixed layers exceeding the context window, when budgeting, then the run fails with a clear ContextOverflow before calling AOAI.
- Given a valid config, then an evidence-token budget is produced for retrieval.

#### Test Requirements
- Unit: budget math + overflow; Integration: pre-AOAI rejection.

#### Definition of Done
- [ ] Budget calc + overflow rejection feeding retrieval.

#### Implementation Notes
Failure scenario "prompt exceeds model context" covered here.

#### Out of Scope
Assembly (PRMT-02).

---

### TASK-PRMT-02: Layered prompt assembly with injection resistance and citation protocol
**Module:** 20 — Prompt Assembly
**Epic:** EPIC-PRMT-B
**Story:** STORY-PRMT-01
**Priority:** Critical
**Implementation order:** 89
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-PRMT-01, TASK-RET-04
**Blocks:** Generation
**Primary role:** Backend / Security

#### Objective
Assemble the model request as clearly-separated layers: platform safety/grounding instructions, agent prompt snapshot, untrusted evidence in strong delimiters with SOURCE IDs, the user request, and the Markdown+citation output protocol.

#### Requirements Covered
FR-RUN-006, FR-SEC-001 (injection), ASDD §16.1, §17.2, §20.2.

#### Detailed Description
Build the request so retrieved evidence is explicitly marked untrusted data inside strongly-delimited source blocks (filename + heading path + content per SOURCE-NNN), instructing the model that evidence content is data, not instructions. Include a constrained citation-marker protocol (D4) the model must use, and forbid inventing sources. No secrets/tools exposed.

#### Technical Approach
- Deterministic template with delimiters; evidence blocks keyed by SOURCE IDs; output-protocol instructions.

#### API or Event Contracts
Produces the AOAI request (module 21 sends it).

#### Data Changes
Not applicable.

#### Security and Authorization
Evidence as untrusted data; instructions inside documents must not override platform/agent instructions (FR-SEC-001); no secrets in the prompt.

#### Error and Recovery Behavior
Not applicable (assembly).

#### Acceptance Criteria
- Given uploaded content containing "ignore previous instructions", when assembled, then it appears only inside delimited untrusted evidence and does not alter platform/agent instructions.
- Given evidence, then each source block carries SOURCE ID + filename + heading path.

#### Test Requirements
- Security: prompt-injection text in evidence does not change behavior; Unit: layer separation + protocol.

#### Definition of Done
- [ ] Layered, injection-resistant assembly with citation protocol.

#### Implementation Notes
Server-side citation parsing is authoritative (module 22).

#### Out of Scope
Sending/parsing (modules 21/22).

---

## MODULE 21 — Markdown Generation & Validation (GEN)

**Epics**

- EPIC-GEN-A: Generation request & parameter recording
- EPIC-GEN-B: Output extraction, validation & safety

### STORY-GEN-01: Generate and validate grounded Markdown

**As the:** agent execution worker
**I want:** to call AOAI with the assembled request, record requested vs effective params, extract and validate Markdown/citations, and preserve the original output
**So that:** outputs are safe, valid, and traceable (FR-OUT-002/003, ASDD §16, §17.2)

#### Business Value
Produces the user-facing artifact with safety and traceability guarantees.

#### Story Acceptance Criteria
- Generation records requested and effective params; uses structured output when supported (D4) else marker parsing.
- Output is validated as Markdown, unsafe/invalid content handled, and the original generated Markdown is preserved.

#### Dependencies
STORY-PRMT-01, STORY-AOI-01.

#### Tasks
`TASK-GEN-01`, `TASK-GEN-02`, `TASK-GEN-03`, `TASK-GEN-04`

---

### TASK-GEN-01: Generation request and requested/effective parameter recording
**Module:** 21 — Generation
**Epic:** EPIC-GEN-A
**Story:** STORY-GEN-01
**Priority:** Critical
**Implementation order:** 90
**Estimated size:** S
**Can run in parallel:** No
**Dependencies:** TASK-AOI-03, TASK-PRMT-02
**Blocks:** Output persistence
**Primary role:** Backend

#### Objective
Send the assembled request to the AOAI chat deployment with snapshot params, and record both requested and effective (adjusted) parameters on the run.

#### Requirements Covered
FR-PAR-004, FR-RUN-004, ASDD §16.

#### Detailed Description
Apply temperature/max-output/TopP (as supported), send via `ITextGenerationService`, and store `RequestedParametersJson` vs `EffectiveParametersJson` (reflecting any capability adjustments). Respect timeouts/retries (AOI-03).

#### Technical Approach
- Map snapshot params to the request; capture effective values.

#### API or Event Contracts
Not applicable (internal).

#### Data Changes
Writes requested/effective params on the run.

#### Security and Authorization
No secrets logged; content not logged by default.

#### Error and Recovery Behavior
Throttling/timeout retried; invalid deployment terminal.

#### Acceptance Criteria
- Given a run, when generating, then requested and effective params are both recorded.
- Given an unsupported param, then the effective value reflects the recorded adjustment.

#### Test Requirements
- Integration (fake AOAI): param recording; retry/timeout behavior.

#### Definition of Done
- [ ] Generation call + requested/effective param recording.

#### Implementation Notes
Effective params support the run details view (module 29).

#### Out of Scope
Output validation (GEN-02).

---

### TASK-GEN-02: Markdown extraction and validation
**Module:** 21 — Generation
**Epic:** EPIC-GEN-B
**Story:** STORY-GEN-01
**Priority:** Critical
**Implementation order:** 91
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-GEN-01
**Blocks:** Output persistence, citations
**Primary role:** Backend / Security

#### Objective
Extract the Markdown (and citation markers) from the response, validate it as well-formed Markdown, handle invalid/unsafe content, and preserve the original text.

#### Requirements Covered
FR-OUT-002, FR-OUT-003 (original preserved), ASDD §16, §20.3.

#### Detailed Description
Parse the response into Markdown + citation markers; validate Markdown structure; if invalid or empty, classify `OutputValidationFailure`. Store the original generated Markdown immutably (module 23). Server-side sanitation policy is applied at render (module 24) but obviously-unsafe constructs are flagged here.

#### Technical Approach
- Markdown validation via the GFM parser; marker extraction feeds module 22.

#### API or Event Contracts
Not applicable.

#### Data Changes
Produces validated Markdown + parsed citation markers.

#### Security and Authorization
Output treated as untrusted (raw HTML disabled downstream).

#### Error and Recovery Behavior
Invalid output → `OutputValidationFailure` (retry per policy); original preserved.

#### Acceptance Criteria
- Given a valid Markdown response, when validated, then it passes and citation markers are extracted.
- Given invalid/empty output, then an output-validation failure is recorded.

#### Test Requirements
- Unit: extraction + validation; invalid/empty handling.

#### Definition of Done
- [ ] Extraction + validation + original preservation + marker parsing.

#### Implementation Notes
Sanitization enforced at render time (module 24).

#### Out of Scope
Citation validation (module 22), rendering (module 24).

---

### TASK-GEN-03: Generation failure behavior
**Module:** 21 — Generation
**Epic:** EPIC-GEN-B
**Story:** STORY-GEN-01
**Priority:** High
**Implementation order:** 92
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-GEN-01, TASK-JOB-06
**Blocks:** None
**Primary role:** Backend

#### Objective
Classify and record generation failures (throttling/timeout/invalid deployment/validation) with safe messages and retry eligibility.

#### Requirements Covered
FR-AOI-005, ASDD §22, failure scenarios.

#### Detailed Description
Map generation errors to categories, record safe message + correlation on the run, and mark retryable/terminal per classifier.

#### Technical Approach
- Reuse classifier (JOB-06) + error contract (module 32).

#### API or Event Contracts
Not applicable.

#### Data Changes
Failure fields on the run.

#### Security and Authorization
No secrets/raw responses in user-facing messages.

#### Error and Recovery Behavior
Retryable transient; terminal invalid/validation.

#### Acceptance Criteria
- Given AOAI throttling during generation, then retried within limits; given invalid deployment, terminal.

#### Test Requirements
- Integration: each failure category.

#### Definition of Done
- [ ] Generation failure classification + recording.

#### Implementation Notes
Aligns with module 19 failure handling.

#### Out of Scope
UI (module 29).

---

### TASK-GEN-04: Opportunistic structured output support
**Module:** 21 — Generation
**Epic:** EPIC-GEN-A
**Story:** STORY-GEN-01
**Priority:** Low
**Implementation order:** 93
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-GEN-02, TASK-CIT-03
**Blocks:** None
**Primary role:** Backend

#### Objective
When the selected deployment reliably supports structured output, request a structured Markdown+citations response; otherwise fall back to constrained citation-marker parsing (D4).

#### Requirements Covered
ASDD §17.2, D4.

#### Detailed Description
Detect structured-output capability from the registry; if supported, request a schema-constrained response and map it to Markdown + citations; else use marker parsing (module 22). Includes a clarification note that reliability varies per deployment.

#### Technical Approach
- Capability flag in the registry; schema request when enabled; graceful fallback.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Citations still validated server-side (module 22).

#### Error and Recovery Behavior
Structured failure → fall back to marker parsing.

#### Acceptance Criteria
- Given a structured-capable deployment, when generating, then a structured response is parsed to Markdown+citations; given an incapable one, marker parsing is used.

#### Test Requirements
- Integration (fake AOAI): both paths.

#### Definition of Done
- [ ] Opportunistic structured output with marker fallback.

#### Implementation Notes
Clarification (D4): marker parsing remains the guaranteed path.

#### Out of Scope
Citation validation (module 22).

---

# PHASE 9 — Outputs, Citations & Rendering

## MODULE 22 — Citation Management (CIT)

**Epics**

- EPIC-CIT-A: Run evidence & citation records
- EPIC-CIT-B: Citation parsing, validation & evidence viewer

### STORY-CIT-01: Structural, validated citations bound to run evidence

**As a:** user
**I want:** citations stored as records linked to retrieved chunks and validated against the run's evidence
**So that:** citations are trustworthy, application-isolated, and inspectable (FR-CIT-*, ASDD §17)

#### Business Value
Trustworthy grounding; prevents fabricated or cross-application citations.

#### Story Acceptance Criteria
- RunEvidence + Citation records connect output → evidence → chunk → file; citations reference only current-run, same-application evidence.
- Unknown/cross-application citation markers are rejected; the evidence drawer shows filename, heading path, passage, and surrounding text.

#### Dependencies
STORY-RET-01, STORY-GEN-01.

#### Tasks
`TASK-CIT-01`, `TASK-CIT-02`, `TASK-CIT-03`, `TASK-CIT-04`

---

### TASK-CIT-01: RunEvidence and Citation schema
**Module:** 22 — Citations
**Epic:** EPIC-CIT-A
**Story:** STORY-CIT-01
**Priority:** Critical
**Implementation order:** 94
**Estimated size:** S
**Can run in parallel:** No
**Dependencies:** TASK-PG-01, TASK-RUN-01
**Blocks:** Citation validation
**Primary role:** Database / Backend

#### Objective
Define `RunEvidence` (run → chunk with SOURCE ID + snapshot metadata) and `Citation` (output → run evidence) tables per ASDD §17.1.

#### Requirements Covered
FR-CIT-001, ASDD §9, §17.1.

#### Detailed Description
`RunEvidence` stores the SOURCE-NNN mapping to a chunk/file with snapshot filename/heading/passage (so citations survive later source deletion). `Citation` links a `GeneratedOutput` to a `RunEvidence`. App-scoped.

#### Technical Approach
- EF entities + FKs; snapshot passage/heading stored on RunEvidence.

#### API or Event Contracts
Not applicable.

#### Data Changes
Tables `execution.run_evidence`, `outputs.citations`.

#### Security and Authorization
App boundary on both.

#### Error and Recovery Behavior
Snapshotting preserves citation display after source deletion (FR-CIT-004).

#### Acceptance Criteria
- Given a run, when evidence is used, then RunEvidence rows capture SOURCE ID + snapshot metadata; citations link to them.

#### Test Requirements
- Integration: persistence + FK integrity.

#### Definition of Done
- [ ] RunEvidence + Citation schema with snapshots.

#### Implementation Notes
Snapshot passage supports deleted-source display.

#### Out of Scope
Parsing/validation (CIT-03).

---

### TASK-CIT-02: Persist run evidence
**Module:** 22 — Citations
**Epic:** EPIC-CIT-A
**Story:** STORY-CIT-01
**Priority:** High
**Implementation order:** 95
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-CIT-01, TASK-RET-04
**Blocks:** Citation validation
**Primary role:** Backend

#### Objective
Persist the evidence package used for a run as `RunEvidence` rows with SOURCE IDs and snapshot metadata.

#### Requirements Covered
FR-CIT-001, FR-RUN-004, ASDD §17.1.

#### Detailed Description
When the run assembles evidence (module 16/20), write RunEvidence rows capturing SOURCE-NNN → chunk/file plus filename/heading/passage snapshot and processing time.

#### Technical Approach
- Write during the Building-context/Saving stage.

#### API or Event Contracts
Not applicable.

#### Data Changes
Inserts `run_evidence` rows.

#### Security and Authorization
App-scoped.

#### Error and Recovery Behavior
Idempotent per run.

#### Acceptance Criteria
- Given a run, when evidence is assembled, then RunEvidence rows exist for each SOURCE ID with snapshot metadata.

#### Test Requirements
- Integration: evidence persistence.

#### Definition of Done
- [ ] RunEvidence persisted per run.

#### Implementation Notes
Basis for citation validation.

#### Out of Scope
Validation (CIT-03).

---

### TASK-CIT-03: Citation marker parsing and validation
**Module:** 22 — Citations
**Epic:** EPIC-CIT-B
**Story:** STORY-CIT-01
**Priority:** Critical
**Implementation order:** 96
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-GEN-02, TASK-CIT-02
**Blocks:** Output persistence
**Primary role:** Backend / Security

#### Objective
Parse citation markers from the generated Markdown and validate each references a SOURCE ID present in this run's evidence and the same application; reject unknown/cross-application markers.

#### Requirements Covered
FR-CIT-001, FR-CIT-002, ASDD §17.2, §17.3, failure scenario "invalid citation".

#### Detailed Description
Parse the constrained citation-marker syntax (D4); for each marker, verify the SOURCE ID exists in the run's RunEvidence (same application); create Citation records for valid ones; reject/flag unknown or cross-application markers. If `CitationsRequired` and none valid, handle per policy.

#### Technical Approach
- Deterministic marker regex/grammar; validate against RunEvidence set.

#### API or Event Contracts
Not applicable.

#### Data Changes
Inserts `citations`; records invalid-citation count (observability).

#### Security and Authorization
Cross-application/unknown citations rejected (FR-CIT-002).

#### Error and Recovery Behavior
Invalid citations flagged; run may still complete with valid ones per policy; metric recorded.

#### Acceptance Criteria
- Given a marker referencing a valid run SOURCE ID, when validated, then a Citation is created.
- Given a marker referencing an unknown or another application's source, then it is rejected and not stored.

#### Test Requirements
- Unit: parsing; Security: unknown + cross-application rejection; Integration: valid citations persisted.

#### Definition of Done
- [ ] Marker parsing + strict validation + Citation persistence.

#### Implementation Notes
Server-side validation is authoritative (never trust model-named files).

#### Out of Scope
Evidence drawer UI (CIT-04).

---

### TASK-CIT-04: Citation evidence drawer and deleted-source handling
**Module:** 22 — Citations
**Epic:** EPIC-CIT-B
**Story:** STORY-CIT-01
**Priority:** High
**Implementation order:** 97
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-CIT-03, TASK-OUT-02
**Blocks:** None
**Primary role:** Full Stack

#### Objective
Provide an authorized citation endpoint + evidence drawer showing filename, heading path, matching passage, surrounding text, and processing time; render "source unavailable" when the source was deleted.

#### Requirements Covered
FR-CIT-003, FR-CIT-004, ASDD §17.4.

#### Detailed Description
`GET …/outputs/{id}/citations/{citationId}` returns snapshot evidence (works even after source deletion). The drawer displays passage + surrounding text (offsets) and marks deleted sources unavailable. Authorized by application membership.

#### Technical Approach
- Serve from RunEvidence snapshot; surrounding text via stored offsets; app-scoped authorization.

#### API or Event Contracts
- `GET /api/applications/{appId}/outputs/{id}/citations` and `/{citationId}` → evidence detail.

#### Data Changes
Read-only.

#### Security and Authorization
App boundary; cross-application citation access denied (failure scenario).

#### Error and Recovery Behavior
Deleted source → "source unavailable" per retention policy.

#### Acceptance Criteria
- Given a valid citation, when opened, then filename/heading/passage/surrounding text/processing time display.
- Given a deleted source, then the citation shows source unavailable but the output remains.
- Given a cross-application request, then denied.

#### Test Requirements
- Integration: drawer data; Security: cross-app denial; deleted-source display.

#### Definition of Done
- [ ] Citation endpoint + evidence drawer + deleted-source handling.

#### Implementation Notes
Snapshot passage avoids depending on live chunks.

#### Out of Scope
Output views (module 23).

---

## MODULE 23 — Output Management (OUT)

**Epics**

- EPIC-OUT-A: Output entity, views & editing
- EPIC-OUT-B: Actions (copy/download/regenerate/delete) & library

### STORY-OUT-01: Manage generated Markdown outputs and library

**As an:** Application Admin
**I want:** to view (preview/source/split), edit, copy, download, regenerate, and delete outputs, and browse a searchable library
**So that:** generated knowledge is usable and traceable while preserving the original (FR-OUT-*, ASDD §7.5)

#### Business Value
The consumable product of the platform; primary-flow steps 15-17.

#### Story Acceptance Criteria
- Output stores title, original + current Markdown, run/agent/param snapshots, citations, created/edited metadata; editing preserves the original.
- Preview/source/split views; copy/download `.md`; regenerate creates a new run; delete soft-deletes; library supports search/filter; role permissions enforced.

#### Dependencies
STORY-GEN-01, STORY-CIT-01, STORY-MDR-01.

#### Tasks
`TASK-OUT-01`, `TASK-OUT-02`, `TASK-OUT-03`, `TASK-OUT-04`, `TASK-OUT-05`

---

### TASK-OUT-01: GeneratedOutput entity and persistence
**Module:** 23 — Outputs
**Epic:** EPIC-OUT-A
**Story:** STORY-OUT-01
**Priority:** Critical
**Implementation order:** 98
**Estimated size:** S
**Can run in parallel:** No
**Dependencies:** TASK-PG-01, TASK-RUN-02
**Blocks:** Output views/actions
**Primary role:** Database / Backend

#### Objective
Define `GeneratedOutput` (ASDD §9.5) storing title, OriginalMarkdown, CurrentMarkdown, IsEdited, created/edited metadata, IsDeleted, and run linkage.

#### Requirements Covered
FR-OUT-003, FR-OUT-004 (original retained), ASDD §9.5.

#### Detailed Description
Persist the output at run completion with OriginalMarkdown = CurrentMarkdown initially; link to AgentRunId; index `generated_outputs(application_id, created_at)`. Edits update CurrentMarkdown + IsEdited, never OriginalMarkdown.

#### Technical Approach
- EF entity + index; original immutable.

#### API or Event Contracts
Not applicable.

#### Data Changes
Table `outputs.generated_outputs` + index.

#### Security and Authorization
App-scoped.

#### Error and Recovery Behavior
Original preserved on all edits.

#### Acceptance Criteria
- Given a completed run, when persisted, then an output with original=current is stored linked to the run.

#### Test Requirements
- Integration: persistence + original immutability.

#### Definition of Done
- [ ] Output entity + persistence.

#### Implementation Notes
Snapshots come via the linked run (module 19).

#### Out of Scope
Views/actions (OUT-02..05).

---

### TASK-OUT-02: Output detail endpoint and views (preview/source/split)
**Module:** 23 — Outputs
**Epic:** EPIC-OUT-A
**Story:** STORY-OUT-01
**Priority:** High
**Implementation order:** 99
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-OUT-01, TASK-MDR-01, TASK-CIT-04
**Blocks:** None
**Primary role:** Full Stack

#### Objective
Expose the output detail (with snapshots + citations) and build the `/applications/:id/outputs/:outputId` screen with Preview, Markdown Source, and Split views + citation drawer.

#### Requirements Covered
FR-OUT-001, FR-OUT-003, ASDD §7.5.

#### Detailed Description
Detail endpoint returns title, current + original Markdown, run/agent/param snapshots, citations, and metadata. UI renders sanitized preview (module 24), raw source, split view, and the citation drawer (module 22). Role-appropriate actions.

#### Technical Approach
- TanStack Query; sanitized renderer; view toggles.

#### API or Event Contracts
- `GET /api/applications/{appId}/outputs/{id}` → detail.

#### Data Changes
Read-only.

#### Security and Authorization
`CanViewOutput`; app boundary; viewers can view/copy/download.

#### Error and Recovery Behavior
Missing/deleted output handled; sanitized rendering.

#### Acceptance Criteria
- Given an output, when opened, then preview/source/split render and citations open in the drawer.
- Given a viewer, then view/copy/download are available, edit is not.

#### Test Requirements
- Component: view toggles + sanitization; Integration: detail endpoint authorization.

#### Definition of Done
- [ ] Output detail endpoint + three views + citation drawer.

#### Implementation Notes
Rendering uses module 24's sanitizer. The Preview/Source/Split layout and evidence drawer use the shared evidence-first workspace shell + resizable panels (`TASK-UX-10`, `TASK-UX-07`; decision D19).

#### Out of Scope
Edit/actions (OUT-03/04); shared workspace/panel primitives (module 37).

---

### TASK-OUT-03: Admin edit with original preservation
**Module:** 23 — Outputs
**Epic:** EPIC-OUT-A
**Story:** STORY-OUT-01
**Priority:** High
**Implementation order:** 100
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-OUT-01, TASK-AUTHZ-02
**Blocks:** None
**Primary role:** Full Stack

#### Objective
Allow Application Admins to edit CurrentMarkdown while retaining OriginalMarkdown, updating IsEdited/edited metadata and auditing the change.

#### Requirements Covered
FR-OUT-004, FR-OUT-005, ASDD §7.5.

#### Detailed Description
Edit endpoint updates CurrentMarkdown + IsEdited + LastEditedBy/At; OriginalMarkdown never changes. Editor uses the same sanitizer for preview. Audited.

#### Technical Approach
- `PUT …/outputs/{id}` updating current only; audit event.

#### API or Event Contracts
- `PUT /api/applications/{appId}/outputs/{id} {currentMarkdown}` → `200`.

#### Data Changes
Updates current Markdown + edit metadata; audit.

#### Security and Authorization
`CanManageOutput` (admin); viewers denied.

#### Error and Recovery Behavior
Concurrency-safe; original immutable.

#### Acceptance Criteria
- Given an admin edit, when saved, then CurrentMarkdown updates, OriginalMarkdown is unchanged, and IsEdited=true.
- Given a viewer, when attempting edit, then denied.

#### Test Requirements
- Integration: edit + original preserved; Security: viewer denied.

#### Definition of Done
- [ ] Edit with original preservation + audit.

#### Implementation Notes
Original retained within the run/output record (FR-OUT-004).

#### Out of Scope
Regenerate (OUT-04).

---

### TASK-OUT-04: Output actions — copy, download .md, regenerate, delete
**Module:** 23 — Outputs
**Epic:** EPIC-OUT-B
**Story:** STORY-OUT-01
**Priority:** High
**Implementation order:** 101
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-OUT-02, TASK-RUN-01
**Blocks:** None
**Primary role:** Full Stack

#### Objective
Implement copy, download `.md`, regenerate-as-new-run, and soft-delete, with role-appropriate permissions and confirmation for delete.

#### Requirements Covered
FR-OUT-005, ASDD §7.5.

#### Detailed Description
Copy/download available to all viewers; regenerate creates a new run (module 19) referencing the same agent (new snapshot) — the original output/run remains intact; delete soft-deletes (admins) with confirmation + audit; historical citations remain.

#### Technical Approach
- Download streams the current Markdown as `.md`; regenerate posts a new run; delete sets IsDeleted.

#### API or Event Contracts
- `POST …/outputs/{id}/regenerate` → new run; `DELETE …/outputs/{id}` (admin).

#### Data Changes
Soft-delete flag; new run for regenerate.

#### Security and Authorization
Copy/download: viewers+; regenerate/delete: admins; confirmation for delete; audited.

#### Error and Recovery Behavior
Delete idempotent; regenerate independent of the original.

#### Acceptance Criteria
- Given a viewer, when downloading, then a `.md` file is produced.
- Given an admin, when regenerating, then a new run is created and the original remains.
- Given delete, when confirmed, then the output is soft-deleted and audited; citations remain.

#### Test Requirements
- Integration: each action + permissions; Security: viewer cannot delete/regenerate.

#### Definition of Done
- [ ] Copy/download/regenerate/delete with permissions + audit.

#### Implementation Notes
Regenerate reuses run submission (module 19).

#### Out of Scope
Library (OUT-05).

---

### TASK-OUT-05: Output library with search and filters
**Module:** 23 — Outputs
**Epic:** EPIC-OUT-B
**Story:** STORY-OUT-01
**Priority:** Medium
**Implementation order:** 102
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-OUT-01, TASK-OUT-02
**Blocks:** None
**Primary role:** Full Stack

#### Objective
Build the output library (`/applications/:id/outputs`) with search/filter by title, agent, creator, date, and run status.

#### Requirements Covered
FR-OUT-006, ASDD §7.2.

#### Detailed Description
Paged/filterable list endpoint + UI; role-appropriate row actions; loading/empty/error states.

#### Technical Approach
- `GET …/outputs?search=&agentId=&creator=&from=&to=&status=&page=`.

#### API or Event Contracts
- `GET /api/applications/{appId}/outputs` (filtered/paged).

#### Data Changes
Read-only.

#### Security and Authorization
`CanViewOutput`; app boundary.

#### Error and Recovery Behavior
Standard states.

#### Acceptance Criteria
- Given outputs, when filtering by agent/date/status, then the list narrows accordingly with correct authorization.

#### Test Requirements
- Integration: filtering; Component: states + role actions.

#### Definition of Done
- [ ] Output library with search/filter + role actions.

#### Implementation Notes
Reuse list patterns from knowledge/users.

#### Out of Scope
Dashboards (modules 26-29).

---

## MODULE 24 — Secure Markdown Rendering (MDR)

**Epics**

- EPIC-MDR-A: Sanitized rendering pipeline & security headers

### STORY-MDR-01: Render untrusted Markdown safely

**As a:** frontend
**I want:** a hardened Markdown pipeline (GFM, raw HTML disabled, sanitized, safe URLs) plus security headers
**So that:** generated/untrusted content cannot execute scripts or unsafe embeds (FR-OUT-002, ASDD §20.3)

#### Business Value
Prevents XSS from model output or uploaded content displayed in the app.

#### Story Acceptance Criteria
- Raw HTML disabled; output sanitized; only safe URL protocols; scripts/event-handlers/embeds blocked; security headers set; malicious Markdown tests pass.

#### Dependencies
STORY-FND-01, module 21/23.

#### Tasks
`TASK-MDR-01`, `TASK-MDR-02`

---

### TASK-MDR-01: Hardened Markdown rendering component
**Module:** 24 — Markdown Rendering
**Epic:** EPIC-MDR-A
**Story:** STORY-MDR-01
**Priority:** Critical
**Implementation order:** 103
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-FND-02
**Blocks:** Outputs, agent test, preview
**Primary role:** Frontend / Security

#### Objective
Build the shared `<SafeMarkdown>` component: `react-markdown` + `remark-gfm` with raw HTML disabled and sanitization, allowing only safe URL protocols and blocking scripts/event-handlers/iframes.

#### Requirements Covered
FR-OUT-002, ASDD §20.3, malicious-Markdown tests.

#### Detailed Description
Configure the pipeline: GFM enabled; raw HTML disabled; `rehype-sanitize`/DOMPurify with an allowlist; validate URL protocols (`http/https/mailto` only, block `javascript:`/`data:` where unsafe); strip event handlers; disallow iframes/embeds. Used everywhere Markdown is displayed.

#### Technical Approach
- `react-markdown` with restricted plugins + sanitizer schema; central component.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Treats all Markdown as untrusted (model output + previews).

#### Error and Recovery Behavior
Unsafe nodes stripped, not rendered.

#### Acceptance Criteria
- Given Markdown containing `<script>` or `javascript:` links or `onerror` handlers, when rendered, then none execute and unsafe nodes are stripped.
- Given safe Markdown (headings/tables/code/links), then it renders correctly.

#### Test Requirements
- Component/security: malicious Markdown payloads neutralized; safe content preserved.

#### Definition of Done
- [ ] Shared hardened Markdown component used across the app.

#### Implementation Notes
Single component avoids inconsistent sanitization. `<SafeMarkdown>` is consumed by the evidence-first output/run workspace (`TASK-UX-10`) and all preview surfaces (decision D19).

#### Out of Scope
Security headers (MDR-02); workspace layout (module 37).

---

### TASK-MDR-02: Security headers and CSP
**Module:** 24 — Markdown Rendering
**Epic:** EPIC-MDR-A
**Story:** STORY-MDR-01
**Priority:** High
**Implementation order:** 104
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-04, TASK-MDR-01
**Blocks:** None
**Primary role:** DevOps / Security

#### Objective
Set baseline security headers (CSP, X-Content-Type-Options, Referrer-Policy, frame-ancestors) at the frontend server and API.

#### Requirements Covered
ASDD §20.3 (embedded-content restrictions), FR-SEC.

#### Detailed Description
Configure a Content-Security-Policy restricting scripts/styles/frames, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, and `frame-ancestors 'none'`. Ensure they don't break the SPA/SignalR while blocking injection vectors.

#### Technical Approach
- nginx headers (frontend) + API response headers; tune CSP for Vite build + SignalR.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Defense-in-depth alongside sanitization.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given the app, when loaded, then CSP + security headers are present and the app + SignalR still function.

#### Test Requirements
- Integration: header presence; smoke: app + SignalR work under CSP.

#### Definition of Done
- [ ] Security headers + CSP applied without breaking functionality.

#### Implementation Notes
Iterate CSP to avoid blocking legitimate assets.

#### Out of Scope
Rendering logic (MDR-01).

---

# PHASE 10 — Realtime & Dashboards

## MODULE 25 — SignalR (SIG)

**Epics**

- EPIC-SIG-A: Hub, groups & event contract
- EPIC-SIG-B: Publishing, sequencing, reconnection & REST recovery

### STORY-SIG-01: Reliable realtime updates with REST recovery

**As a:** user
**I want:** live file/run/dashboard updates via authorized SignalR groups with sequence numbers and reconnection
**So that:** I see progress in real time and recover accurate state after disconnects (FR-DSH-003/004, ASDD §18)

#### Business Value
Responsive UX for processing/execution; PostgreSQL remains authoritative.

#### Story Acceptance Criteria
- Hub with authorized groups (user/application/run/operations); typed events with sequence numbers; stale events ignored.
- Workers persist state before publishing; on reconnect the client refreshes via REST.

#### Dependencies
STORY-AUTHZ-01, modules 10/19.

#### Tasks
`TASK-SIG-01`, `TASK-SIG-02`, `TASK-SIG-03`, `TASK-SIG-04`, `TASK-SIG-05`, `TASK-SIG-06`

---

### TASK-SIG-01: SignalR hub, authentication, and groups
**Module:** 25 — SignalR
**Epic:** EPIC-SIG-A
**Story:** STORY-SIG-01
**Priority:** Critical
**Implementation order:** 105
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-IDN-02, TASK-AUTHZ-03
**Blocks:** Dashboards
**Primary role:** Backend

#### Objective
Implement the authenticated SignalR hub with authorized group management (`user:{id}`, `application:{id}`, `run:{id}`, `platform:operations`).

#### Requirements Covered
ASDD §18.2, FR-DSH-003, FR-SEC-004.

#### Detailed Description
Hub requires auth (cookie); `user:{id}` auto-bound; join methods for application/run/operations validated via membership (AUTHZ-03). Connection lifecycle managed; unauthorized joins rejected.

#### Technical Approach
- Cookie-authenticated hub; group join validation via membership service.

#### API or Event Contracts
Hub methods: `JoinApplication/LeaveApplication/JoinRun/LeaveRun`.

#### Data Changes
Not applicable.

#### Security and Authorization
Group joins authorized server-side.

#### Error and Recovery Behavior
Unauthorized join rejected; reconnection handled (SIG-05).

#### Acceptance Criteria
- Given an authenticated user, when joining an authorized application group, then they receive that app's events; unauthorized joins are rejected.

#### Test Requirements
- Integration/security: authorized vs unauthorized joins.

#### Definition of Done
- [ ] Authenticated hub + authorized groups.

#### Implementation Notes
Same-origin proxy ensures cookies reach the hub (FND-06).

#### Out of Scope
Event publishing (SIG-03/04).

---

### TASK-SIG-02: Event contract and envelope
**Module:** 25 — SignalR
**Epic:** EPIC-SIG-A
**Story:** STORY-SIG-01
**Priority:** High
**Implementation order:** 106
**Estimated size:** XS
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-03
**Blocks:** Publishing
**Primary role:** Backend

#### Objective
Finalize the event envelope (event id, type, applicationId, entityId, UTC timestamp, entity sequence number, safe payload) and the event catalog (ASDD §18.1).

#### Requirements Covered
ASDD §18.1, §18.3.

#### Detailed Description
Define events: `FileQueued/FileStageChanged/FileCompleted/FileFailed/RunQueued/RunStageChanged/RunCompleted/RunFailed/DashboardChanged`. Envelope carries a per-entity sequence number so clients ignore stale events; payloads are safe (no secrets/content).

#### Technical Approach
- Envelope from `Shared` (FND-03); per-entity monotonic sequence.

#### API or Event Contracts
Event catalog + envelope as above.

#### Data Changes
Sequence source (per entity) maintained.

#### Security and Authorization
Payloads safe; no secrets/content.

#### Error and Recovery Behavior
Sequence enables stale-event rejection (SIG-06).

#### Acceptance Criteria
- Given events, when emitted, then each carries type/app/entity/timestamp/sequence and a safe payload.

#### Test Requirements
- Unit: envelope + safe payload assertions.

#### Definition of Done
- [ ] Event catalog + envelope finalized.

#### Implementation Notes
Sequence per entity, not global.

#### Out of Scope
Publisher (SIG-03).

---

### TASK-SIG-03: File event publishing
**Module:** 25 — SignalR
**Epic:** EPIC-SIG-B
**Story:** STORY-SIG-01
**Priority:** High
**Implementation order:** 107
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-SIG-01, TASK-SIG-02, TASK-PRC-04
**Blocks:** Processing dashboard
**Primary role:** Backend

#### Objective
Publish file processing events to the appropriate application group after DB commit (source-of-truth).

#### Requirements Covered
ASDD §18.4, FR-DSH-003, FR-PRC.

#### Detailed Description
After the document worker persists stage/status, publish `FileQueued/StageChanged/Completed/Failed` to `application:{id}` with the sequence number. Never publish before persistence.

#### Technical Approach
- Publisher invoked post-commit from the worker.

#### API or Event Contracts
File events (SIG-02).

#### Data Changes
Not applicable.

#### Security and Authorization
Delivered only to authorized application group.

#### Error and Recovery Behavior
Lost events recovered via REST (SIG-06).

#### Acceptance Criteria
- Given file processing, when stages persist, then corresponding events publish to the app group with increasing sequence.

#### Test Requirements
- Integration: event emission ordering post-commit.

#### Definition of Done
- [ ] File events published post-commit.

#### Implementation Notes
Pairs with processing dashboard (module 28).

#### Out of Scope
Run events (SIG-04).

---

### TASK-SIG-04: Run and dashboard event publishing
**Module:** 25 — SignalR
**Epic:** EPIC-SIG-B
**Story:** STORY-SIG-01
**Priority:** High
**Implementation order:** 108
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-SIG-01, TASK-SIG-02, TASK-RUN-03
**Blocks:** Run dashboard
**Primary role:** Backend

#### Objective
Publish run events (`RunQueued/StageChanged/Completed/Failed`) to run/application groups and `DashboardChanged` to relevant groups after DB commit.

#### Requirements Covered
ASDD §18.1/§18.4, FR-RUN-007.

#### Detailed Description
After the agent worker persists run stage/status, publish run events to `run:{id}` + `application:{id}`; emit `DashboardChanged` to update counters. Post-commit only.

#### Technical Approach
- Publisher invoked post-commit from the agent worker.

#### API or Event Contracts
Run + dashboard events (SIG-02).

#### Data Changes
Not applicable.

#### Security and Authorization
Delivered to authorized run/app/operations groups.

#### Error and Recovery Behavior
REST recovery on loss.

#### Acceptance Criteria
- Given a run, when stages persist, then run events publish to run+app groups and dashboards update.

#### Test Requirements
- Integration: run/dashboard event emission.

#### Definition of Done
- [ ] Run + dashboard events published post-commit.

#### Implementation Notes
Feeds run dashboard (module 29).

#### Out of Scope
Reconnection (SIG-05).

---

### TASK-SIG-05: Frontend connection lifecycle, indicator, and reconnection
**Module:** 25 — SignalR
**Epic:** EPIC-SIG-B
**Story:** STORY-SIG-01
**Priority:** High
**Implementation order:** 109
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-SIG-01, TASK-IDN-03
**Blocks:** Live dashboards
**Primary role:** Frontend

#### Objective
Manage the SignalR connection lifecycle with a status indicator, automatic reconnection, and group re-join on reconnect.

#### Requirements Covered
FR-DSH-004, ASDD §18.

#### Detailed Description
A connection manager shows connected/reconnecting/disconnected; auto-reconnects with backoff; re-joins previously joined groups; triggers REST refresh after reconnect (SIG-06).

#### Technical Approach
- `HubConnectionBuilder` with automatic reconnect; indicator component; re-join on `onreconnected`.

#### API or Event Contracts
Consumes hub.

#### Data Changes
Not applicable.

#### Security and Authorization
Re-join re-authorizes groups server-side.

#### Error and Recovery Behavior
On reconnect, refresh via REST (SIG-06).

#### Acceptance Criteria
- Given a dropped connection, when it reconnects, then the indicator updates, groups re-join, and data refreshes via REST.

#### Test Requirements
- Component: lifecycle transitions + re-join.

#### Definition of Done
- [ ] Connection indicator + auto-reconnect + group re-join.

#### Implementation Notes
Pairs with SIG-06 REST recovery.

#### Out of Scope
Query invalidation logic (SIG-06).

---

### TASK-SIG-06: Sequence handling and REST recovery
**Module:** 25 — SignalR
**Epic:** EPIC-SIG-B
**Story:** STORY-SIG-01
**Priority:** High
**Implementation order:** 110
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-SIG-05, TASK-SIG-02
**Blocks:** None
**Primary role:** Frontend

#### Objective
Apply events by sequence (ignore stale), and after reconnect refresh authoritative state via REST/TanStack Query invalidation.

#### Requirements Covered
FR-DSH-004, ASDD §18.3 (ignore stale), §18.4 (REST source of truth).

#### Detailed Description
Track the last sequence per entity; ignore lower/equal sequences; on event, patch/invalidate the relevant queries; after reconnect, invalidate to force REST refresh so REST remains authoritative.

#### Technical Approach
- Per-entity sequence map; TanStack Query `invalidateQueries` on events/reconnect.

#### API or Event Contracts
Consumes events; triggers REST.

#### Data Changes
Not applicable.

#### Security and Authorization
Not applicable.

#### Error and Recovery Behavior
Stale events ignored; REST reconciles after loss.

#### Acceptance Criteria
- Given an out-of-order (stale) event, when received, then it is ignored.
- Given a reconnect, when it completes, then queries refresh from REST.

#### Test Requirements
- Component: stale-event rejection + reconnect refresh.

#### Definition of Done
- [ ] Sequence-based application + REST recovery.

#### Implementation Notes
REST is authoritative after reconnect (ASDD §18.4).

#### Out of Scope
Dashboard specifics (modules 26-29).

---

## MODULE 26 — Platform Dashboard (PDSH)

**Epics**

- EPIC-PDSH-A: Cross-application platform metrics & attention area

### STORY-PDSH-01: Platform-wide operational overview

**As a:** platform operator (Super Admin/Super Viewer)
**I want:** a dashboard of applications, users, files, processing/run summaries, failures, worker health, AOAI availability, and DLQ counts
**So that:** I can monitor the whole platform at a glance (FR-DSH-001, ASDD §21.1)

#### Business Value
Central operational visibility for platform roles.

#### Story Acceptance Criteria
- Aggregated counts across all applications with live updates and an attention-required area.

#### Dependencies
STORY-JOB-01, STORY-SIG-01, module 31.

#### Tasks
`TASK-PDSH-01`, `TASK-PDSH-02`

---

### TASK-PDSH-01: Platform dashboard aggregation endpoint
**Module:** 26 — Platform Dashboard
**Epic:** EPIC-PDSH-A
**Story:** STORY-PDSH-01
**Priority:** High
**Implementation order:** 111
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-JOB-01, TASK-SQS-04, TASK-AOI-05
**Blocks:** Platform dashboard UI
**Primary role:** Backend

#### Objective
Provide an aggregation endpoint returning platform metrics: applications, users, total files, queued/active processing, failed jobs, active/failed runs, worker health, AOAI availability, DLQ counts, recent activity, attention items.

#### Requirements Covered
FR-DSH-001, ASDD §21.1.

#### Detailed Description
Efficient aggregate queries across applications (respecting no-N+1), plus DLQ depth (SQS-04), worker heartbeat staleness (module 31), and AOAI availability (AOI-05). Restricted to platform roles.

#### Technical Approach
- Aggregate SQL using the ASDD §10 indexes; cache-free (D9) but efficient.

#### API or Event Contracts
- `GET /api/platform/dashboard` (SuperAdmin/SuperViewer).

#### Data Changes
Read-only.

#### Security and Authorization
Platform roles only.

#### Error and Recovery Behavior
Partial dependency failures degrade gracefully (e.g. AOAI unknown).

#### Acceptance Criteria
- Given a platform role, when loading, then all platform metrics + DLQ + worker health + AOAI availability return.
- Given a non-platform role, then `403`.

#### Test Requirements
- Integration: aggregation correctness; Security: role enforcement.

#### Definition of Done
- [ ] Platform aggregation endpoint.

#### Implementation Notes
No caching (D9); rely on indexes.

#### Out of Scope
UI (PDSH-02).

---

### TASK-PDSH-02: Platform dashboard UI with live updates
**Module:** 26 — Platform Dashboard
**Epic:** EPIC-PDSH-A
**Story:** STORY-PDSH-01
**Priority:** Medium
**Implementation order:** 112
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-PDSH-01, TASK-SIG-06
**Blocks:** None
**Primary role:** Frontend

#### Objective
Build `/platform/dashboard` showing metrics, attention items, and live updates via `DashboardChanged`.

#### Requirements Covered
FR-DSH-001, FR-DSH-003, ASDD §21.1.

#### Detailed Description
Cards for counts, a failures/attention section, worker/AOAI/DLQ health indicators; subscribe to platform/operations events for live counters; loading/empty/error states.

#### Technical Approach
- TanStack Query + SignalR invalidation; role-guarded route.

#### API or Event Contracts
Consumes PDSH-01 + `DashboardChanged`.

#### Data Changes
Not applicable.

#### Security and Authorization
Platform roles only.

#### Error and Recovery Behavior
REST refresh on reconnect.

#### Acceptance Criteria
- Given a platform role, when a job fails, then the dashboard failure count updates live.

#### Test Requirements
- Component: rendering + live update + role guard.

#### Definition of Done
- [ ] Platform dashboard UI with live metrics.

#### Implementation Notes
Reuse card components across dashboards.

#### Out of Scope
Per-application dashboard (module 27).

---

## MODULE 27 — Application Dashboard (ADSH)

**Epics**

- EPIC-ADSH-A: Application-scoped readiness & activity

### STORY-ADSH-01: Application operational overview

**As an:** application user
**I want:** a dashboard of knowledge readiness, file/agent/run/output summaries, and attention items for my application
**So that:** I understand my application's state quickly (FR-APP-004, ASDD §21.2)

#### Business Value
Focused per-application visibility with role-appropriate quick actions.

#### Story Acceptance Criteria
- Application-scoped metrics with live updates and attention items; role-specific quick actions.

#### Dependencies
STORY-APP-01, STORY-SIG-01.

#### Tasks
`TASK-ADSH-01`, `TASK-ADSH-02`

---

### TASK-ADSH-01: Application dashboard aggregation endpoint
**Module:** 27 — Application Dashboard
**Epic:** EPIC-ADSH-A
**Story:** STORY-ADSH-01
**Priority:** High
**Implementation order:** 113
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-APP-01, TASK-JOB-01
**Blocks:** App dashboard UI
**Primary role:** Backend

#### Objective
Provide an application-scoped aggregation endpoint: total/ready/queued/processing/failed files, active agents, running/failed executions, generated outputs, recent activity, attention items.

#### Requirements Covered
FR-APP-004, ASDD §21.2.

#### Detailed Description
Aggregate queries scoped by `application_id`; authorized by app membership; efficient via indexes.

#### Technical Approach
- App-scoped aggregate SQL.

#### API or Event Contracts
- `GET /api/applications/{appId}/dashboard`.

#### Data Changes
Read-only.

#### Security and Authorization
`CanViewApplication`; app boundary.

#### Error and Recovery Behavior
Graceful partial degradation.

#### Acceptance Criteria
- Given an app member, when loading, then app-scoped metrics return; cross-app access denied.

#### Test Requirements
- Integration: scoped aggregation; Security: cross-app denial.

#### Definition of Done
- [ ] App dashboard aggregation endpoint.

#### Implementation Notes
Mirror platform patterns, app-scoped.

#### Out of Scope
UI (ADSH-02).

---

### TASK-ADSH-02: Application dashboard UI with live updates and quick actions
**Module:** 27 — Application Dashboard
**Epic:** EPIC-ADSH-A
**Story:** STORY-ADSH-01
**Priority:** Medium
**Implementation order:** 114
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-ADSH-01, TASK-SIG-06, TASK-AUTHZ-04
**Blocks:** None
**Primary role:** Frontend

#### Objective
Build `/applications/:id/overview` dashboard with metrics, attention items, live updates, and role-specific quick actions (upload, create agent, run).

#### Requirements Covered
FR-APP-004, FR-DSH-003.

#### Detailed Description
App-scoped cards, attention area, live counters via `application:{id}` events; quick actions gated by role; loading/empty/error states.

#### Technical Approach
- Query + SignalR invalidation; capability-gated actions.

#### API or Event Contracts
Consumes ADSH-01 + app events.

#### Data Changes
Not applicable.

#### Security and Authorization
App membership; actions role-gated (server-enforced).

#### Error and Recovery Behavior
REST refresh on reconnect.

#### Acceptance Criteria
- Given an app admin, when a file completes, then readiness updates live and quick actions appear per role.

#### Test Requirements
- Component: rendering + live update + role gating.

#### Definition of Done
- [ ] App dashboard UI with live metrics + quick actions.

#### Implementation Notes
Reuse card components.

#### Out of Scope
Processing/run dashboards (modules 28/29).

---

## MODULE 28 — Processing Dashboard (PRDSH)

**Epics**

- EPIC-PRDSH-A: Processing views, details & retry

### STORY-PRDSH-01: Monitor and manage processing jobs

**As an:** application user (and platform roles)
**I want:** Active/Queued/Completed/Failed/All views with stage/progress/attempts/duration/correlation and retry
**So that:** I can monitor and recover processing (FR-DSH-002, FR-PRC-008, ASDD §21.3)

#### Business Value
Operational control over ingestion; enables failure investigation/retry.

#### Story Acceptance Criteria
- Tabbed views with filters, live updates, job details, and an authorized retry action; platform vs app scoping.

#### Dependencies
STORY-JOB-01, STORY-SIG-01.

#### Tasks
`TASK-PRDSH-01`, `TASK-PRDSH-02`

---

### TASK-PRDSH-01: Processing jobs query endpoints
**Module:** 28 — Processing Dashboard
**Epic:** EPIC-PRDSH-A
**Story:** STORY-PRDSH-01
**Priority:** High
**Implementation order:** 115
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-JOB-01, TASK-PRC-04
**Blocks:** Processing UI
**Primary role:** Backend

#### Objective
Provide filtered processing-job list + detail endpoints (platform-wide for platform roles, app-scoped for app users) with stage/progress/attempts/duration/correlation.

#### Requirements Covered
FR-DSH-001, FR-DSH-002, FR-PRC-008, ASDD §21.3.

#### Detailed Description
List with status tabs/filters; detail with failed stage, safe error, attempts, last attempt, retry eligibility, recommended action, correlation ID. Platform roles see all apps; app users see their app.

#### Technical Approach
- App-scoped or platform-scoped queries using `processing_jobs` indexes.

#### API or Event Contracts
- `GET /api/platform/processing?status=` and `GET /api/applications/{appId}/processing?status=`; `GET …/processing/{jobId}`.

#### Data Changes
Read-only.

#### Security and Authorization
Platform vs app scoping; retry authorized (JOB-06).

#### Error and Recovery Behavior
Retry action reuses JOB-06.

#### Acceptance Criteria
- Given a failed job, when viewed, then details + retry eligibility + correlation show; app users only see their app.

#### Test Requirements
- Integration: filtering + detail; Security: scoping.

#### Definition of Done
- [ ] Processing list/detail endpoints (platform + app).

#### Implementation Notes
Retry endpoint from JOB-06.

#### Out of Scope
UI (PRDSH-02).

---

### TASK-PRDSH-02: Processing dashboard UI (tabs, filters, retry, live)
**Module:** 28 — Processing Dashboard
**Epic:** EPIC-PRDSH-A
**Story:** STORY-PRDSH-01
**Priority:** Medium
**Implementation order:** 116
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-PRDSH-01, TASK-SIG-03
**Blocks:** None
**Primary role:** Frontend

#### Objective
Build processing dashboards (`/platform/processing`, `/applications/:id/processing`) with Active/Queued/Completed/Failed/All tabs, filters, progress, current stage, attempts, duration, correlation, retry, live updates, and empty states.

#### Requirements Covered
FR-DSH-002, FR-DSH-003, ASDD §21.3.

#### Detailed Description
Tabbed, filterable lists updating live via file events; job detail drawer with retry (authorized); empty/loading/error states.

#### Technical Approach
- Query + SignalR invalidation; retry mutation.

#### API or Event Contracts
Consumes PRDSH-01 + file events.

#### Data Changes
Not applicable.

#### Security and Authorization
Retry gated to admins/platform; scoping enforced server-side.

#### Error and Recovery Behavior
REST refresh on reconnect.

#### Acceptance Criteria
- Given a processing job, when its stage changes, then the row updates live; failed jobs offer authorized retry.

#### Test Requirements
- Component: tabs/filters/live/retry gating.

#### Definition of Done
- [ ] Processing dashboards with live updates + retry.

#### Implementation Notes
Shared table components with run dashboard.

#### Out of Scope
Run dashboard (module 29).

---

## MODULE 29 — Agent Run Dashboard (RDSH)

**Epics**

- EPIC-RDSH-A: Run views, details & retry/regenerate

### STORY-RDSH-01: Monitor and inspect agent runs

**As an:** application user
**I want:** Queued/Active/Completed/Failed run views with details (snapshot, evidence, deployment, params, stage timings, failures) and retry/regenerate
**So that:** I can inspect and recover runs (FR-RUN-004, ASDD §21.4)

#### Business Value
Traceability + operational control for executions.

#### Story Acceptance Criteria
- Filterable run lists with live updates; run details showing full snapshot/evidence/params/timings/failure; retry/regenerate actions.

#### Dependencies
STORY-RUN-01, STORY-SIG-01.

#### Tasks
`TASK-RDSH-01`, `TASK-RDSH-02`

---

### TASK-RDSH-01: Run query and detail endpoints
**Module:** 29 — Run Dashboard
**Epic:** EPIC-RDSH-A
**Story:** STORY-RDSH-01
**Priority:** High
**Implementation order:** 117
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-RUN-01, TASK-CIT-02
**Blocks:** Run UI
**Primary role:** Backend

#### Objective
Provide filtered run list + detail endpoints returning agent snapshot, user request, deployment, requested/effective params, retrieval settings, evidence used, stage timings, output, citations, and failure details.

#### Requirements Covered
FR-RUN-004, ASDD §21.4.

#### Detailed Description
App-scoped run list with status filters; detail assembling the full run snapshot + evidence + timings + failure. Authorized by app membership.

#### Technical Approach
- Queries using `agent_runs` indexes; detail joins run evidence/output/citations.

#### API or Event Contracts
- `GET /api/applications/{appId}/runs?status=`; `GET …/runs/{runId}`.

#### Data Changes
Read-only.

#### Security and Authorization
App boundary; retry/regenerate authorized.

#### Error and Recovery Behavior
Retry/regenerate reuse modules 19/23.

#### Acceptance Criteria
- Given a run, when viewed, then the full snapshot/evidence/params/timings/failure display; cross-app denied.

#### Test Requirements
- Integration: list/detail; Security: scoping.

#### Definition of Done
- [ ] Run list/detail endpoints.

#### Implementation Notes
Detail reuses citation/evidence data (module 22).

#### Out of Scope
UI (RDSH-02).

---

### TASK-RDSH-02: Run dashboard UI (lists, detail, timings, retry/regenerate)
**Module:** 29 — Run Dashboard
**Epic:** EPIC-RDSH-A
**Story:** STORY-RDSH-01
**Priority:** Medium
**Implementation order:** 118
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-RDSH-01, TASK-SIG-04
**Blocks:** None
**Primary role:** Frontend

#### Objective
Build run dashboards (`/applications/:id/runs`, `/runs/:runId`) with filterable lists, live updates, run detail (snapshot/evidence/params/stage timings/failure), and retry/regenerate.

#### Requirements Covered
FR-RUN-004, FR-RUN-007, ASDD §21.4.

#### Detailed Description
Filterable run list updating live via run events; detail view with stage timeline, evidence, deployment, requested vs effective params, failure details; retry/regenerate actions gated by role; empty/loading/error states.

#### Technical Approach
- Query + SignalR invalidation; detail timeline component.

#### API or Event Contracts
Consumes RDSH-01 + run events.

#### Data Changes
Not applicable.

#### Security and Authorization
Retry/regenerate gated; scoping server-enforced.

#### Error and Recovery Behavior
REST refresh on reconnect.

#### Acceptance Criteria
- Given a run, when stages change, then the list/detail update live; failed runs offer authorized retry/regenerate.

#### Test Requirements
- Component: lists/detail/live/retry gating.

#### Definition of Done
- [ ] Run dashboards with detail + live + retry/regenerate.

#### Implementation Notes
Shares table components with processing dashboard.

#### Out of Scope
Output editing (module 23).

---

# PHASE 11 — Cross-Cutting: Audit, Health, Errors, Observability

## MODULE 30 — Audit (AUD)

**Epics**

- EPIC-AUD-A: Audit model & writers
- EPIC-AUD-B: Audit access, search & sensitive-data exclusion

### STORY-AUD-01: Tamper-evident, safe audit trail

**As a:** platform operator
**I want:** important events recorded with timestamp/user/action/application/entity/outcome/correlation and no secrets
**So that:** activity is traceable and compliant (FR-17, ASDD §17-domain audit)

#### Business Value
Accountability and troubleshooting without leaking sensitive data.

#### Story Acceptance Criteria
- Audit model + writer used across auth/user/app/file/agent/run/output/config events; no source documents/secrets stored; access restricted; searchable/filterable.

#### Dependencies
STORY-IDN-01, module 14.

#### Tasks
`TASK-AUD-01`, `TASK-AUD-02`, `TASK-AUD-03`

---

### TASK-AUD-01: Audit data model and writer
**Module:** 30 — Audit
**Epic:** EPIC-AUD-A
**Story:** STORY-AUD-01
**Priority:** High
**Implementation order:** 119
**Estimated size:** S
**Can run in parallel:** No
**Dependencies:** TASK-PG-01
**Blocks:** All audit emission
**Primary role:** Backend / Database

#### Objective
Define `AuditEvent` (timestamp, user, action, application, entity, outcome, correlation ID) and a writer service used everywhere; exclude secrets/full documents.

#### Requirements Covered
FR-17, ASDD §9 (AuditEvent), §10 index.

#### Detailed Description
Append-only audit table + `IAuditWriter.WriteAsync(event)`; index `audit_events(application_id, occurred_at)`. Never stores source content, prompts, outputs, or secrets. Audit write failures never block the primary action.

#### Technical Approach
- Append-only table; writer injected across modules.

#### API or Event Contracts
Not applicable.

#### Data Changes
Table `audit.audit_events` + index.

#### Security and Authorization
No secrets/documents; app-scoped where applicable.

#### Error and Recovery Behavior
Non-blocking writes; log on failure.

#### Acceptance Criteria
- Given any auditable action, when it occurs, then a safe audit row is written with correlation and no secrets.

#### Test Requirements
- Unit/Integration: writer + no-secret assertions.

#### Definition of Done
- [ ] Audit model + writer + index.

#### Implementation Notes
Used by auth (IDN-04) and all mutating modules.

#### Out of Scope
UI/search (AUD-03).

---

### TASK-AUD-02: Emit audit events across modules
**Module:** 30 — Audit
**Epic:** EPIC-AUD-A
**Story:** STORY-AUD-01
**Priority:** Medium
**Implementation order:** 120
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-AUD-01
**Blocks:** None
**Primary role:** Backend

#### Objective
Emit audit events for the FR-17 list: auth, user/role, application lifecycle, file (upload/replace/disable/delete/retry), agent (create/modify/enable/delete), run execution, output edit/delete, and AOAI deployment-config changes.

#### Requirements Covered
FR-17, ASDD §17 audit list.

#### Detailed Description
Hook the audit writer into each mutating command with a safe, structured payload (entity IDs, outcome, correlation). Coordinated so each action emits exactly one canonical event.

#### Technical Approach
- Central audit calls in application-layer command handlers.

#### API or Event Contracts
Not applicable.

#### Data Changes
Inserts audit rows.

#### Security and Authorization
No secrets/documents.

#### Error and Recovery Behavior
Non-blocking.

#### Acceptance Criteria
- Given each listed action, when performed, then a corresponding safe audit event is recorded.

#### Test Requirements
- Integration: representative actions produce audit rows.

#### Definition of Done
- [ ] Audit emission across all listed events.

#### Implementation Notes
Avoid duplicate events per action.

#### Out of Scope
Search UI (AUD-03).

---

### TASK-AUD-03: Audit search UI and access control
**Module:** 30 — Audit
**Epic:** EPIC-AUD-B
**Story:** STORY-AUD-01
**Priority:** Medium
**Implementation order:** 121
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-AUD-01, TASK-AUTHZ-01
**Blocks:** None
**Primary role:** Full Stack

#### Objective
Provide an authorized audit search/filter endpoint + UI (`/platform/audit`, `/applications/:id/activity`) with date/user/action/entity filters.

#### Requirements Covered
FR-17, ASDD §7.2, §19.3 (`CanViewAudit`).

#### Detailed Description
Search endpoint with filters + pagination; platform audit for platform roles, application activity for app users; UI list with filters and empty/loading states.

#### Technical Approach
- Filtered query on `audit_events`; app-scoped vs platform scoping.

#### API or Event Contracts
- `GET /api/platform/audit` and `GET /api/applications/{appId}/activity` (filtered/paged).

#### Data Changes
Read-only.

#### Security and Authorization
`CanViewAudit`; app boundary; no secrets exposed.

#### Error and Recovery Behavior
Standard states.

#### Acceptance Criteria
- Given `CanViewAudit`, when filtering, then results narrow; app users only see their app's activity; no secrets shown.

#### Test Requirements
- Integration: filtering + scoping; Security: unauthorized denied.

#### Definition of Done
- [ ] Audit search endpoints + UI with access control.

#### Implementation Notes
Reuse list/filter patterns.

#### Out of Scope
Audit export.

---

## MODULE 31 — Health & Operations (OPS)

**Epics**

- EPIC-OPS-A: Liveness/readiness & dependency health
- EPIC-OPS-B: Worker heartbeats, stale detection & operations page

### STORY-OPS-01: Comprehensive health and operations visibility

**As a:** platform operator
**I want:** liveness/readiness endpoints, dependency health (PG/pgvector/S3/SQS/AOAI), worker heartbeats, and stale/DLQ visibility
**So that:** I can detect and diagnose problems (FR-18, ASDD §24)

#### Business Value
Operational reliability and fast diagnosis.

#### Story Acceptance Criteria
- Liveness + readiness endpoints; dependency health; worker heartbeat + stale detection; DLQ visibility; operations page; deep checks user-initiated/sparing.

#### Dependencies
STORY-S3-01, STORY-SQS-01, STORY-AOI-01, STORY-JOB-01.

#### Tasks
`TASK-OPS-01`, `TASK-OPS-02`, `TASK-OPS-03`

---

### TASK-OPS-01: Liveness/readiness and dependency health checks
**Module:** 31 — Health & Ops
**Epic:** EPIC-OPS-A
**Story:** STORY-OPS-01
**Priority:** High
**Implementation order:** 122
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-S3-04, TASK-SQS-01, TASK-AOI-05, TASK-PG-01
**Blocks:** Compose healthchecks, ops page
**Primary role:** Backend

#### Objective
Implement liveness + readiness endpoints checking Postgres, schema, pgvector extension, S3, SQS, and required worker heartbeats; expose external AOAI dependency health separately (sparing).

#### Requirements Covered
FR-18, ASDD §24.

#### Detailed Description
`/health/live` (process responsive) and `/health/ready` (PG + schema + pgvector + S3 + SQS + worker heartbeat current). External AOAI checks are user-initiated/scheduled sparingly (AOI-05) to avoid quota use.

#### Technical Approach
- ASP.NET Core health checks; readiness aggregates dependency checks.

#### API or Event Contracts
- `GET /health/live`, `GET /health/ready`, `GET /api/operations/aoai-health`.

#### Data Changes
Not applicable.

#### Security and Authorization
Deep/AOAI checks restricted to platform roles.

#### Error and Recovery Behavior
Readiness fails if a required dependency is down.

#### Acceptance Criteria
- Given all dependencies up, when checking readiness, then healthy; given pgvector missing, then not ready.

#### Test Requirements
- Integration: readiness up/down per dependency.

#### Definition of Done
- [ ] Liveness + readiness + dependency health.

#### Implementation Notes
Compose healthchecks (FND-05) use `/health/ready`.

#### Out of Scope
Ops UI (OPS-03).

---

### TASK-OPS-02: Worker heartbeats and stale-worker detection
**Module:** 31 — Health & Ops
**Epic:** EPIC-OPS-B
**Story:** STORY-OPS-01
**Priority:** High
**Implementation order:** 123
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-JOB-06
**Blocks:** Ops page, dashboards
**Primary role:** Backend

#### Objective
Have both workers emit periodic heartbeats and detect stale workers for health/readiness and dashboards.

#### Requirements Covered
FR-18, ASDD §24 (worker heartbeat current), §9 (WorkerHeartbeat).

#### Detailed Description
Workers update `WorkerHeartbeat` periodically; a helper reports stale workers (heartbeat older than threshold) surfaced to readiness + operations + platform dashboard.

#### Technical Approach
- Heartbeat timer in each worker; staleness query.

#### API or Event Contracts
Consumed by health/dashboards.

#### Data Changes
Updates `operations.worker_heartbeats`.

#### Security and Authorization
Ops data restricted to platform roles.

#### Error and Recovery Behavior
Stale worker flagged; readiness may reflect missing worker.

#### Acceptance Criteria
- Given a worker stops heartbeating, when checked, then it is flagged stale and readiness/ops reflect it.

#### Test Requirements
- Integration: heartbeat freshness + stale detection.

#### Definition of Done
- [ ] Worker heartbeats + stale detection.

#### Implementation Notes
Reuses heartbeat table from JOB-06.

#### Out of Scope
UI (OPS-03).

---

### TASK-OPS-03: Operations page (health, DLQ, workers, AOAI)
**Module:** 31 — Health & Ops
**Epic:** EPIC-OPS-B
**Story:** STORY-OPS-01
**Priority:** Medium
**Implementation order:** 124
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-OPS-01, TASK-OPS-02, TASK-SQS-04
**Blocks:** None
**Primary role:** Full Stack

#### Objective
Build `/platform/operations` showing component health (API, PG, pgvector, S3, SQS, AOAI embed/gen, workers, SignalR), DLQ counts, stale workers, and user-initiated deep checks.

#### Requirements Covered
FR-18, ASDD §24.

#### Detailed Description
Operations endpoint aggregating health/DLQ/worker/AOAI status + a page rendering it with a manual "run deep check" for AOAI (sparing). Restricted to platform roles.

#### Technical Approach
- Aggregation endpoint + UI; manual AOAI check button.

#### API or Event Contracts
- `GET /api/operations/health` (platform roles).

#### Data Changes
Read-only.

#### Security and Authorization
Platform roles only.

#### Error and Recovery Behavior
Graceful per-component degradation.

#### Acceptance Criteria
- Given a platform role, when opening operations, then all component statuses + DLQ + stale workers render; deep AOAI check is manual.

#### Test Requirements
- Integration: aggregation; Component: rendering + role guard.

#### Definition of Done
- [ ] Operations page with health/DLQ/workers/AOAI.

#### Implementation Notes
Deep checks sparing to avoid quota use.

#### Out of Scope
Metrics dashboards (module 33).

---

## MODULE 32 — Error Handling (ERR)

**Epics**

- EPIC-ERR-A: Problem Details contract & category mapping

### STORY-ERR-01: Consistent, safe error contract

**As a:** developer and user
**I want:** a Problem Details contract with categorized, safe messages and correlation IDs
**So that:** errors are diagnosable without leaking internals (ASDD §22)

#### Business Value
Predictable API errors and safe user messaging.

#### Story Acceptance Criteria
- All API errors use Problem Details with title/safe explanation/retry availability/recommended action/correlation; raw AOAI/stack traces stay in restricted logs.

#### Dependencies
STORY-FND-01.

#### Tasks
`TASK-ERR-01`, `TASK-ERR-02`

---

### TASK-ERR-01: Problem Details middleware and error categories
**Module:** 32 — Error Handling
**Epic:** EPIC-ERR-A
**Story:** STORY-ERR-01
**Priority:** High
**Implementation order:** 26b
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-01
**Blocks:** All endpoints, frontend error handling
**Primary role:** Backend

#### Objective
Implement centralized Problem Details error handling mapping domain error categories (ASDD §22) to safe HTTP responses with correlation IDs.

#### Requirements Covered
ASDD §22 (error categories), FR-SEC (safe logging).

#### Detailed Description
Middleware/exception handler mapping categories (Validation, Authentication, Authorization, Conflict, Configuration, DependencyUnavailable, Throttled, Timeout, ContextOverflow, RetrievalEmpty, EmbeddingFailure, GenerationFailure, StorageFailure, QueueFailure, OutputValidationFailure, InternalFailure) to status codes + Problem Details (title, safe detail, retry availability, recommended action, correlation). Raw AOAI responses/stack traces logged at restricted level only.

#### Technical Approach
- `IExceptionHandler`/middleware; typed domain exceptions → categories.

#### API or Event Contracts
Problem Details envelope on all error responses.

#### Data Changes
Not applicable.

#### Security and Authorization
No secrets/stack traces in responses.

#### Error and Recovery Behavior
Correlation ID on every error; retry availability indicated.

#### Acceptance Criteria
- Given any handled error, when returned, then it is Problem Details with a safe message + correlation and no internals.
- Given a throttling error, then retry availability is indicated.

#### Test Requirements
- Unit: category→status mapping; Integration: safe payloads + correlation.

#### Definition of Done
- [ ] Problem Details middleware + category mapping.

#### Implementation Notes
Used consistently across modules; frontend parses this envelope (FND-02).

#### Out of Scope
Frontend rendering (ERR-02).

---

### TASK-ERR-02: Frontend error surfacing
**Module:** 32 — Error Handling
**Epic:** EPIC-ERR-A
**Story:** STORY-ERR-01
**Priority:** Medium
**Implementation order:** 125
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-ERR-01, TASK-FND-02
**Blocks:** None
**Primary role:** Frontend

#### Objective
Surface Problem Details errors consistently (safe title/message, retry affordance, correlation ID) with a global error boundary and per-request handling.

#### Requirements Covered
ASDD §22, FR-DSH-004 (safe messaging).

#### Detailed Description
Parse Problem Details in the API wrapper; show toast/inline errors with title, safe detail, correlation, and a retry button where applicable; global error boundary for uncaught errors.

#### Technical Approach
- Central error parser + toast/inline components; error boundary.

#### API or Event Contracts
Consumes Problem Details.

#### Data Changes
Not applicable.

#### Security and Authorization
Only safe fields shown.

#### Error and Recovery Behavior
Retryable errors offer retry; correlation displayed for support.

#### Acceptance Criteria
- Given a Problem Details error, when received, then a safe message + correlation display and retry appears when eligible.

#### Test Requirements
- Component: error rendering + retry affordance.

#### Definition of Done
- [ ] Consistent frontend error surfacing + boundary.

#### Implementation Notes
Reused across all features.

#### Out of Scope
Backend mapping (ERR-01).

---

## MODULE 33 — Logging & Observability (OBS)

**Epics**

- EPIC-OBS-A: Structured logging & correlation
- EPIC-OBS-B: Metrics & tracing

### STORY-OBS-01: Structured, correlated observability without sensitive data

**As a:** developer/operator
**I want:** structured logs, correlation propagation, metrics, and trace spans excluding sensitive content
**So that:** I can diagnose performance/failures safely (ASDD §23, §20.5)

#### Business Value
Diagnosability and performance insight without leaking data.

#### Story Acceptance Criteria
- Structured logging with correlation across browser→API→job→SQS→worker→AOAI→output→SignalR; key metrics + trace spans; no source docs/prompts/outputs/secrets/embeddings logged by default.

#### Dependencies
STORY-FND-01, cross-cutting across modules.

#### Tasks
`TASK-OBS-01`, `TASK-OBS-02`, `TASK-OBS-03`

---

### TASK-OBS-01: Structured logging and correlation propagation
**Module:** 33 — Observability
**Epic:** EPIC-OBS-A
**Story:** STORY-OBS-01
**Priority:** High
**Implementation order:** 126
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-01, TASK-FND-03
**Blocks:** Metrics/tracing
**Primary role:** Backend

#### Objective
Configure structured logging with a correlation ID propagated across the full chain (browser→API→DB job→SQS→worker→AOAI→output→SignalR), excluding sensitive content by default.

#### Requirements Covered
ASDD §23.1, §20.5.

#### Detailed Description
Adopt structured logging; generate/propagate a correlation ID from the browser through API, job records, SQS messages, workers, AOAI calls, outputs, and SignalR events. Never log source documents, prompts, user requests, outputs, credentials, or embeddings by default.

#### Technical Approach
- Serilog-style structured logs + `LogContext` correlation enrichment; correlation from `Shared` (FND-03).

#### API or Event Contracts
Correlation header (e.g. `X-Correlation-ID`).

#### Data Changes
Not applicable.

#### Security and Authorization
Sensitive-content exclusion enforced.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given a request flowing through the system, when logged, then the same correlation ID appears at each hop and no sensitive content is present.

#### Test Requirements
- Integration: correlation propagation; Security: no sensitive fields logged.

#### Definition of Done
- [ ] Structured logging + correlation + sensitive-data exclusion.

#### Implementation Notes
Correlation underpins diagnosis and audit linkage.

#### Out of Scope
Metrics/tracing (OBS-02/03).

---

### TASK-OBS-02: Metrics collection
**Module:** 33 — Observability
**Epic:** EPIC-OBS-B
**Story:** STORY-OBS-01
**Priority:** Medium
**Implementation order:** 127
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-OBS-01
**Blocks:** None
**Primary role:** Backend

#### Objective
Emit the ASDD §23.2 metrics: API latency/error rate, queue depth/oldest-age, job duration/failure, AOAI latency/throttling, embedding batch count, chunks per file, retrieval latency, empty-retrieval rate, evidence count/token usage, run duration/failure, context utilization, invalid-citation count, worker heartbeat age.

#### Requirements Covered
ASDD §23.2.

#### Detailed Description
Instrument counters/histograms via `System.Diagnostics.Metrics`/OpenTelemetry; expose for local scraping/inspection. No sensitive content in metric labels.

#### Technical Approach
- Meters per subsystem; OTEL exporter (console/OTLP) for local.

#### API or Event Contracts
Metrics endpoint/exporter.

#### Data Changes
Not applicable.

#### Security and Authorization
No sensitive labels.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given system activity, when observed, then the listed metrics are emitted with safe labels.

#### Test Requirements
- Unit/Integration: representative metrics emitted.

#### Definition of Done
- [ ] ASDD §23.2 metrics instrumented.

#### Implementation Notes
Feeds ops/performance baseline (module 36).

#### Out of Scope
Tracing (OBS-03).

---

### TASK-OBS-03: Distributed tracing spans
**Module:** 33 — Observability
**Epic:** EPIC-OBS-B
**Story:** STORY-OBS-01
**Priority:** Low
**Implementation order:** 128
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-OBS-01
**Blocks:** None
**Primary role:** Backend

#### Objective
Add trace spans for the agent-run pipeline (ASDD §23.3) and processing pipeline for latency attribution.

#### Requirements Covered
ASDD §23.3.

#### Detailed Description
Create spans: Authorize, Load agent snapshot, Calculate budget, Create query embedding, Semantic search, Keyword search, Rank fusion, Evidence assembly, AOAI generation, Markdown validation, Citation validation, Persist output; and processing-stage spans. Correlate with logs/metrics.

#### Technical Approach
- OpenTelemetry `ActivitySource` spans across the pipelines.

#### API or Event Contracts
Traces via OTEL exporter.

#### Data Changes
Not applicable.

#### Security and Authorization
No sensitive content in span attributes.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given a run, when traced, then the ASDD §23.3 spans appear with timings and correlation.

#### Test Requirements
- Integration: spans emitted for a run.

#### Definition of Done
- [ ] Run + processing trace spans.

#### Implementation Notes
Complements stage timings (module 19).

#### Out of Scope
APM backend setup.

---

# PHASE 12 — Quality, Deployment & Release Readiness

## MODULE 34 — Testing Strategy (TST)

**Epics**

- EPIC-TST-A: Test infrastructure & fakes
- EPIC-TST-B: Unit, integration, architecture & security tests
- EPIC-TST-C: End-to-end & resilience tests

### STORY-TST-01: Comprehensive, deterministic test suite

**As a:** developer
**I want:** unit/integration/architecture/security/E2E tests with a deterministic fake Azure OpenAI and optional real smoke tests
**So that:** behavior is verified without unpredictable model cost (ASDD §25)

#### Business Value
Confidence, regression safety, and reproducibility without unbounded AOAI usage.

#### Story Acceptance Criteria
- Test doubles for AOAI; unit/integration/architecture/security/E2E coverage of critical flows; deterministic by default; real AOAI smoke tests opt-in.

#### Dependencies
STORY-FND-01, and the modules each test targets.

#### Tasks
`TASK-TST-01`, `TASK-TST-02`, `TASK-TST-03`, `TASK-TST-04`

---

### TASK-TST-01: Fake Azure OpenAI test service and test harness
**Module:** 34 — Testing
**Epic:** EPIC-TST-A
**Story:** STORY-TST-01
**Priority:** High
**Implementation order:** 27b
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-AOI-01, TASK-FND-01
**Blocks:** Deterministic integration/E2E tests
**Primary role:** Backend / QA

#### Objective
Provide deterministic fakes for `IEmbeddingService`, `ITextGenerationService`, and `ITokenEstimator` plus a shared test harness (Testcontainers or Compose-backed Postgres/pgvector + LocalStack) for integration tests.

#### Requirements Covered
ASDD §25.1, §25.6 (fake AOAI; deterministic embeddings).

#### Detailed Description
Fake embedding service returns deterministic vectors (e.g. hashed) at the configured dimension; fake generation returns controllable outputs including citation markers, malformed markdown, throttling, and timeout scenarios; token estimator is deterministic. Harness spins up Postgres+pgvector and LocalStack (S3+SQS) for integration tests. Tests never call real AOAI by default.

#### Technical Approach
- DI swaps AOAI interfaces with fakes in the test host; Testcontainers/Compose for infra.

#### API or Event Contracts
Not applicable.

#### Data Changes
Ephemeral test schemas.

#### Security and Authorization
Test-only; no real credentials.

#### Error and Recovery Behavior
Fakes can simulate throttling/timeout/failure categories.

#### Acceptance Criteria
- Given integration tests, when run, then they use fakes + ephemeral infra deterministically and never hit real AOAI.

#### Test Requirements
- Meta: harness self-tests (fake determinism).

#### Definition of Done
- [ ] Fake AOAI services + integration harness.

#### Implementation Notes
Enables retrieval/generation/citation tests without cost.

#### Out of Scope
Real AOAI smoke (TST-04).

---

### TASK-TST-02: Unit, integration and architecture tests
**Module:** 34 — Testing
**Epic:** EPIC-TST-B
**Story:** STORY-TST-01
**Priority:** High
**Implementation order:** 129
**Estimated size:** L
**Can run in parallel:** Yes
**Dependencies:** TASK-TST-01
**Blocks:** Release readiness
**Primary role:** Backend / QA

#### Objective
Implement unit tests (chunking, token budget, rank fusion, citation parsing/validation, markdown validation, param resolution), integration tests (auth, authorization, applications, upload→processing→embedding, retrieval, run→output, replacement/deletion), and architecture tests (module boundaries, no cross-module internal access).

#### Requirements Covered
ASDD §25.2–§25.4, §4.4 (architecture rules), and cross-module FRs.

#### Detailed Description
Cover critical algorithms with unit tests; end-to-end backend flows with integration tests on the harness; enforce modular-monolith boundaries with architecture tests (e.g. NetArchTest) preventing cross-module coupling and enforcing dependency direction.

#### Technical Approach
- xUnit + FluentAssertions; NetArchTest for boundaries; harness from TST-01.

#### API or Event Contracts
Not applicable.

#### Data Changes
Ephemeral.

#### Security and Authorization
Includes authorization test coverage.

#### Error and Recovery Behavior
Tests assert error categories/Problem Details.

#### Acceptance Criteria
- Given the suite, when run, then critical algorithms + flows + boundary rules pass deterministically.

#### Test Requirements
- This task is the tests.

#### Definition of Done
- [ ] Unit + integration + architecture tests passing in CI.

#### Implementation Notes
Architecture test referenced by TASK-FND-01/TST context.

#### Out of Scope
Security/E2E specifics (TST-03).

---

### TASK-TST-03: Security, isolation and resilience tests
**Module:** 34 — Testing
**Epic:** EPIC-TST-C
**Story:** STORY-TST-01
**Priority:** High
**Implementation order:** 130
**Estimated size:** L
**Can run in parallel:** Yes
**Dependencies:** TASK-TST-01, TASK-TST-02
**Blocks:** Release readiness
**Primary role:** QA / Security

#### Objective
Implement security tests (role-permission matrix, cross-application isolation, disabled-user/role-change invalidation, malicious-markdown sanitization, unauthorized access, CSRF), and resilience tests (retry/backoff, duplicate SQS delivery idempotency, DLQ routing, SignalR reconnect + REST authority, cancellation).

#### Requirements Covered
FR-SEC-001..005, FR-CIT/OUT (markdown safety), FR-PRC (retry/DLQ), ASDD §25.5, §20, §18.

#### Detailed Description
Automate the ASDD role-permission matrix; verify no cross-app data access; verify security-stamp invalidation disables sessions; feed malicious markdown/script payloads and assert sanitization; verify duplicate message processing is idempotent and poison messages route to DLQ; verify SignalR reconnect falls back to REST authority; verify pre-generation cancellation.

#### Technical Approach
- Parameterized authorization tests; injected duplicate/failing messages; DOM sanitization assertions (frontend + backend policy).

#### API or Event Contracts
Not applicable.

#### Data Changes
Ephemeral.

#### Security and Authorization
This task validates security controls.

#### Error and Recovery Behavior
Validates recovery/idempotency behavior.

#### Acceptance Criteria
- Given security/resilience tests, when run, then isolation, invalidation, sanitization, idempotency, DLQ, reconnect, and cancellation all pass.

#### Test Requirements
- This task is the tests.

#### Definition of Done
- [ ] Security + isolation + resilience tests passing.

#### Implementation Notes
Cross-app isolation is a core requirement (FR-SEC-002).

#### Out of Scope
Real AOAI smoke (TST-04).

---

### TASK-TST-04: End-to-end and real-AOAI smoke tests
**Module:** 34 — Testing
**Epic:** EPIC-TST-C
**Story:** STORY-TST-01
**Priority:** Medium
**Implementation order:** 131
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-TST-01, TASK-GEN-01, TASK-OUT-01
**Blocks:** Release readiness
**Primary role:** QA

#### Objective
Implement E2E tests (browser-level: login → create app → upload → process → create agent → run → view output → edit/export) against fakes, plus an opt-in smoke test suite exercising real Azure OpenAI embeddings + generation.

#### Requirements Covered
ASDD §25.7, all user journeys (ASDD §16).

#### Detailed Description
Playwright E2E covering the primary journeys deterministically with fakes; a separate, explicitly-enabled smoke suite that runs a minimal real embedding + generation call to validate live AOAI configuration without large quota use.

#### Technical Approach
- Playwright for E2E; env-flag-gated real-AOAI smoke tests.

#### API or Event Contracts
Not applicable.

#### Data Changes
Ephemeral.

#### Security and Authorization
Smoke tests use real credentials only when explicitly enabled.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given E2E tests, when run, then core journeys pass with fakes; given the smoke flag, then a real AOAI embed+generate succeeds.

#### Test Requirements
- This task is the tests.

#### Definition of Done
- [ ] E2E suite + opt-in real-AOAI smoke tests.

#### Implementation Notes
Real smoke tests validate the actual AOAI config safely.

#### Out of Scope
Load/perf testing (module 36 baseline only).

---

## MODULE 35 — Local Deployment & Developer Experience (DEP)

**Epics**

- EPIC-DEP-A: One-command startup, seeding & scripts
- EPIC-DEP-B: Developer runbook & troubleshooting

### STORY-DEP-01: One-command local environment

**As a:** developer
**I want:** to start the entire stack with one command, seeded and ready, with helper scripts and a runbook
**So that:** onboarding and daily development are frictionless (ASDD §12, §26)

#### Business Value
Fast, reliable local onboarding and reproducible environments.

#### Story Acceptance Criteria
- `docker compose up` brings up all services healthy, migrations applied, LocalStack initialized, Super Admin seeded; helper scripts + runbook documented.

#### Dependencies
STORY-FND-01 (compose/init/migrations/seed), all workers.

#### Tasks
`TASK-DEP-01`, `TASK-DEP-02`

---

### TASK-DEP-01: One-command startup orchestration and helper scripts
**Module:** 35 — Deployment
**Epic:** EPIC-DEP-A
**Story:** STORY-DEP-01
**Priority:** High
**Implementation order:** 132
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-05, TASK-FND-08, TASK-FND-09, TASK-FND-10, TASK-OPS-01
**Blocks:** Release verification
**Primary role:** DevOps

#### Objective
Ensure ordered, healthy startup (Postgres → LocalStack init → migrations/seed → API → workers → frontend) via compose `depends_on` + healthchecks, plus helper scripts (reset DB/volumes, reseed, tail logs, run tests).

#### Requirements Covered
ASDD §12 (deployment topology), §26 (developer experience).

#### Detailed Description
Compose ordering keyed on `/health/ready`; a first-run migration/seed step; scripts to reset (including explicit named-volume removal), reseed, and view logs. Only frontend + API expose host ports by default.

#### Technical Approach
- `depends_on: condition: service_healthy`; PowerShell/Bash helper scripts.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
No secrets committed; `.env` from template.

#### Error and Recovery Behavior
Startup fails fast if a dependency is unhealthy.

#### Acceptance Criteria
- Given a clean checkout with `.env`, when `docker compose up`, then all services become healthy, migrations apply, LocalStack initializes, and Super Admin is seeded.

#### Test Requirements
- Manual/scripted: fresh-clone bring-up validation.

#### Definition of Done
- [ ] One-command startup + helper scripts.

#### Implementation Notes
Reset script must `docker volume rm` named volumes explicitly (compose `rm -v` does not remove named volumes).

#### Out of Scope
Runbook prose (DEP-02).

---

### TASK-DEP-02: Developer runbook and troubleshooting guide
**Module:** 35 — Deployment
**Epic:** EPIC-DEP-B
**Story:** STORY-DEP-01
**Priority:** Medium
**Implementation order:** 133
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-DEP-01
**Blocks:** Release readiness
**Primary role:** DevOps / Docs

#### Objective
Write a consolidated runbook: prerequisites, first-run steps, configuring Azure OpenAI (Entra ID + API-key fallback), seeded credentials, common tasks, and troubleshooting (AOAI 401/403/429, pgvector missing, LocalStack re-init, DLQ inspection, stale workers).

#### Requirements Covered
ASDD §26, §24, §11 (config).

#### Detailed Description
A single README/runbook section covering setup, configuration, day-to-day workflows, and a troubleshooting table mapping symptoms → cause → fix, including the AOAI availability gotchas and volume reset guidance.

#### Technical Approach
- Markdown documentation in the repo.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Documents credential handling without embedding secrets.

#### Error and Recovery Behavior
Troubleshooting guidance for common failures.

#### Acceptance Criteria
- Given the runbook, when a developer follows it, then they can configure AOAI, start the stack, and resolve common issues.

#### Test Requirements
- Doc review against a fresh-onboarding walkthrough.

#### Definition of Done
- [ ] Consolidated runbook + troubleshooting guide.

#### Implementation Notes
Note the GitHub Models retirement/410 gotcha if that provider is ever referenced; this platform uses Azure OpenAI only.

#### Out of Scope
Release checklist (module 36).

---

## MODULE 36 — Final Integration & Release Readiness (REL)

**Epics**

- EPIC-REL-A: End-to-end verification & acceptance mapping
- EPIC-REL-B: Performance baseline, limitations & release checklist

### STORY-REL-01: Verified, documented release readiness

**As a:** delivery lead
**I want:** full-workflow verification, acceptance-criteria mapping, security/failure verification, a performance baseline, and a release checklist
**So that:** the platform is demonstrably complete and operable (ASDD §16, §25, §27)

#### Business Value
Objective, auditable confirmation the platform meets its requirements.

#### Story Acceptance Criteria
- All user journeys verified end-to-end; every acceptance criterion mapped; security + failure-recovery verified; performance baseline captured; known limitations + release checklist documented.

#### Dependencies
All prior modules.

#### Tasks
`TASK-REL-01`, `TASK-REL-02`

---

### TASK-REL-01: Full-workflow verification and acceptance mapping
**Module:** 36 — Release
**Epic:** EPIC-REL-A
**Story:** STORY-REL-01
**Priority:** High
**Implementation order:** 134
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-TST-04, TASK-DEP-02
**Blocks:** Release
**Primary role:** QA / Delivery

#### Objective
Execute and document verification of every primary user journey (ASDD §16) end-to-end, map each FRD acceptance criterion to passing evidence, and verify security controls + failure-recovery scenarios.

#### Requirements Covered
ASDD §16 (journeys), §25 (tests), FRD acceptance criteria (all sections).

#### Detailed Description
Run the full journey set (platform admin setup, app provisioning, knowledge upload→ready, agent authoring/testing, run→output→edit/export, replacement/deletion, dashboards, audit, operations) and produce a mapping table from each acceptance criterion → verifying test/evidence → result. Verify security (isolation, invalidation, sanitization) and failure recovery (retry, DLQ, reconnect).

#### Technical Approach
- Structured verification checklist tied to test IDs.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Confirms security controls operate as designed.

#### Error and Recovery Behavior
Confirms recovery behaviors.

#### Acceptance Criteria
- Given the verification pass, when complete, then every journey + acceptance criterion has a mapped, passing result or a documented deferral.

#### Test Requirements
- Uses the full test suite (module 34).

#### Definition of Done
- [ ] Full-workflow verification + acceptance mapping documented.

#### Implementation Notes
Feeds the traceability matrix in this document.

#### Out of Scope
Performance/limitations (REL-02).

---

### TASK-REL-02: Performance baseline, known limitations and release checklist
**Module:** 36 — Release
**Epic:** EPIC-REL-B
**Story:** STORY-REL-01
**Priority:** Medium
**Implementation order:** 135
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-REL-01, TASK-OBS-02
**Blocks:** None
**Primary role:** QA / Delivery

#### Objective
Capture a local performance baseline (ingestion, retrieval, run latency using the §23.2 metrics), document known limitations/deferred scope, and produce deployment + release checklists.

#### Requirements Covered
ASDD §27 (limitations), §23 (metrics), §12 (deployment).

#### Detailed Description
Record representative timings for processing a document, retrieval, and an agent run using the observability metrics; enumerate deferred scope (HNSW, streaming, cache, versioning, mid-generation cancellation) and known limitations; provide a pre-release checklist (migrations applied, seed present, health green, tests green, secrets configured, DLQs empty).

#### Technical Approach
- Use metrics (OBS-02) to record baseline; document checklists in the runbook.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Checklist includes secret configuration verification.

#### Error and Recovery Behavior
Checklist includes DLQ-empty verification.

#### Acceptance Criteria
- Given release prep, when complete, then a baseline + limitations + release checklist exist and pass.

#### Test Requirements
- Doc review + baseline capture.

#### Definition of Done
- [ ] Performance baseline + limitations + release checklist.

#### Implementation Notes
Deferred scope aligns with decisions D1–D12.

#### Out of Scope
Production-grade load testing.

---

# PHASE 1 (CROSS-CUTTING) — Frontend Design System & Experience

> **Sequencing note:** Module 37 is a foundational frontend workstream that belongs in **Phase P1** and must be built immediately after the SPA scaffold (`TASK-FND-02`), before the feature UIs. It is documented here, after Module 36, only to preserve the original 1–36 module numbering. Feature modules (knowledge, agents, dashboards, processing, outputs, runs) **consume** the shared tokens, shell, and components defined here rather than reimplementing them.

## MODULE 37 — Frontend Design System, Application Shell & Interaction (UX)

**Design direction:** A dark-first "AI Operations Workspace" — calm, information-dense, fast, trustworthy, evidence-first; Linear-style precision + Vercel-style simplicity with Azure-Portal-level density but cleaner; one restrained accent; purposeful motion only. Avoid heavy glassmorphism, glowing borders, animated backgrounds behind data, and per-element animation (decisions D13–D19).

**Epics**

- EPIC-UX-A: Design tokens, theming & component foundation
- EPIC-UX-B: Application shell, navigation & command palette
- EPIC-UX-C: Shared interaction surfaces (tables, charts, timelines, editors, evidence workspace)

### STORY-UX-01: Establish the design system, theme, and application shell

**As a:** frontend engineer and platform user
**I want:** a dark-first design system (semantic tokens, shadcn/Radix components, purposeful Motion), a consistent application shell, and a permission-aware command palette
**So that:** every screen is consistent, accessible, keyboard-friendly, fast, and reads as a serious enterprise AI system (ASDD §7; decisions D13–D19)

#### Business Value
A single, original, accessible visual identity that accelerates every feature module and avoids inconsistent, duplicated UI.

#### Story Acceptance Criteria
- Semantic tokens + dark-first theme; shadcn/ui on Radix with no second UI framework; Motion honoring reduced-motion; collapsible shell with app switcher, breadcrumbs, theme switch, health indicator; Ctrl/Cmd+K command palette respecting roles.

#### Dependencies
STORY-FND-01 (`TASK-FND-02`), STORY-AUTHZ-01 (capabilities).

#### Tasks
`TASK-UX-01`, `TASK-UX-02`, `TASK-UX-03`, `TASK-UX-04`, `TASK-UX-05`

---

### TASK-UX-01: Semantic design tokens and dark-first Tailwind theme
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-A
**Story:** STORY-UX-01
**Priority:** High
**Implementation order:** 2a
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-FND-02
**Blocks:** All Module 37 tasks and all feature UI
**Primary role:** Frontend

#### Objective
Define the semantic design-token set and a dark-first (theme-switchable) Tailwind theme that all components consume.

#### Requirements Covered
ASDD §7 (frontend design), FR-DSH-003 (consistent live UI), decisions D14.

#### Detailed Description
Establish CSS-variable tokens: `--background`, `--surface`, `--surface-raised`, `--surface-hover`, `--border`, `--border-strong`, `--text-primary`, `--text-secondary`, `--text-muted`, `--accent`, `--success`, `--warning`, `--danger`, `--processing`. Map them into Tailwind theme extensions so utilities and shadcn components read tokens (never arbitrary hex). Dark-first palette (deep neutral graphite/blue-black background, one restrained indigo accent; processing=cyan, success=emerald, warning=amber, failure=rose). Define radius scale (8–12 px controls, 12–16 px cards, 16–20 px drawers/modals), typography (Inter/Geist interface + JetBrains Mono for prompt/Markdown source), and a subtle elevation/shadow scale reserved for overlays. Provide a `ThemeProvider` with light/dark and `prefers-color-scheme` default.

#### Technical Approach
- CSS variables in a root stylesheet; `tailwind.config` extends `colors`/`borderRadius`/`fontFamily` from the variables; theme toggling swaps a `data-theme`/class on `<html>`.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Not applicable.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given any component, when it renders, then colors/spacing/radius/typography derive from tokens (no hard-coded hex in components).
- Given a theme switch, when toggled, then dark and light render correctly and persist per user preference.

#### Test Requirements
- Component: token-driven rendering + theme toggle snapshot.

#### Definition of Done
- [ ] Token set + dark-first Tailwind theme + ThemeProvider.

#### Implementation Notes
Tokens make future rebranding and full light-mode trivial; status colors never rely on color alone (icon + text required, see `TASK-UX-09`).

#### Out of Scope
Component library wiring (UX-02); animation (UX-03).

---

### TASK-UX-02: shadcn/ui + Radix component foundation and package baseline
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-A
**Story:** STORY-UX-01
**Priority:** High
**Implementation order:** 2b
**Estimated size:** M
**Can run in parallel:** No
**Dependencies:** TASK-UX-01
**Blocks:** All feature UI
**Primary role:** Frontend

#### Objective
Install and configure shadcn/ui (on Radix primitives) as the single component foundation and lock the approved free-package baseline.

#### Requirements Covered
ASDD §7, decisions D13, D16, D18.

#### Detailed Description
Initialize shadcn/ui wired to the tokens from UX-01 and generate the base primitives used across the app (button, input, select, textarea, dialog, sheet/drawer, dropdown-menu, tabs, tooltip, popover, collapsible, breadcrumb, badge, progress, skeleton, card, command). Add the locked dependency set: `@tanstack/react-query`, `@tanstack/react-table`, `motion`, `recharts`, `react-hook-form`, `zod`, `cmdk`, `sonner`, `react-dropzone`, `react-resizable-panels`, `@monaco-editor/react`, `react-markdown`, `remark-gfm`, `rehype-sanitize`/DOMPurify, `lucide-react`, `class-variance-authority`, `clsx`, `tailwind-merge`, `date-fns`, `@microsoft/signalr`, `react-router-dom`. Configure Sonner toaster once at the app root. **No second component framework**; Magic UI/Aceternity are added later only as isolated, license-verified accents (UX-10, D18).

#### Technical Approach
- `shadcn` init + generate primitives; centralize `cn()` (clsx + tailwind-merge); pin explicit versions (no `latest` in production, verify licenses).

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
No secrets in bundle (FR-SEC-002); only license-compatible packages.

#### Error and Recovery Behavior
Sonner provides consistent toast feedback surface (consumed by `TASK-ERR-02`).

#### Acceptance Criteria
- Given the base primitives, when used, then they render with the design tokens and pass accessibility (keyboard/focus/ARIA via Radix).
- Given the dependency set, when audited, then versions are pinned and licenses are compatible.

#### Test Requirements
- Component: primitive render + keyboard/focus smoke tests.

#### Definition of Done
- [ ] shadcn/Radix primitives generated + package baseline pinned + Sonner root.

#### Implementation Notes
The official shadcn MCP may accelerate component generation, but design decisions stay governed by D13–D19 (MCP must not introduce a second framework or paid components).

#### Out of Scope
Shell/navigation (UX-04); feature-specific components (their modules).

---

### TASK-UX-03: Motion animation system and reduced-motion support
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-A
**Story:** STORY-UX-01
**Priority:** Medium
**Implementation order:** 2c
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-UX-02
**Blocks:** UX-09, UX-10
**Primary role:** Frontend

#### Objective
Provide a small, purposeful animation system (durations, easings, shared transition primitives) using Motion, with global reduced-motion handling.

#### Requirements Covered
ASDD §7, decisions D15.

#### Detailed Description
Define reusable transition presets and timings: button feedback 100–150 ms, popover 150–200 ms, drawer 200–300 ms, page content 200–350 ms, status transition 200–300 ms. Provide wrappers for drawer/sheet entry, tab-content transitions, collapsible/progressive-disclosure, list-entrance, and status-change transitions. Wire a global `prefers-reduced-motion` guard (Motion `useReducedMotion` + a `@media (prefers-reduced-motion: reduce)` fallback) that disables non-essential animation. **Prohibit** decorative animation in tables, forms, audit, and error/security screens.

#### Technical Approach
- Central `motion` presets module; `MotionConfig`/reduced-motion provider at the root.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Not applicable.

#### Error and Recovery Behavior
Not applicable.

#### Acceptance Criteria
- Given reduced-motion preference, when set, then non-essential animation is disabled while state is still conveyed.
- Given a drawer/tab/collapsible, when opened, then it animates within the defined timing budget only.

#### Test Requirements
- Component: reduced-motion disables animation; timing presets applied.

#### Definition of Done
- [ ] Motion presets + reduced-motion guard + shared transition wrappers.

#### Implementation Notes
Animation explains state (queued→processing→ready) and spatial origin (drawers); it is never decorative background movement.

#### Out of Scope
Processing-timeline visuals (UX-09); success screens with long celebrations (prohibited).

---

### TASK-UX-04: Application shell — sidebar, header, breadcrumbs, app switcher, theme, health
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-B
**Story:** STORY-UX-01
**Priority:** High
**Implementation order:** 2d
**Estimated size:** L
**Can run in parallel:** No
**Dependencies:** TASK-UX-02, TASK-AUTHZ-04
**Blocks:** All feature pages
**Primary role:** Frontend

#### Objective
Build the global application shell: collapsible left sidebar (platform + current-application sections), top header (breadcrumbs, command button, system-health indicator, theme switcher, notifications, user menu), and responsive/mobile navigation.

#### Requirements Covered
ASDD §7.1/§7.2, FR-APP-004 (navigation to app areas), FR-DSH-003, decisions D14, D17.

#### Detailed Description
Sidebar with icon+label (expanded) and tooltip (collapsed), an application switcher near the top, separators between **Platform** (Dashboard, Applications, Processing, Users, Audit, Operations) and **Current Application** (Overview, Knowledge, Agents, Runs, Outputs, Activity), and small counters only for actionable states (e.g. failed processing). Entries are gated by `useCapabilities` (`TASK-AUTHZ-04`) — viewers never see admin-only destinations; frontend visibility is UX only, never authorization (FR-3.5). Header includes breadcrumbs, a command button (opens UX-05), a system-health indicator (fed by `TASK-OPS-01`), theme switcher (UX-01), notifications, and user menu. Responsive: sidebar collapses to a mobile drawer.

#### Technical Approach
- shadcn sidebar/sheet/dropdown/breadcrumb + Radix; route-driven active state; health indicator via a lightweight readiness query.

#### API or Event Contracts
Consumes `me`/membership (module 4), `/health/ready` + operations health (module 31).

#### Data Changes
Not applicable.

#### Security and Authorization
Nav gating is UX convenience; the server still enforces every action (verified `TASK-TST-03`).

#### Error and Recovery Behavior
Health indicator degrades gracefully when readiness is unknown; 403 destinations are hidden.

#### Acceptance Criteria
- Given a Super Viewer, when viewing the shell, then only permitted destinations render and admin actions are absent.
- Given a narrow viewport, when opened, then the sidebar becomes an accessible mobile drawer.
- Given a degraded dependency, when readiness reports it, then the health indicator reflects it.

#### Test Requirements
- Component: role-based nav visibility, responsive behavior, health indicator states.

#### Definition of Done
- [ ] Collapsible shell + header + app switcher + breadcrumbs + health + responsive nav.

#### Implementation Notes
Context-specific actions belong on their pages, not the sidebar; counters appear only for actionable states.

#### Out of Scope
Command palette internals (UX-05); page content (feature modules).

---

### TASK-UX-05: Permission-aware command palette (cmdk)
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-B
**Story:** STORY-UX-01
**Priority:** Medium
**Implementation order:** 2e
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-UX-02, TASK-AUTHZ-04
**Blocks:** None
**Primary role:** Frontend

#### Objective
Implement a global Ctrl/Cmd+K command palette (cmdk) with permission-filtered commands and navigation.

#### Requirements Covered
ASDD §7, decisions D17.

#### Detailed Description
Register commands: create application, upload knowledge, create agent, run agent, open processing, view failed jobs, search outputs, switch application, open system health, change theme. Each command is filtered by `useCapabilities` so an Application Viewer never sees create/upload/agent-management commands. Include recent/contextual commands and fuzzy search. Replaces the need for an always-on large search field.

#### Technical Approach
- `cmdk` (or shadcn Command) with a registry of capability-tagged commands; keyboard shortcut wiring.

#### API or Event Contracts
Navigates/opens flows; no new backend contract.

#### Security and Authorization
Command visibility mirrors capabilities; actions still enforced server-side.

#### Error and Recovery Behavior
Unavailable actions are hidden, not shown-then-denied.

#### Acceptance Criteria
- Given Ctrl/Cmd+K, when pressed, then the palette opens and lists only permitted commands.
- Given a viewer, then create/upload/agent commands are absent.

#### Test Requirements
- Component: shortcut open, permission filtering, command execution/navigation.

#### Definition of Done
- [ ] Permission-aware command palette wired to the shell.

#### Implementation Notes
Keyboard-first polish without visual clutter; respects the same capability source as the shell.

#### Out of Scope
Full-text server search of content (not in baseline).

---

### STORY-UX-02: Deliver the signature RAG interaction surfaces

**As an:** application user
**I want:** shared data tables, actionable charts, form/editor primitives, a processing stage-timeline, and an evidence-first output/run workspace
**So that:** knowledge readiness, processing, and grounded AI output are presented clearly, consistently, and trustworthily (ASDD §7, §21; decisions D16, D19)

#### Business Value
The distinctive, trust-building surfaces (stage timeline + evidence-first output) plus consistent tables/forms across every module — built once, reused everywhere.

#### Story Acceptance Criteria
- One TanStack-Table pattern (search/filter/sort/paginate/columns/row-actions/empty/loading), Recharts conventions for actionable trends only, react-hook-form + zod forms, Monaco + resizable split layouts, a reusable stage-timeline + detail drawer, and an evidence-first Markdown workspace consumed by feature modules.

#### Dependencies
STORY-UX-01, STORY-MDR-01 (`TASK-MDR-01`).

#### Tasks
`TASK-UX-06`, `TASK-UX-07`, `TASK-UX-08`, `TASK-UX-09`, `TASK-UX-10`

---

### TASK-UX-06: Shared data-table pattern (TanStack Table)
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-C
**Story:** STORY-UX-02
**Priority:** High
**Implementation order:** 20a
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-UX-02
**Blocks:** Knowledge/users/processing/agents/runs/outputs/audit tables
**Primary role:** Frontend

#### Objective
Build one reusable, shadcn-styled TanStack Table component providing the enterprise table pattern used by every list screen.

#### Requirements Covered
FR-KNW-009, FR-DSH-002, FR-OUT-006, FR-17 (audit lists), ASDD §7; decisions D16.

#### Detailed Description
A headless TanStack-Table wrapper with: text search, status filter, date filter, sortable columns, column visibility, pagination, row actions (destructive actions in a row menu, not inline), whole-row click to detail, empty state, loading skeleton, and a responsive fallback. Server-driven filtering/paging integrates with TanStack Query. Consumed by Applications, Users, Files, Processing jobs, Agents, Runs, Outputs, and Audit tables (replaces per-module table code).

#### Technical Approach
- `@tanstack/react-table` engine + shadcn table/skeleton/dropdown; query-param-synced filters; optional `@tanstack/react-virtual` for very long lists.

#### API or Event Contracts
Consumes each module's list endpoint (filter/sort/page params).

#### Data Changes
Not applicable.

#### Security and Authorization
Row actions gated by capabilities; server still authorizes.

#### Error and Recovery Behavior
Empty/loading/error states standardized.

#### Acceptance Criteria
- Given a list screen, when configured, then search/filter/sort/paginate/column-visibility/row-actions/empty/loading all work from the shared component.
- Given a small viewport, then the responsive fallback renders.

#### Test Requirements
- Component: sorting/filtering/pagination/empty/loading + row-action gating.

#### Definition of Done
- [ ] Reusable data-table used by all list modules.

#### Implementation Notes
Feature modules (KNW-06, USR-04, PRDSH-02, RDSH-02, OUT-05, AUD-03, APP-03) consume this instead of bespoke tables.

#### Out of Scope
Module-specific columns/actions (defined in their tasks).

---

### TASK-UX-07: Forms foundation (react-hook-form + zod), Monaco editor and resizable panels
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-C
**Story:** STORY-UX-02
**Priority:** High
**Implementation order:** 20b
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-UX-02
**Blocks:** Agent editor, output editing, user/app forms
**Primary role:** Frontend

#### Objective
Provide the shared form foundation (react-hook-form + zod with shadcn Form), the Monaco editor wrapper, and the resizable split-panel primitive.

#### Requirements Covered
FR-AGT-001/003 (large prompt editor), FR-OUT-004 (edit), FR-PAR (parameter forms), ASDD §7.4/§7.5; decisions D18.

#### Detailed Description
A typed form pattern (zod schema → react-hook-form → shadcn Form fields) with consistent validation, error, and disabled states. A Monaco wrapper configured for large prompt/Markdown editing (line numbers, search, keyboard nav, JetBrains-Mono, no telemetry) used by the agent prompt editor and Markdown source editing. A `react-resizable-panels` primitive for the agent editor/settings split, the Markdown/preview split, and the output/evidence split.

#### Technical Approach
- `react-hook-form` + `@hookform/resolvers/zod`; `@monaco-editor/react`; `react-resizable-panels` with persisted sizes.

#### API or Event Contracts
Not applicable.

#### Data Changes
Not applicable.

#### Security and Authorization
Client validation is UX; server remains authoritative (Problem Details from `TASK-ERR-01`).

#### Error and Recovery Behavior
Field + form-level errors mapped from server validation category.

#### Acceptance Criteria
- Given a form, when submitted invalid, then zod + server validation errors display consistently.
- Given a large prompt, when edited in Monaco, then line numbers/search/keyboard nav work and no silent truncation occurs.
- Given a split layout, when dragged, then panels resize and sizes persist.

#### Test Requirements
- Component: form validation, Monaco mount/edit, panel resize.

#### Definition of Done
- [ ] Shared forms + Monaco wrapper + resizable panels.

#### Implementation Notes
Consumed by `TASK-AGT-04` (prompt editor), `TASK-OUT-02`/`TASK-OUT-03` (source/split/edit), user/app forms.

#### Out of Scope
Feature-specific schemas (their modules).

---

### TASK-UX-08: Charting conventions (Recharts)
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-C
**Story:** STORY-UX-02
**Priority:** Low
**Implementation order:** 20c
**Estimated size:** S
**Can run in parallel:** Yes
**Dependencies:** TASK-UX-01
**Blocks:** Dashboard trend charts
**Primary role:** Frontend

#### Objective
Provide token-themed Recharts chart primitives used only for actionable trends on dashboards.

#### Requirements Covered
FR-DSH-001 (platform metrics), FR-APP-004, ASDD §21; decisions D16.

#### Detailed Description
Themed line/area/bar chart wrappers (using design tokens) for: processing volume over time, failed vs completed jobs, agent-run duration, knowledge growth, and token use by agent. Enforce conventions: no pie charts for two values, no 3D, no decorative/synthetic-data charts; every chart must explain a real trend and include accessible labels/tooltips.

#### Technical Approach
- `recharts` composable components themed from CSS variables; responsive containers.

#### API or Event Contracts
Consumes dashboard aggregation endpoints (modules 26/27).

#### Data Changes
Not applicable.

#### Security and Authorization
Displays only authorized, scoped metrics.

#### Error and Recovery Behavior
Empty/insufficient-data state instead of a misleading chart.

#### Acceptance Criteria
- Given trend data, when rendered, then themed, labeled, responsive charts display; with no data, an empty state shows instead.

#### Test Requirements
- Component: chart render + empty-data state.

#### Definition of Done
- [ ] Reusable themed chart primitives for actionable trends.

#### Implementation Notes
Consumed by platform/application dashboards (`TASK-PDSH-02`, `TASK-ADSH-02`); charts are optional accents, not the dashboard's primary content (attention-first, see UX-10 note).

#### Out of Scope
Decorative visualizations; non-actionable metrics.

---

### TASK-UX-09: Processing stage-timeline, status system and detail drawer
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-C
**Story:** STORY-UX-02
**Priority:** High
**Implementation order:** 20d
**Estimated size:** M
**Can run in parallel:** Yes
**Dependencies:** TASK-UX-02, TASK-UX-03
**Blocks:** Processing dashboard, run dashboard
**Primary role:** Frontend

#### Objective
Build the reusable stage-timeline, the accessible status system, and the detail side-drawer used for processing jobs and agent runs.

#### Requirements Covered
FR-PRC-002/003 (stages/progress), FR-RUN-003/007, FR-DSH-002, ASDD §21.3/§21.4; decisions D15, D19.

#### Detailed Description
A vertical stage-timeline component (done ✓ / active ◉ with subtle pulse / pending ○) rendering ingestion stages (Uploaded, Validated, Parsed, Chunked, Embedding N/M, Indexed, Ready) and run stages, with per-stage progress. A status system where each status = color **+ icon + text label** (neutral=queued, cyan=processing, emerald=ready, amber=warning, rose=failed, gray=disabled) — never color alone. A detail side-drawer showing status, current stage, progress, timeline, timings (started/elapsed), initiator, and correlation ID; the active stage uses subtle pulsing only (UX-03), not decorative motion. Live updates arrive via SignalR (`TASK-SIG-03`/`TASK-SIG-04`) with REST as authority on reconnect.

#### Technical Approach
- Presentational timeline/status/drawer components consuming job/run detail; Motion for state transitions only.

#### API or Event Contracts
Consumes processing/run detail (modules 28/29) + SignalR events (module 25).

#### Data Changes
Not applicable.

#### Security and Authorization
Scoped by app membership; retry actions gated to admins/platform.

#### Error and Recovery Behavior
Failed stage shows safe error + correlation ID + retry affordance where eligible.

#### Acceptance Criteria
- Given a processing job, when its stage advances, then the timeline updates with an accessible status (icon + label, not color alone) and the active stage pulses subtly.
- Given a selected row, when opened, then the drawer shows timeline, timings, initiator, and correlation ID.

#### Test Requirements
- Component: timeline states, accessible status, drawer content, reduced-motion.

#### Definition of Done
- [ ] Reusable stage-timeline + status system + detail drawer.

#### Implementation Notes
Consumed by `TASK-PRDSH-02` and `TASK-RDSH-02`; this is one of the product's signature, trust-building surfaces (D19).

#### Out of Scope
Dashboard list/tab wiring (modules 28/29); backend stage data (modules 10/19).

---

### TASK-UX-10: Evidence-first output/run workspace shell and isolated accents/login
**Module:** 37 — Frontend Design System
**Epic:** EPIC-UX-C
**Story:** STORY-UX-02
**Priority:** High
**Implementation order:** 20e
**Estimated size:** L
**Can run in parallel:** Yes
**Dependencies:** TASK-UX-02, TASK-UX-07, TASK-MDR-01
**Blocks:** Output workspace, run detail, agent execution view
**Primary role:** Frontend

#### Objective
Build the evidence-first Markdown workspace shell (Preview / Source / Split with an evidence drawer and citation interaction), the staged AI-execution experience, and the isolated, license-verified visual accents (login, empty states).

#### Requirements Covered
FR-OUT-001/002, FR-CIT-003 (evidence viewer), FR-RUN-007 (staged progress), ASDD §7.5; decisions D14, D18, D19.

#### Detailed Description
A reusable workspace layout with three modes (Preview / Markdown Source / Split) rendering via the hardened `<SafeMarkdown>` (`TASK-MDR-01`, raw HTML disabled) on a resizable split (`TASK-UX-07`), plus an Evidence panel listing sources used. Citation interaction: clicking a citation highlights the marker, slides open the evidence drawer, scrolls to the matching passage, briefly highlights source lines, and shows filename + heading path. A staged AI-execution component ("Request accepted → sections searched → passages selected → generating → validating citations → saving") replacing a bare spinner, with an admin "Run details" panel; on completion it transitions smoothly to the output and briefly highlights the new state (no confetti, ≤300–500 ms). Isolated Magic UI/Aceternity accents are permitted only on login/empty states — never inside tables, forms, audit, or error dialogs — and must be license-verified (D18).

#### Technical Approach
- Composition of SafeMarkdown + resizable panels + evidence drawer; Motion transitions from UX-03; accent components isolated behind their own modules.

#### API or Event Contracts
Consumes output/run detail + citations (modules 22/23) and run stage events (module 25).

#### Data Changes
Not applicable.

#### Security and Authorization
All Markdown treated as untrusted (rendered via SafeMarkdown); viewers can view/copy/download, edit gated to admins.

#### Error and Recovery Behavior
Deleted-source citations handled gracefully (`TASK-CIT-04`); safe rendering always.

#### Acceptance Criteria
- Given an output, when a citation is clicked, then the evidence drawer opens, scrolls to and highlights the passage, and shows filename + heading path.
- Given a running agent, when executing, then staged progress (not a bare spinner) is shown and completion transitions smoothly to the output.
- Given untrusted Markdown, when rendered, then no script/unsafe embed executes.

#### Test Requirements
- Component: citation→evidence interaction, staged execution, sanitization; reduced-motion.

#### Definition of Done
- [ ] Evidence-first workspace shell + staged execution + isolated accents/login.

#### Implementation Notes
Consumed by `TASK-OUT-02`/`TASK-OUT-03` (output workspace), `TASK-RDSH-02` (run detail), and `TASK-AGTT-02` (agent test). This evidence-first surface is the product's most differentiating, trust-building UI (D19).

#### Out of Scope
Backend citation/evidence data (module 22); output actions (module 23).

---

# PART B — REVIEW, TRACEABILITY & COMPLETENESS

The following sections were produced by re-reading both source documents (FRD v1.1 and ASDD v1.1) after the backlog was drafted, then performing traceability, architecture, interaction, failure, security, gap, and duplication reviews. Every requirement below maps to at least one story and task, or is explicitly documented as deferred.

## R1. Requirements Traceability Matrix

**Status legend:** `Planned` = story + task defined in this backlog; no code implemented yet (greenfield).
**Validation method:** Unit / Integration / Architecture / Security / E2E / Manual (per module 34 test tasks).

### R1.1 Roles & Authorization (FRD §3)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| §3.1 Super Admin capabilities | STORY-AUTHZ-01 | TASK-AUTHZ-01, TASK-AUTHZ-02, TASK-APP-02 | Security, Integration | Planned |
| §3.2 Super Viewer (read-only) | STORY-AUTHZ-01 | TASK-AUTHZ-01, TASK-AUTHZ-02, TASK-AUTHZ-04 | Security | Planned |
| §3.3 Application Admin capabilities | STORY-AUTHZ-01 | TASK-AUTHZ-01, TASK-AUTHZ-02, TASK-APP-05 | Security, Integration | Planned |
| §3.4 Application Viewer (+ viewer-exec gate) | STORY-AUTHZ-01, STORY-APP-01 | TASK-AUTHZ-01, TASK-AUTHZ-02, TASK-APP-06 | Security | Planned |
| §3.5 Backend enforcement everywhere | STORY-AUTHZ-01 | TASK-AUTHZ-02, TASK-AUTHZ-03 | Security, Architecture | Planned |

### R1.2 Applications (FRD §4)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-APP-001 Create application | STORY-APP-01 | TASK-APP-01, TASK-APP-02, TASK-APP-03 | Integration, E2E | Planned |
| FR-APP-002 Application isolation | STORY-APP-01, STORY-AUTHZ-01 | TASK-APP-01, TASK-AUTHZ-02 | Security | Planned |
| FR-APP-003 Archive/restore/delete | STORY-APP-01 | TASK-APP-02, TASK-APP-04 | Integration | Planned |
| FR-APP-004 Application dashboard | STORY-ADSH-01 | TASK-ADSH-01, TASK-ADSH-02 | Integration, E2E | Planned |

### R1.3 User Management (FRD §5) — REMOVED (decision D20)

Module 5 / User Management is removed from the backlog; user accounts, credentials, and role/attribute assignment are provisioned in Keycloak, not this application. See the Module 5 removal note and `TASK-IDN-01`/`TASK-APP-05`.

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-USR-001 Roles are claims-derived | STORY-IDN-01 | TASK-IDN-01, TASK-IDN-02 | Integration | Planned |
| FR-USR-002 No local account lifecycle | STORY-IDN-01 | TASK-IDN-01 | Security | Planned |

### R1.4 Knowledge Files (FRD §6)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-KNW-001 Supported formats (TXT/MD) | STORY-KNW-01 | TASK-KNW-02 | Unit, Integration | Planned |
| FR-KNW-002 Multiple-file upload | STORY-KNW-01 | TASK-KNW-03, TASK-KNW-06 | Integration, E2E | Planned |
| FR-KNW-003 File validation | STORY-KNW-01 | TASK-KNW-02 | Unit | Planned |
| FR-KNW-004 File storage (S3) | STORY-KNW-01, STORY-S3-01 | TASK-KNW-03, TASK-S3-02 | Integration | Planned |
| FR-KNW-005 Duplicate detection | STORY-KNW-01 | TASK-KNW-03 | Integration | Planned |
| FR-KNW-006 File replacement | STORY-REP-01 | TASK-REP-01, TASK-REP-02 | Integration | Planned |
| FR-KNW-007 Disable file | STORY-KNW-01 | TASK-KNW-05 | Integration | Planned |
| FR-KNW-008 Delete file | STORY-REP-01, STORY-KNW-01 | TASK-KNW-05, TASK-REP-03 | Integration | Planned |
| FR-KNW-009 File list (+ preview) | STORY-KNW-01 | TASK-KNW-04, TASK-KNW-06 | Integration, E2E | Planned |

### R1.5 Processing (FRD §7)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-PRC-001 Background processing | STORY-PRC-01, STORY-JOB-01 | TASK-PRC-01, TASK-JOB-02, TASK-SQS-02 | Integration | Planned |
| FR-PRC-002 Processing stages | STORY-PRC-01 | TASK-PRC-01, TASK-PRC-03, TASK-CHK-01, TASK-EMB-01 | Integration | Planned |
| FR-PRC-003 Meaningful progress | STORY-PRC-01 | TASK-PRC-04, TASK-SIG-03 | Integration, E2E | Planned |
| FR-PRC-004 Markdown-aware chunking | STORY-CHK-01 | TASK-CHK-01, TASK-CHK-02, TASK-CHK-03 | Unit | Planned |
| FR-PRC-005 Embeddings | STORY-EMB-01 | TASK-EMB-01, TASK-EMB-02, TASK-EMB-03 | Integration | Planned |
| FR-PRC-006 Idempotency | STORY-JOB-01 | TASK-JOB-03, TASK-SQS-03, TASK-EMB-03 | Integration, Security | Planned |
| FR-PRC-007 Retry | STORY-JOB-01 | TASK-JOB-06, TASK-SQS-04 | Integration | Planned |
| FR-PRC-008 Failure details | STORY-PRC-01 | TASK-PRC-04, TASK-PRDSH-01 | Integration | Planned |

### R1.6 Processing Dashboard & Live Updates (FRD §8)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-DSH-001 Platform dashboard | STORY-PDSH-01 | TASK-PDSH-01, TASK-PDSH-02 | Integration, E2E | Planned |
| FR-DSH-002 Application processing page | STORY-PRDSH-01 | TASK-PRDSH-01, TASK-PRDSH-02 | Integration, E2E | Planned |
| FR-DSH-003 Live updates | STORY-SIG-01 | TASK-SIG-03, TASK-SIG-04, TASK-PDSH-02, TASK-PRDSH-02 | Integration, E2E | Planned |
| FR-DSH-004 Reconnection | STORY-SIG-01 | TASK-SIG-05, TASK-SIG-06 | Integration | Planned |

### R1.7 Agents (FRD §9)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-AGT-001 Create agent | STORY-AGT-01 | TASK-AGT-01, TASK-AGT-02, TASK-AGT-04 | Integration, E2E | Planned |
| FR-AGT-002 Prompt-defined output | STORY-AGT-01, STORY-PRMT-01 | TASK-AGT-01, TASK-PRMT-02 | Integration | Planned |
| FR-AGT-003 Large prompts | STORY-AGT-01 | TASK-AGT-04, TASK-PRMT-01 | Unit, Integration | Planned |
| FR-AGT-004 Knowledge scope | STORY-AGT-01 | TASK-AGT-05 | Integration, Security | Planned |
| FR-AGT-005 Simple changes without versioning | STORY-AGT-01 | TASK-AGT-02 | Integration | Planned |
| FR-AGT-006 Prompt visibility (role-scoped) | STORY-AGT-01, STORY-AUTHZ-01 | TASK-AGT-02, TASK-AUTHZ-02 | Security | Planned |

### R1.8 Generation & Retrieval Parameters (FRD §10)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-PAR-001 Initial generation parameters | STORY-AGT-01, STORY-GEN-01 | TASK-AGT-03, TASK-GEN-01 | Unit, Integration | Planned |
| FR-PAR-002 Initial retrieval parameters | STORY-AGT-01, STORY-RET-01 | TASK-AGT-03, TASK-RET-04 | Unit, Integration | Planned |
| FR-PAR-003 Presets | STORY-AGT-01 | TASK-AGT-03, TASK-AGT-04 | Unit | Planned |
| FR-PAR-004 Capability validation | STORY-AGT-01 | TASK-AGT-03 | Unit, Integration | Planned |

### R1.9 Azure OpenAI (FRD §11)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-AOI-001 Deployment configuration | STORY-AOI-01 | TASK-AOI-01 | Unit, Integration | Planned |
| FR-AOI-002 Authentication (Entra/API key) | STORY-AOI-01 | TASK-AOI-02 | Integration | Planned |
| FR-AOI-003 Provider abstraction | STORY-AOI-01 | TASK-AOI-01, TASK-AOI-03, TASK-TST-01 | Architecture, Unit | Planned |
| FR-AOI-004 Availability | STORY-AOI-01, STORY-OPS-01 | TASK-AOI-05, TASK-OPS-01 | Integration | Planned |
| FR-AOI-005 Rate/quota handling | STORY-AOI-01 | TASK-AOI-03 | Integration | Planned |

### R1.10 Retrieval (FRD §12)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-RET-001 Application-scoped search | STORY-RET-01 | TASK-RET-01 | Security, Integration | Planned |
| FR-RET-002 Ready & enabled knowledge only | STORY-RET-01 | TASK-RET-01, TASK-RET-05 | Integration | Planned |
| FR-RET-003 Semantic retrieval | STORY-RET-01 | TASK-RET-01, TASK-PG-06 | Integration | Planned |
| FR-RET-004 Hybrid retrieval | STORY-RET-01 | TASK-RET-02, TASK-PG-04 | Unit, Integration | Planned |
| FR-RET-005 Duplicate suppression | STORY-RET-01 | TASK-RET-03 | Unit | Planned |
| FR-RET-006 Context budgeting | STORY-RET-01, STORY-PRMT-01 | TASK-RET-04, TASK-PRMT-01 | Unit | Planned |
| FR-RET-007 No evidence (strict grounding) | STORY-RET-01 | TASK-RET-05 | Integration | Planned |
| FR-RET-008 Partial readiness | STORY-RET-01 | TASK-RET-05 | Integration | Planned |

### R1.11 Agent Runs (FRD §13)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-RUN-001 Submit run | STORY-RUN-01 | TASK-RUN-01 | Integration, E2E | Planned |
| FR-RUN-002 Asynchronous execution | STORY-RUN-01 | TASK-RUN-01, TASK-RUN-02, TASK-SQS-02 | Integration | Planned |
| FR-RUN-003 Run stages | STORY-RUN-01 | TASK-RUN-02, TASK-RUN-03 | Integration | Planned |
| FR-RUN-004 Run snapshot | STORY-RUN-01, STORY-RDSH-01 | TASK-RUN-01, TASK-GEN-01, TASK-CIT-02, TASK-RDSH-01 | Integration | Planned |
| FR-RUN-005 Duplicate submission | STORY-RUN-01 | TASK-RUN-01 | Integration, Security | Planned |
| FR-RUN-006 Prompt assembly | STORY-PRMT-01 | TASK-PRMT-01, TASK-PRMT-02 | Unit | Planned |
| FR-RUN-007 Progress | STORY-RUN-01, STORY-SIG-01 | TASK-RUN-03, TASK-SIG-04 | Integration, E2E | Planned |

### R1.12 Markdown Output (FRD §14)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-OUT-001 Views (preview/source/split) | STORY-OUT-01 | TASK-OUT-02 | E2E | Planned |
| FR-OUT-002 Secure rendering | STORY-MDR-01 | TASK-MDR-01, TASK-MDR-02 | Security | Planned |
| FR-OUT-003 Stored output | STORY-OUT-01 | TASK-OUT-01 | Integration | Planned |
| FR-OUT-004 Edit output (preserve original) | STORY-OUT-01 | TASK-OUT-03 | Integration | Planned |
| FR-OUT-005 Output actions | STORY-OUT-01 | TASK-OUT-04 | E2E | Planned |
| FR-OUT-006 Output library | STORY-OUT-01 | TASK-OUT-05 | Integration, E2E | Planned |

### R1.13 Citations (FRD §15)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-CIT-001 Structural citations | STORY-CIT-01, STORY-PRMT-01 | TASK-PRMT-02, TASK-CIT-03 | Unit | Planned |
| FR-CIT-002 Citation validation | STORY-CIT-01 | TASK-CIT-03 | Unit, Integration | Planned |
| FR-CIT-003 Evidence viewer | STORY-CIT-01 | TASK-CIT-01, TASK-CIT-02, TASK-CIT-04 | E2E | Planned |
| FR-CIT-004 Deleted source handling | STORY-CIT-01 | TASK-CIT-04 | Integration | Planned |

### R1.14 Security (FRD §16)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| FR-SEC-001 Prompt injection resistance | STORY-PRMT-01 | TASK-PRMT-02, TASK-RET-05 | Security | Planned |
| FR-SEC-002 Secrets (no leakage) | STORY-AOI-01, STORY-ERR-01, STORY-OBS-01 | TASK-AOI-02, TASK-FND-07, TASK-ERR-01, TASK-OBS-01 | Security | Planned |
| FR-SEC-003 File access control | STORY-S3-01, STORY-AUTHZ-01 | TASK-S3-02, TASK-S3-03, TASK-AUTHZ-02 | Security | Planned |
| FR-SEC-004 Cross-application protection | STORY-AUTHZ-01 | TASK-AUTHZ-02, TASK-AUTHZ-03, TASK-RET-01 | Security | Planned |
| FR-SEC-005 Secure authentication | STORY-IDN-01 | TASK-IDN-01, TASK-IDN-02, TASK-MDR-02 | Security | Planned |

### R1.15 Audit, Health, Deployment (FRD §17–§19)

| Requirement | Story | Tasks | Validation | Status |
|---|---|---|---|---|
| §17 Audit events & safe records | STORY-AUD-01 | TASK-AUD-01, TASK-AUD-02, TASK-AUD-03, TASK-IDN-04 | Integration, Security | Planned |
| §18 Health & operations page | STORY-OPS-01 | TASK-OPS-01, TASK-OPS-02, TASK-OPS-03, TASK-SQS-04 | Integration | Planned |
| §19 Local deployment (compose + scripts) | STORY-FND-02, STORY-FND-03, STORY-DEP-01 | TASK-FND-04, TASK-FND-05, TASK-FND-08, TASK-FND-09, TASK-FND-10, TASK-DEP-01 | Manual, E2E | Planned |

### R1.16 Acceptance Criteria (FRD §20)

| # | Acceptance criterion | Verifying tasks | Status |
|---|---|---|---|
| 1 | Compose starts all components | TASK-FND-05, TASK-DEP-01, TASK-REL-01 | Planned |
| 2 | Bootstrap Super Admin can log in via Keycloak SSO | TASK-FND-10, TASK-IDN-01, TASK-IDN-02 | Planned |
| 3 | Create application + assign application roles (Keycloak) | TASK-APP-02, TASK-APP-05 | Planned |
| 4 | Upload multiple TXT/MD files | TASK-KNW-03, TASK-KNW-06 | Planned |
| 5 | Files stored in LocalStack S3 | TASK-S3-02 | Planned |
| 6 | Jobs pass through LocalStack SQS | TASK-SQS-02, TASK-SQS-03 | Planned |
| 7 | Worker generates AOAI embeddings | TASK-EMB-01, TASK-AOI-03 | Planned |
| 8 | Chunks + vectors stored in pgvector | TASK-PG-02, TASK-EMB-03 | Planned |
| 9 | SignalR shows processing progress | TASK-SIG-03, TASK-PRC-04 | Planned |
| 10 | Create agent with large prompt + settings | TASK-AGT-04, TASK-AGT-03 | Planned |
| 11 | Viewer runs enabled agent when permitted | TASK-APP-06, TASK-RUN-01 | Planned |
| 12 | Retrieval only uses authorized content | TASK-RET-01, TASK-AUTHZ-02 | Planned |
| 13 | AOAI generates grounded Markdown | TASK-GEN-01, TASK-PRMT-02 | Planned |
| 14 | Output safely rendered + citations inspectable | TASK-MDR-01, TASK-CIT-04 | Planned |
| 15 | Failed jobs investigated & retried | TASK-JOB-06, TASK-PRDSH-02, TASK-RDSH-02 | Planned |
| 16 | Run preserves prompt/model/param/evidence | TASK-RUN-01, TASK-GEN-01, TASK-CIT-02 | Planned |
| 17 | Cross-application access tests fail | TASK-TST-03, TASK-AUTHZ-02 | Planned |
| 18 | Restart does not lose persisted state | TASK-PG-01, TASK-JOB-01, TASK-TST-03 | Planned |
| 19 | No Ollama/Redis/cache dependency | TASK-FND-01, TASK-TST-02 (architecture) | Planned |

**Coverage result:** All 73 `FR-XXX-NNN` requirements, all four role definitions (§3), audit/health/deployment sections (§17–§19), and all 19 acceptance criteria (§20) map to at least one story and task. No requirement is unmapped.

**Frontend design-system mapping (Module 37):** The UX module provides the cross-cutting frontend foundation consumed by the already-mapped UI tasks — its tasks are not a new requirement but the shared implementation surface for existing ones: `TASK-UX-04`/`TASK-UX-05` (shell + palette) support §3.5 role-aware navigation and FR-APP-004; `TASK-UX-06` (data table) supports FR-KNW-009, FR-DSH-002, FR-OUT-006, FR-17; `TASK-UX-07` (forms/Monaco/panels) supports FR-AGT-001/003 and FR-OUT-004; `TASK-UX-08` (charts) supports FR-DSH-001/APP-004; `TASK-UX-09` (stage-timeline) supports FR-PRC-002/003 and FR-RUN-003/007; `TASK-UX-10` (evidence-first workspace) supports FR-OUT-001/002 and FR-CIT-003. Governed by decisions D13–D19.

---

## R2. Architecture Coverage Review (ASDD)

| ASDD area | Covered by | Notes |
|---|---|---|
| §4 Modular monolith + 2 workers | Modules 1, 10, 19; TASK-FND-01, TASK-PRC-01, TASK-RUN-02 | Architecture tests enforce boundaries (TASK-TST-02) |
| §5 Component/module map | Module 1 folder layout; 36 functional-area modules + Module 37 (cross-cutting frontend design system) | — |
| §6 Frontend architecture | Module 37 (design system, shell, shared components); TASK-FND-02, TASK-AUTHZ-04, module 24 | shadcn/Radix + Tailwind tokens; React/Vite/Query/Router/SignalR; evidence-first UX (D13–D19) |
| §7 Authorization model | Module 3 (AUTHZ) | Policies + resource-based + SignalR groups |
| §8 Async/messaging (outbox, DLQ) | Modules 8, 9; TASK-JOB-04, TASK-JOB-05, TASK-SQS-03 | Outbox baseline + reconciler (D1) |
| §9 Data model / entities | Modules 14, 6, 9, 17, 19, 22, 23, 30, 31 | Per-entity tables/migrations |
| §10 Indexes | TASK-PG-03, TASK-PG-02, TASK-PG-04 | Vector + FTS + relational |
| §11 Configuration | TASK-FND-07, TASK-AOI-01 | Env + secrets |
| §12 Deployment topology | Modules 1, 35 | Compose + startup ordering |
| §13 Processing pipeline | Modules 10, 11, 13 | Stages + chunking + embedding |
| §14 Retrieval pipeline | Module 16 | Semantic + hybrid + fusion + budget |
| §15 Generation pipeline | Modules 20, 21, 22 | Prompt + generation + citations |
| §16 User journeys | TASK-TST-04, TASK-REL-01 | E2E coverage |
| §17 Domain services / audit | Module 30 | Audit writer + emission |
| §18 Realtime (SignalR) | Module 25 | Hub + reconnection + REST authority |
| §19 Security model | Modules 2, 3, 24; module 34 | Auth, authz, sanitization |
| §20 Cross-cutting (correlation, safe logging) | Module 33 | Correlation + sensitive exclusion |
| §21 Dashboards | Modules 26–29 | Platform/app/processing/run |
| §22 Error handling | Module 32 | Problem Details + categories |
| §23 Observability | Module 33 | Logs + metrics + traces |
| §24 Health/operations | Module 31 | Liveness/readiness + heartbeats |
| §25 Testing strategy | Module 34 | Unit→E2E + fakes |
| §26 Developer experience | Module 35 | One-command + runbook |
| §27 Limitations/deferred | TASK-REL-02; decisions D1–D12 | Documented deferrals |

**Result:** Every major ASDD section maps to at least one module/task.

---

## R3. Cross-Cutting Concerns Review

| Concern | Where enforced |
|---|---|
| Authorization (every API/file/agent/run/retrieval/SignalR) | Module 3; TASK-AUTHZ-02, TASK-AUTHZ-03; verified TASK-TST-03 |
| Application isolation | TASK-AUTHZ-02, TASK-RET-01, TASK-S3-02; verified TASK-TST-03 |
| Idempotency (jobs + runs) | TASK-JOB-03, TASK-SQS-03, TASK-RUN-01, TASK-EMB-03 |
| Correlation IDs (browser→worker→SignalR) | TASK-OBS-01; consumed by TASK-ERR-01, TASK-AUD-01 |
| Safe error contract | Module 32 (all endpoints) |
| Secret handling | TASK-FND-07, TASK-AOI-02; no secrets in logs/audit/errors |
| Sanitization of untrusted Markdown | Module 24; verified TASK-TST-03 |
| Immutability of run snapshots | TASK-RUN-01, TASK-GEN-01, TASK-CIT-02 |
| Embedding-config immutability for retrieval (D10) | TASK-AOI-01, TASK-EMB-03, TASK-RET-01 |
| Realtime + REST authority | Module 25; TASK-SIG-06 |

---

## R4. User Journey Coverage (ASDD §16)

| Journey | Tasks | Status |
|---|---|---|
| Platform admin bootstraps platform | TASK-FND-10, TASK-IDN-01, TASK-APP-02 | Planned |
| Provision application + members | TASK-APP-02, TASK-APP-05 | Planned |
| Upload knowledge → ready | TASK-KNW-03, TASK-PRC-01..04, TASK-EMB-03, TASK-SIG-03 | Planned |
| Author + test agent | TASK-AGT-01..05, TASK-AGTT-01, TASK-AGTT-02 | Planned |
| Run agent → grounded output | TASK-RUN-01..03, TASK-RET-01..05, TASK-GEN-01, TASK-CIT-03 | Planned |
| Inspect citations/evidence | TASK-CIT-04 | Planned |
| Edit/export output | TASK-OUT-03, TASK-OUT-04 | Planned |
| Replace/delete knowledge | TASK-REP-01..03 | Planned |
| Monitor dashboards | Modules 26–29 | Planned |
| Investigate + retry failures | TASK-JOB-06, TASK-PRDSH-02, TASK-RDSH-02 | Planned |
| Review audit + operations | TASK-AUD-03, TASK-OPS-03 | Planned |

All primary journeys are covered end-to-end and verified by TASK-TST-04 (E2E) and TASK-REL-01.

---

## R5. Failure Scenario Coverage

| Failure scenario | Handling task(s) | Verified by |
|---|---|---|
| Transient AOAI error / throttling (429) | TASK-AOI-03, TASK-JOB-06 | TASK-TST-03 |
| AOAI unavailable/misconfigured | TASK-AOI-05, TASK-OPS-01, TASK-GEN-03 | TASK-TST-01 (fake down) |
| Duplicate SQS delivery | TASK-SQS-03, TASK-JOB-03, TASK-EMB-03 | TASK-TST-03 |
| Poison message → DLQ | TASK-SQS-04 | TASK-TST-03 |
| Worker crash / stale lease | TASK-JOB-03, TASK-JOB-06, TASK-OPS-02 | TASK-TST-03 |
| Outbox undelivered message | TASK-JOB-04, TASK-JOB-05 | Integration |
| Processing stage failure | TASK-PRC-04, TASK-PRDSH-01 | Integration |
| No evidence retrieved (strict grounding) | TASK-RET-05 | Integration |
| Context overflow | TASK-PRMT-01 | Unit |
| Invalid/hallucinated citations | TASK-CIT-03 | Unit |
| Malformed generated Markdown | TASK-GEN-02 | Unit |
| Deleted source referenced by output | TASK-CIT-04 | Integration |
| Replacement processing fails (keep old active) | TASK-REP-02 | Integration |
| SignalR disconnect | TASK-SIG-05, TASK-SIG-06 | Integration |
| Pre-generation cancellation | TASK-RUN-05 | Integration |
| Disabled user / role change mid-session | TASK-IDN-01 | Security |

---

## R6. Security Review

| Control | Requirement | Tasks |
|---|---|---|
| Cookie auth (HTTP-only/Secure/SameSite) + CSRF | FR-SEC-005 | TASK-IDN-01, TASK-IDN-02 |
| Session revocation on Keycloak-side account change | FR-USR-002 | TASK-IDN-01 |
| Backend authorization everywhere | §3.5 | TASK-AUTHZ-02, TASK-AUTHZ-03 |
| Cross-application isolation | FR-SEC-004 | TASK-AUTHZ-02, TASK-RET-01, TASK-S3-02 |
| Private storage + server-generated keys | FR-SEC-003 | TASK-S3-01, TASK-S3-02 |
| Prompt-injection resistance + strict grounding | FR-SEC-001 | TASK-PRMT-02, TASK-RET-05 |
| Untrusted Markdown sanitization + CSP | FR-OUT-002 | TASK-MDR-01, TASK-MDR-02 |
| No secrets in logs/audit/errors | FR-SEC-002 | TASK-AOI-02, TASK-ERR-01, TASK-OBS-01, TASK-AUD-01 |
| Safe error messages (Problem Details) | ASDD §22 | TASK-ERR-01 |
| Audit trail without sensitive data | FRD §17 | TASK-AUD-01, TASK-AUD-02 |

All are verified by dedicated security/isolation tests in TASK-TST-03. No OWASP Top-10 category (injection, broken access control, sensitive-data exposure, SSRF via file keys, security misconfiguration) is left unaddressed for this local scope.

---

## R7. Missing Requirements and Gaps

Discovered during review and **added** to the backlog (not left as gaps):

- **Cross-cutting error contract** was implicit in the FRD but explicit in ASDD §22 → added Module 32 (ERR) with a dedicated `TASK-ERR-01` at an early implementation order so every later endpoint uses it.
- **Correlation propagation** across the full async chain → added Module 33 (OBS) `TASK-OBS-01` and referenced by audit/error tasks.
- **Worker heartbeat + stale detection** (needed by §18 and dashboards) → `TASK-OPS-02`, `TASK-JOB-06`.
- **Transactional outbox + reconciler** for DB/queue consistency (ASDD §8) → `TASK-JOB-04`, `TASK-JOB-05` (decision D1).
- **Deletion worker** for asynchronous cascade deletes (FR-KNW-008) → `TASK-REP-03`, `deletion-jobs` queue.
- **Fake AOAI test service** so deterministic tests do not consume quota (ASDD §25) → `TASK-TST-01`.

**Explicitly deferred (documented, not gaps):**

- HNSW/approximate vector indexing — exact cosine baseline first (D5, `TASK-PG-06`, `TASK-REL-02`).
- Token-level output streaming — stage-level progress only (D3).
- Mid-generation cancellation — pre-generation cancellation only (D2, `TASK-RUN-05`).
- Redis/cache, Ollama, general agent versioning, multi-tenant scaling — out of scope by FRD §20.19 and decisions D9.

No requirement remains without either a mapped task or an explicit deferral.

---

## R8. Duplicate and Overengineering Review

- **Consolidated** all Docker/compose/init/migration/seed setup into Module 1 + Module 35 rather than repeating per module (removed would-be duplicates in processing/agent modules).
- **Single audit writer** (`TASK-AUD-01`) reused by all modules instead of per-module audit code; emission centralized in `TASK-AUD-02`.
- **Single Problem Details contract** (`TASK-ERR-01`) instead of per-endpoint error shaping.
- **Shared dashboard components** — processing and run dashboards reuse table/list/live patterns (`TASK-PRDSH-02`, `TASK-RDSH-02`) rather than bespoke implementations.
- **Single design system & shell** — Module 37 centralizes design tokens, the application shell, command palette, and shared table/chart/timeline/editor/evidence components so feature modules consume them instead of reimplementing UI primitives (enforces the "no second UI framework" rule, D13; Motion used only for purposeful transitions, D15).
- **One retrieval pipeline** (Module 16) reused by agent test (`TASK-AGTT-01`) and runs (`TASK-RUN-02`) — no duplicate retrieval logic.
- **Removed overengineering:** no caching layer, no message-bus abstraction beyond SQS, no CQRS/event-sourcing, no premature ANN index, no generic versioning framework — all excluded per decisions D5/D9. Health deep-checks kept sparing to avoid AOAI quota use.

---

## R9. Final Completeness Statement

**Totals**

- **Phases:** 13 (P0–P12)
- **Modules:** 37 (36 functional-area modules + 1 cross-cutting frontend design-system module)
- **Epics:** 79
- **User stories:** 41
- **Tasks:** 151 (each in full implementation-contract format)

**Tasks by priority (approximate)**

- High: foundation, identity, authorization, storage/queue/job infra, processing, embeddings, pgvector, retrieval, agents, runs, generation, citations, audit writer, error contract, health, core tests (~ the critical path).
- Medium: dashboards UI, outputs actions, observability metrics, deployment runbook, E2E/smoke tests.
- Low: distributed tracing spans (`TASK-OBS-03`).

**Tasks by primary role**

- Backend / Database: majority (infra, processing, retrieval, generation, persistence).
- Frontend: auth UX, knowledge/agent/output UIs, dashboards, secure rendering, SignalR client.
- Full Stack: audit search, operations page.
- DevOps: compose/startup/runbook.
- QA / Security: Module 34 test tasks + release verification.

**Critical-path sequence (must be sequential)**

1. Module 1 (Foundation) → 2 (Identity) → 3 (Authorization)
2. Module 14 (PostgreSQL/pgvector) + 7 (S3) + 8 (SQS) + 9 (Job infra)
3. Module 6 (Knowledge) → 10 (Processing) → 11 (Chunking) → 12 (AOAI) → 13 (Embeddings)
4. Module 16 (Retrieval) → 17 (Agents) → 19 (Runs) → 20 (Prompt) → 21 (Generation) → 22 (Citations) → 23 (Outputs)
5. Cross-cutting (30 Audit, 31 Health, 32 Errors, 33 Observability) woven in — `TASK-ERR-01` and `TASK-AUD-01` land early (implementation orders 26b/119).
6. Module 34 (Testing) throughout → 35 (Deployment) → 36 (Release).

**Parallel workstreams**

- Frontend design system + application shell (Module 37) parallel to backend infra, immediately after `TASK-FND-02`; every later UI module depends on it.
- Frontend scaffolding (`TASK-FND-02`, `TASK-AUTHZ-04`, module 24) parallel to backend infra.
- Dashboards (26–29) parallel once their aggregation endpoints and the shared components (Module 37) exist.
- Observability (33) and error contract (32) parallel to feature modules.
- Test authoring (34) parallel to the modules under test using the fake AOAI harness.

**Remaining assumptions/decisions:** D1–D19 (Part A) remain the governing decisions; none were invalidated during review. D13–D19 capture the UI/UX direction (dark-first AI operations workspace, shadcn/Radix foundation, purposeful Motion, TanStack Table/Recharts, permission-aware command palette, react-hook-form/zod + Monaco, evidence-first UX).

**Known risks**

- Azure OpenAI availability/quota (mitigated by fakes for tests, sparing health checks, retry classification).
- Exact cosine search performance at larger corpora (mitigated by baseline-then-HNSW deferral and a performance baseline in `TASK-REL-02`).
- LocalStack re-init only on fresh containers and named-volume persistence (captured in `TASK-FND-08`, `TASK-DEP-01` reset guidance).

**Deferred scope:** HNSW indexing, output streaming, mid-generation cancellation, caching/Redis/Ollama, general versioning, production/multi-node scaling (see R7 and `TASK-REL-02`).

**Source-document review confirmation:** Both source documents — `docs/Functional Requirements Document.md` (FRD v1.1) and `docs/Architecture and Solution Design Document.md` (ASDD v1.1) — were read in full at the start and re-reviewed after drafting to build Sections R1–R9. Every FRD requirement (§3 roles, all 73 `FR-XXX-NNN`, §17–§19, and the 19 §20 acceptance criteria) and every major ASDD section (§4–§27) is mapped to at least one story and task, or is explicitly documented as deferred. This backlog is implementation-ready: an engineering team or AI coding agent can execute it module-by-module in the stated dependency order without returning to the source documents.

*End of backlog.*
