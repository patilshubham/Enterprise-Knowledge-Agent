# User Manual Analysis

Planning and verification notes behind [docs/user-manual.md](user-manual.md).
This is a maintainer artifact: it records what was inspected, the exact
user-facing strings verified, screenshots and diagrams to produce, and any
conflicts or gaps. It contains no credentials or secrets.

Verified against the running application at `http://localhost:8080` and the
frontend source under `src/frontend` on 2026-08-18. Authoritative order:
running app behavior → frontend/route/validation code → API contracts → design
docs.

---

## Product Summary

Enterprise Knowledge Agent is a browser-based retrieval-augmented generation
(RAG) platform. A user creates an isolated **application**, uploads `.txt`/`.md`
knowledge files, lets the system process them into a searchable vector index,
configures a prompt-driven **agent**, then either **runs** the agent against a
request or **chats** against the knowledge. Answers are grounded in the uploaded
files and include evidence and citations.

## Intended Audiences

- First-time users learning the create → upload → configure → run/chat → review flow.
- Power users tuning retrieval/generation parameters and reviewing evidence.
- Operators monitoring processing queues, worker health, and audit activity.

## User Roles

> **Important conflict (documented, not silently resolved).** The design
> documents describe four roles (SuperAdmin, SuperViewer, ApplicationAdmin,
> ApplicationViewer) behind Keycloak authentication. The **current application
> has no authentication and no login**. The frontend capability hook
> (`src/frontend/src/app/authz/useCapabilities.ts`) is a stub that returns
> `FULL_CAPABILITIES` — every capability flag is `true` — and every API call is
> attributed to a single actor, `local-user`. Therefore, in the running product
> there is effectively **one role with full access**. Role names below are
> documented for context and labeled **Planned**.

Capability flags (all `true` in this build), from
`src/frontend/src/app/authz/capabilities.ts`: `isSuperAdmin`, `isSuperViewer`,
`canManageApplications`, `canViewApplication`, `canManageKnowledge`,
`canManageAgents`, `canExecuteAgent`, `canViewFullPrompts`, `canManageOutput`,
`canViewOutput`, `canViewPlatformProcessing`, `canViewAudit`, `canViewOperations`.

## Role and Permission Matrix

| Capability | Current build | Planned role(s) that would hold it |
| --- | --- | --- |
| View platform dashboard/operations/audit | Enabled | SuperAdmin, SuperViewer |
| Create/manage applications | Enabled | SuperAdmin |
| Manage knowledge, agents; run agents; manage outputs | Enabled | SuperAdmin, ApplicationAdmin (+ Viewer run if `allowViewerExecution`) |
| View application, view outputs | Enabled | all roles with access to the application |

The server still enforces **application isolation** (cross-application access
returns 404) and **read-only-state guards** (archived/deleted applications reject
mutations), independent of the UX capability flags.

## Application Navigation

Two sidebar groups (source: `src/frontend/src/app/shell/nav-items.ts`, verified live):

- **PLATFORM:** Dashboard (`/platform/dashboard`), Applications
  (`/platform/applications`), Processing (`/platform/processing`), Audit
  (`/platform/audit`), Operations (`/platform/operations`).
- **CURRENT APPLICATION** (appears after opening an application): Overview
  (`/overview`), Chat (`/chat`), Knowledge (`/knowledge`), Processing
  (`/processing`), Agents (`/agents`), Runs (`/runs`), Outputs (`/outputs`),
  Activity (`/activity`). Note: the per-application audit is the **Activity**
  item; there is no separate app-level "Audit" item.

Header (source: `src/frontend/src/app/shell/Header.tsx`): mobile nav button,
breadcrumbs, **Search** button with `⌘K` hint (command palette), realtime
connection indicator, health indicator, theme toggle. The application switcher
sits at the top of the sidebar.

## Page Inventory

Routes from `src/frontend/src/app/router.tsx` (18 routes). `:id` =
`:applicationId`.

