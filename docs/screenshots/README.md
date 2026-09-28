# Screenshot Inventory

Tracks every screenshot referenced by [README.md](../../README.md). All images
are captured from the actual running application — none are mocked or edited to
add features that do not exist.

## Capture Standards

- **Viewport**: 1440 × 1000 (desktop). Mobile shots only if responsive behavior
  needs documenting (none currently).
- **Theme**: Dark theme (application default) unless a shot specifically
  demonstrates the light theme.
- **Browser**: Chromium via Playwright automation against `http://localhost:8080`.
- **File format**: PNG (or JPEG where the tool emits it), lowercase kebab-case.
- **Naming**: `docs/screenshots/<page-or-workflow>.png`.
- **Demonstration data**: The seeded-by-hand "Test" application and its "List
  Tasks" agent, `tasks.md` knowledge file, runs, outputs, and conversations.
- **Sensitive-data rules**: The build has no authentication and no real secrets
  in the UI. The only actor shown is `local-user`. No tokens, keys, personal
  data, or private hostnames appear. Azure OpenAI keys are never surfaced to the
  frontend by design.
- **Review requirement**: Each captured image was opened and visually checked
  for readability and absence of sensitive data before being referenced.

## Planned Screenshots

| Filename | README section | Purpose | Page / workflow | Required state | Role | Demo data | Sensitive info to remove | Capture method | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `platform-dashboard.png` | Screenshots, Overview | Show cross-application health/throughput overview | `/platform/dashboard` | Stack up, demo data present | n/a (no auth) | Test app metrics | none | Playwright | Captured |
| `platform-applications.png` | Screenshots, Usage Guide | Application list + create/manage | `/platform/applications` | Stack up | n/a | "Test" application | none | Playwright | Captured |
| `platform-operations.png` | Health & Operations | Worker heartbeats + queue depths | `/platform/operations` | Stack up, workers healthy | n/a | live workers/queues | none | Playwright | Captured |
| `application-knowledge.png` | Usage Guide | Knowledge file list + statuses | `/applications/:id/knowledge` | `tasks.md` ingested | n/a | tasks.md (Ready) | none | Playwright | Captured |
| `agent-detail.png` | Usage Guide | Agent prompt + generation/retrieval config | `/applications/:id/agents/:agentId` | "List Tasks" agent exists | n/a | List Tasks agent | none | Playwright | Captured |
| `run-detail.png` | Usage Guide, Background Processing | Run stages, snapshot, evidence | `/applications/:id/runs/:runId` | a completed run exists | n/a | completed run | none | Playwright | Captured |
| `output-detail.png` | Usage Guide | Generated markdown + citations | `/applications/:id/outputs/:outputId` | an output exists | n/a | grounded output | none | Playwright | Captured |
| `application-chat.png` | Usage Guide | Chat thread with grounded answer | `/applications/:id/chat/:conversationId` | a conversation exists | n/a | demo conversation | none | Playwright | Captured |

## Existing Screenshots

None were reusable. Prior QA PNGs that briefly lived in the repository root were
removed in an earlier cleanup; they were ad-hoc and not aligned to this README.
Every screenshot listed above was freshly captured for this documentation.

---

# User Manual Screenshot Inventory

Screenshots referenced by [docs/user-manual.md](../user-manual.md). The eight
images above (captured for the README) are reused by the manual; the additional
images below were captured specifically for the manual.

## Screenshot Standards

- **Browser:** Chromium via Playwright automation.
- **Viewport:** 1440 × 1000 desktop. **Zoom:** 100%.
- **Theme:** Dark (application default), one consistent theme.
- **File format:** PNG, lowercase kebab-case names.
- **Demonstration-data policy:** the seeded-by-hand "Test" application and its
  "List Tasks" agent, `tasks.md` file, runs, outputs, and conversations.
- **Sensitive-data policy:** the build has no authentication and no real secrets
  in the UI; the only actor shown is `local-user`. No tokens, keys, personal
  data, or private hostnames appear.
- **Annotation policy:** numbered red circular callouts overlaid on the running
  app before capture (no text baked into the image); explanations follow the
  image in the manual.
- **Review requirements:** each image was opened and visually verified for
  readability and absence of sensitive data.

## Planned Screenshots

| Filename | Manual section | Page/dialog | Purpose | Role | Required state | Demo data | Actions visible | Sensitive to hide | Method | Annotated | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `navigation-sidebar.png` | Application Layout | Sidebar | Show platform + application nav groups | n/a | App open | Test app | nav links | none | Playwright | No | Captured |
| `main-dashboard-annotated.png` | Dashboard | Platform dashboard | Orient the user to dashboard regions | n/a | Data present | Test metrics | header/nav/tiles | none | Playwright | Yes | Captured |
| `application-overview.png` | Application Overview | `/overview` | Show app summary + pipeline | n/a | App open | Test | — | none | Playwright | No | Captured |
| `application-create-dialog.png` | Applications workflow | Create dialog | Show application form fields | n/a | Dialog open | empty form | Cancel/Create | none | Playwright | No | Captured |
| `agents-list.png` | Agents module | `/agents` | Show agent list + create | n/a | Agent exists | List Tasks | Create agent | none | Playwright | No | Captured |
| `agent-create-dialog.png` | Agents workflow | Create dialog | Show initial agent form | n/a | Dialog open | empty form | Cancel/Create | none | Playwright | No | Captured |
| `agent-detail-annotated.png` | Agents module | `/agents/:id` | Orient to agent configuration | n/a | Agent exists | List Tasks | Run/Disable | none | Playwright | Yes | Captured |
| `knowledge-upload-dialog.png` | Knowledge workflow | Upload dialog | Show upload control | n/a | Dialog open | empty dropzone | Cancel/Upload | none | Playwright | No | Captured |
| `run-agent-dialog.png` | Runs workflow | Run dialog | Show run submission form | n/a | Dialog open | agent select | Cancel/Run | none | Playwright | No | Captured |
| `runs-list.png` | Runs module | `/runs` | Show run list + filters | n/a | Runs exist | 7 runs | New run | none | Playwright | No | Captured |
| `outputs-library.png` | Outputs module | `/outputs` | Show output library | n/a | Outputs exist | 4 outputs | — | none | Playwright | No | Captured |
| `platform-processing.png` | Processing module | `/platform/processing` | Show processing queue | n/a | Job exists | tasks.md job | filters | none | Playwright | No | Captured |
| `application-activity.png` | Activity module | `/activity` | Show app-scoped audit log | n/a | Activity exists | Test events | filters | none | Playwright | No | Captured |
| `command-palette.png` | Navigation | `⌘K` palette | Show quick command access | n/a | Palette open | commands | — | none | Playwright | No | Captured |

## Reused Screenshots (from the README set)

`platform-dashboard.png`, `platform-applications.png`, `platform-operations.png`,
`application-knowledge.png`, `agent-detail.png`, `run-detail.png`,
`output-detail.png`, `application-chat.png` — all current, dark theme, no
sensitive content, reused as-is by the manual.

## Blocked Screenshots

None. Every planned user-manual screenshot was captured from the running app.