| Page | Route | Purpose |
| --- | --- | --- |
| Platform Dashboard | `/platform/dashboard` | Cross-application health/throughput metrics |
| Applications | `/platform/applications` | List/create/manage applications |
| Platform Processing | `/platform/processing` | Platform-wide processing job queue |
| Platform Audit | `/platform/audit` | Platform-wide audit log |
| Operations | `/platform/operations` | Component health, worker heartbeats, queue depths, AOAI deep check |
| App Overview | `/applications/:id/overview` | Application summary + workflow pipeline |
| Chat | `/applications/:id/chat` and `/chat/:conversationId` | Grounded conversations with streaming |
| Knowledge | `/applications/:id/knowledge` | Knowledge file list + upload/manage |
| Knowledge File Detail | `/applications/:id/knowledge/:fileId` | File metadata, status, preview |
| App Processing | `/applications/:id/processing` | App processing job queue |
| Agents | `/applications/:id/agents` | Agent list + create |
| Agent Detail | `/applications/:id/agents/:agentId` | Prompt + generation/retrieval config, run |
| Runs | `/applications/:id/runs` | Agent run list + new run |
| Run Detail | `/applications/:id/runs/:runId` | Stage timeline, snapshot, evidence, cancel/retry |
| Outputs | `/applications/:id/outputs` | Output library |
| Output Detail | `/applications/:id/outputs/:outputId` | Markdown output, citations, edit/regenerate |
| Activity | `/applications/:id/activity` | App-scoped audit log |
| Not Found | `*` | 404 fallback |

Overlays/dialogs: command palette (`⌘K`), application create/edit dialog, agent
create dialog, knowledge upload dialog, run-agent dialog, conversation
rename/delete dialogs, and confirmation dialogs for archive/restore/delete/
disable/enable/reprocess/regenerate/cancel.

## Control Inventory

Verified exact labels (source files noted in the frontend inventory). Highlights:

- **Application form** (`ApplicationFormDialog.tsx`): Name (required, ≤200),
  Description (optional, ≤2000), "Allow viewers to run agents" checkbox. Buttons
  "Cancel" / "Create" (or "Save changes").
- **Agent create dialog** (`AgentCreateDialog.tsx`): Name (required, ≤200),
  Description (optional, ≤2000). Buttons "Cancel" / "Create".
- **Agent detail form**: Name, Description, Prompt (code editor, ≤100,000),
  "Model deployment" (select), Temperature (slider 0–1, presets Precise 0.2 /
  Balanced 0.7 / Creative 1.1 / Custom), Maximum output tokens (default 2048),
  Top P (optional 0–1), "Retrieval mode" (Semantic / Hybrid (semantic +
  keyword)), Retrieval count (1–50, default 8), Minimum similarity (0–1, default
  0.35), Maximum evidence tokens (default 4000), Include neighbors, Strict
  grounding, Citations required (checkboxes, default on), Knowledge scope (All
  ready files / Selected files only). Button "Save agent".
- **Knowledge upload** (`KnowledgeFileUploadDialog.tsx`): text/markdown files,
  per-file remove, "Upload (N)". Max size 10 MiB per file (enforced by
  `KnowledgeFiles:MaxFileSizeBytes`).
- **Run agent dialog** (`RunAgentDialog.tsx`): Agent (if listed), Request
  (required, ≤20,000), Output title (optional, ≤200). Button "Run".
- **Chat composer** (`ChatComposer.tsx`): message textarea (≤4000), "Send
  message"/"Stop generating"; helper "Enter to send · Shift+Enter for a new
  line". Advanced configuration (knowledge scope, files, model deployment,
  retrieval mode, temperature, system prompt) is snapshotted per conversation.
- **Output detail** (`OutputWorkspace`): tabs Preview / Markdown Source / Split;
  actions Copy, Download, Edit (Save/Cancel), Regenerate, Delete.
- **Tables**: search box, column visibility ("Columns"), sortable headers,
  Previous/Next pagination, per-row action menus.

## Workflow Inventory

1. First access / open the app (no login).
2. Create an application.
3. Open an application; understand the Overview and pipeline.
4. Upload knowledge files; monitor processing to Ready.
5. Manage a file (disable/enable/reprocess/replace/delete; preview).
6. Create an agent; configure prompt + generation + retrieval.
7. Enable/disable/delete an agent.
8. Run an agent (Request + optional title); watch stages; cancel; retry.
9. Review a run's evidence and output.
10. Work with an output (view, copy, download, edit, regenerate, delete).
11. Chat: new conversation, ask, stream, retry failed, rename/delete conversation.
12. Search/filter/sort/paginate lists (runs, outputs, files, processing, audit).
13. Monitor processing queue and operations; run the Azure OpenAI deep check.
14. Review audit/activity.
15. Use the command palette and switch applications.
16. Change theme.

## Configuration Inventory

User-editable, per agent (live) and per conversation (snapshot at creation):
knowledge scope + selected files, model deployment, temperature/preset, maximum
output tokens, top P, retrieval mode, retrieval count, minimum similarity,
maximum evidence tokens, include neighbors, strict grounding, citations required,
system prompt (chat). Application-level: name, description, "allow viewers to run
agents" (stored; not enforced without auth). Preferences: theme (dark/light).

## Status Inventory

Base status kinds (`src/frontend/src/components/status/status-tokens.ts`), each
shown with icon **and** text (never color alone): Queued, Processing, Ready,
Warning, Failed, Disabled.

- **Knowledge file:** Queued, Processing (or "Processing (X%)"), Ready, Failed,
  Disabled, Deleting, Deleted.
- **Processing job / Agent run:** Queued, Running→"Processing", Completed→"Ready",
  Failed, Cancelled→"Disabled".
- **Run stages:** Queued → Preparing → Retrieving → Building context →
  Generating → Validating → Saving → Completed.
- **Application:** Active, Archived, Deleted. **Agent:** Active, Disabled, Deleted.
- **Chat message:** streaming (animated dots), completed, failed (alert +
  message + Retry), no-results.

## Notification and Error Inventory

Toasts (Sonner) verified per action, e.g. "Application created", "Agent created -
configure it below", "Agent saved", "File queued for reprocessing", "Run
submitted", "Cancellation requested", "Output saved", "Regeneration started", with
matching "Failed to …" errors. Conversation rename/delete toast strings were not
directly confirmed — labeled **Unverified** in the manual. API errors surface as
Problem Details with a safe user-facing message and correlation id.

Representative failure: an agent run can end **Failed** with category
`RetrievalEmpty` and message "No evidence matched this request and strict
grounding is enabled for this agent." (observed in demo data) — such runs are
retry-eligible.

## Empty-State Inventory

Verified copy, e.g. Applications "No applications yet - create one to get
started."; Runs "No agent runs to show."; Chat thread "Ask about your knowledge
base" / "Answers are grounded in this application's knowledge files. Ask a
question below to get started."; Conversation sidebar "No conversations yet. Start
a new chat to ask about the knowledge base."; scope with no files "No ready
knowledge files are available for this application."

## Loading-State Inventory

Skeleton placeholders on detail pages (agent/run/output/knowledge-file), the chat
thread, the conversation sidebar, and tables. Dashboards show skeleton cards while
loading.

## Implemented Features

Applications, knowledge upload/processing/management, agents, runs (with
stages/cancel/retry), retrieval (semantic/hybrid), outputs (edit/regenerate/
citations), chat (streaming), dashboards, processing queue, operations, audit/
activity, realtime updates, command palette, theme toggle, search/filter/sort/
pagination.

## Partial Features

- **Roles/permissions:** capability model present; not enforced without auth.
- **"Allow viewers to run agents":** stored/shown; not enforced without roles.

## Planned Features

Authentication and roles; PDF/DOCX/image/audio/video ingestion; DOCX/PDF export;
multiple LLM providers/fallback; cache. (Design docs only.)

## Unavailable Features

No user account/profile management, no password reset, no logout (there is no
login), no billing.

## Existing Documentation

`README.md`, `docs/readme-analysis.md`, `docs/Architecture and Solution Design
Document.md`, `docs/Functional Requirements Document.md`, `docs/tasks.md`,
`docs/diagrams/*.mmd`.

## Existing Screenshots

Eight images already exist under `docs/screenshots/` (created for the README):
`platform-dashboard.png`, `platform-applications.png`, `platform-operations.png`,
`application-knowledge.png`, `agent-detail.png`, `run-detail.png`,
`output-detail.png`, `application-chat.png`. All match the current dark-theme UI
and are reused by the manual.

## Required Screenshots

Additional captures needed for the manual (see
[docs/screenshots/README.md](screenshots/README.md)): navigation sidebar,
application create dialog, agent create dialog, agent list, knowledge upload
dialog, run-agent dialog, run list, output library, command palette, platform
processing, activity/audit, application overview, annotated dashboard, annotated
agent-detail.

## Required Diagrams

User journey; navigation map; knowledge-file status lifecycle; agent-run stage
lifecycle; error/retry flow. Mermaid, embedded in the manual with sources under
`docs/diagrams/`.

## Demonstration Data Requirements

Existing demo data suffices: application "Test"; knowledge file `tasks.md`
(Ready); agent "List Tasks"; multiple completed and failed runs; several outputs;
three conversations. Actor is `local-user`; no secrets or personal data appear.

## Application Startup Requirements

Stack running via Docker Compose (`http://localhost:8080`), migrations applied,
and — for ingestion/generation to succeed — a real Azure OpenAI endpoint and
deployments configured. See the README Quick Start.

## Documentation Conflicts

1. **Auth/roles.** Docs describe Keycloak + four roles; the app has no auth and
   full-access `local-user`. Manual documents the real single-role behavior and
   labels roles **Planned**. Affected: every "who can" statement and the Roles
   section.
2. **"Allow viewers to run agents".** Present as a setting but not enforced
   (no roles). Manual notes it is stored but has no runtime effect in this build.
3. **Knowledge API path vs UI.** UI route `/knowledge`; API endpoint group
   `/files`. Manual uses the UI term "Knowledge" for users.

## Missing Information

Conversation rename/delete exact toast strings (labeled Unverified). No support
channel, license, or account-management flows exist.

## Unverified Behavior

- Exact conversation rename/delete toast copy.
- Live Azure OpenAI generation quality/latency (depends on external resource).
- Replace-file dialog copy (row action exists; not individually screenshotted).

## Documentation Risks

Readers who trust the design docs may expect login/roles that do not exist;
mitigated by explicit **Planned** labels and a Getting Started note. Ingestion and
runs fail without a configured Azure OpenAI resource; called out in Getting
Started and Troubleshooting.

---

## Pass 3 — Coverage Review

### Page coverage

| Page | Route | Roles (this build) | Manual section | Screenshot | Workflow coverage | Status |
| --- | --- | --- | --- | --- | --- | --- |
| Platform Dashboard | `/platform/dashboard` | all | Dashboard | `platform-dashboard`, `main-dashboard-annotated` | View/refresh | Complete |
| Applications | `/platform/applications` | all | Applications | `platform-applications`, `application-create-dialog` | Create/edit/archive/restore/delete | Complete |
| Platform Processing | `/platform/processing` | all | Processing | `platform-processing` | Monitor/filter/recover | Complete |
| Platform Audit | `/platform/audit` | all | Activity and Audit | (shares `application-activity`) | View/filter | Complete |
| Operations | `/platform/operations` | all | Operations | `platform-operations` | Monitor/deep check | Complete |
| App Overview | `/applications/:id/overview` | all | Application Overview | `application-overview` | Orient/navigate | Complete |
| Chat | `/applications/:id/chat` | all | Chat | `application-chat` | Ask/stream/rename/delete | Complete |
| Knowledge | `/applications/:id/knowledge` | all | Knowledge | `application-knowledge`, `knowledge-upload-dialog` | Upload/manage/preview | Complete |
| Knowledge File Detail | `/applications/:id/knowledge/:fileId` | all | Knowledge (Manage) | (covered by Knowledge) | Preview/manage | Complete |
| App Processing | `/applications/:id/processing` | all | Processing | (shares `platform-processing`) | Monitor/recover | Complete |
| Agents | `/applications/:id/agents` | all | Agents | `agents-list`, `agent-create-dialog` | Create/enable/disable/delete | Complete |
| Agent Detail | `/applications/:id/agents/:agentId` | all | Agents (Configure) | `agent-detail`, `agent-detail-annotated` | Configure/run | Complete |
| Runs | `/applications/:id/runs` | all | Runs | `runs-list`, `run-agent-dialog` | Submit/filter | Complete |
| Run Detail | `/applications/:id/runs/:runId` | all | Runs | `run-detail` | Watch/cancel/retry | Complete |
| Outputs | `/applications/:id/outputs` | all | Outputs | `outputs-library` | Search/filter | Complete |
| Output Detail | `/applications/:id/outputs/:outputId` | all | Outputs | `output-detail` | View/copy/download/edit/regenerate/delete | Complete |
| Activity | `/applications/:id/activity` | all | Activity and Audit | `application-activity` | View/filter | Complete |
| Command palette | overlay (`⌘K`) | all | Command palette | `command-palette` | Quick actions/navigation | Complete |
| Not Found | `*` | all | (n/a) | — | — | Not applicable |

### Control coverage

Documented in the manual: text inputs and text areas (with limits and validation),
prompt/Markdown editors, selects, temperature slider + presets, checkboxes/toggles,
primary/secondary/destructive buttons, table search/sort/columns/pagination/row
actions, tabs (Preview/Source/Split), upload dropzone, copy/download, confirmation
dialogs, and disabled-button reasons. See [Forms and Controls
Reference](user-manual.md#forms-and-controls-reference).

### Workflow coverage

Create application; edit/archive/restore/delete application; upload knowledge;
manage a knowledge file; create agent; configure agent; enable/disable/delete
agent; run an agent (incl. cancel/retry); review and manage an output; ask in chat;
rename/delete a conversation. Each includes prerequisites → steps → expected result
→ verify → errors/recovery.

### Role coverage

The single full-access behavior is documented, with a prominent conflict note that
authentication and roles are **Planned**. The Planned role matrix and the enforced
protections (application isolation, read-only state) are explained. Role-specific
screenshots are **not applicable** because there is one role in this build.

### Status coverage

Every visible status (knowledge file, processing job, agent run, run stages,
application, agent) is documented with meaning, trigger, user action, and next
state in [Status and Progress Reference](user-manual.md#status-and-progress-reference),
plus lifecycle diagrams under [How processing works](user-manual.md#how-processing-works).

### Configuration coverage

Agent settings, conversation settings, application settings, and theme preference
are documented with purpose, defaults, allowed values, effect, and risk in
[Settings and Configuration](user-manual.md#settings-and-configuration) and the
agent workflow.

### Diagrams added

`user-journey`, `navigation-map`, `knowledge-file-lifecycle`,
`agent-run-lifecycle`, `error-retry-flow` (sources under `docs/diagrams/`; all
render-verified in light and dark before embedding).

### Screenshots

21 images referenced by the manual, all present under `docs/screenshots/` (8 reused
from the README set, 13 captured for the manual, 2 of which are annotated). No
blocked captures.

### Remaining gaps / unverified

- Conversation rename/delete toast strings (labeled Unverified in the manual).
- Replace-file dialog copy (action documented; not individually screenshotted).
- Live Azure OpenAI answer quality/latency (depends on the external resource).
- No formal accessibility audit performed.
