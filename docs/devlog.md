# Development Log

## EXT-110 — Revive CI on GitHub Actions

**Date:** 2026-11-13
**Branch:** `EXT-110-revive-ci`
**Issue:** [#5](https://github.com/maiano/brown-events-pilot/issues/5)

### Approach

Analyzed the existing GitLab pipeline before migrating the useful jobs to
GitHub Actions. Selected two independent jobs: backend and frontend, triggered
on pull requests and pushes to `main`, with read-only repository permissions.
The legacy `.gitlab-ci.yml` was removed after confirming that the GitHub
workflow covers both build and test for the current stack.

### Key decisions

- Combined backend restore, Release build, and xUnit tests in one job.
- Preserved frontend `npm ci` and Vite production build in a separate job.
- Dropped Docker publishing — no container registry is configured for this fork.
- Kept the .NET 6 application unchanged; CI installs SDK 6.0.428 explicitly.
- The workflow generates a temporary `global.json` before restore to pin .NET SDK 6.0.428 the runner image ships with newer SDKs that would otherwise be selected.
- Used Ubuntu 22.04 and Node 22; the seven service tests use EF InMemory, so CI needs no PostgreSQL or Compose services.
- Set workflow permissions to `contents: read`.

### Human review

I approved the migration analysis and the exact workflow YAML before the agent
implemented anything. I rejected an earlier proposal to add Docker image
publishing (no registry exists) and an optional dependency cache (not worth
the complexity for a two-job workflow). I verified that the workflow does not
touch application code or dependencies, and that `global.json` is used only to
pin the SDK selection, not to change the application target.

### Validation

- **Workflow lint:** actionlint 1.7.12 — passed; whitespace checks — passed.
- **Diff scope:** reviewed; only `.github/workflows/ci.yml`, `global.json`, and
  removal of `.gitlab-ci.yml`.
- **Backend (disposable .NET 6.0.428 container):** restore and Release build
  succeeded; 7/7 tests passed with the workflow's reuse flags.
- **Frontend (Node 22.23.1 / npm 10.9.8):** `npm ci` and production build
  passed.
- **GitHub Actions PR run:** [✅ both jobs passed](https://github.com/maiano/brown-events-pilot/actions/runs/43510170696). Backend: restore, Release build, 7/7 tests. Frontend: `npm ci`,
  production build. Main-push triggering verified statically only.
- **Observed warnings (pre-existing, not introduced):** 54 backend warnings
  (CS1591 and EF assembly conflict); npm reported 9 vulnerabilities (4
  moderate, 5 high). Neither was addressed in this task.

### Lessons learned

Installing a legacy SDK does not guarantee that it is selected — a CI-only
`global.json` is the reliable way to pin .NET 6 when the runner image ships
newer SDKs. Reusing the solution build when running tests (`--no-build` with
`--no-restore`) keeps the job fast and avoids duplicate compilation. Separating backend and frontend into independent jobs gives clearer failure signals than the legacy three-stage GitLab pipeline (build → test → deploy) did for this two-job verification scope.

### Limitations

The backend remains on unsupported .NET 6; the workflow pins SDK 6.0.428
accordingly. The workflow will need to be updated for NestJS during EXT-150
(different runtime, different test runner, possibly Prisma migrations). The
legacy GitLab pipeline was removed, but if any external mirror still uses it,
that mirror will need its own replacement.

## BEVN-003 — Technical Debt Audit

**Date:** 2026-11-13
**Branch:** `BEVN-003-technical-debt-audit`
**Issue:** [#4](https://github.com/maiano/brown-events-pilot/issues/4)

- **Approach:** I separated read-only discovery, source re-verification, and
  approved documentation. The audit records revision `c4390a6`, covers
  backend/frontend/infrastructure, and explicitly traces blocking async calls,
  EF navigation loading, and CORS. Existing architecture notes were context,
  not proof.
- **Decisions and corrections:** We retained 22 distinct findings (2 High,
  16 Medium, 4 Low), merged overlapping candidates, downgraded unsupported
  security impact, and separated capacity/access-policy questions and
  scheduled CI work from confirmed defects. Each finding has one primary area
  and one migration-impact bucket, with practical before/during/deferred
  recommendations.
- **My verification:** I required exact source evidence and conditional
  failure mechanisms before approving the audit. I reproduced TD-006 with two
  POSTs against the same email and observed two registration rows. I
  reproduced TD-017 in the browser: after modal registration succeeded, the
  registrations list remained stale without a reload. I confirmed TD-001
  (`AddSingleton<AppDbContext>`) and TD-015 (`localhost:5000`) by source only;
  I did not run concurrent load or test from a second machine. I confirmed
  TD-002/003 (`.Result`, synchronous `FirstOrDefault`) by source only; I did
  not reproduce a deadlock. TD-013 I reviewed and agreed to move to a
  deployment-policy question rather than a confirmed defect.
- **Validation:** Re-read Issue #4 through GitHub MCP; checked
  finding/severity/area/bucket totals, required fields, local source-link
  targets and line references, remediation coverage, and whitespace/diff
  scope. Only the audit and this devlog changed; no application tests or
  builds were run for documentation.
- **Limitations:** SQL/N+1 counts, concurrency exceptions, partial-save
  failure injection, deployed access/logging/schema details, and load effects
  remain unverified. No commit, push, PR, or issue changes were performed;
  the written result awaits human review.
- **Lesson:** Separate source mechanisms from measured effects, and
  distinguish automatic removal of legacy APIs from behavior the Prisma
  rewrite must explicitly preserve or correct.

## BEVN-002 — API Documentation

**Date:** 2026-11-12
**Branch:** `BEVN-002-api-documentation`
**Issue:** [#3](https://github.com/maiano/brown-events-pilot/issues/3)

### Approach

The goal was to make the existing ASP.NET Core API understandable and usable through Swagger UI without changing runtime behavior.

I followed an analysis → implementation → independent review → verification workflow. The agent was required to inspect the existing API contract and propose a plan before implementation.

### Implementation

Codex extended the existing Swashbuckle configuration:

- Enabled XML documentation and documented all 12 endpoints across seven paths.
- Added descriptions for the six entity models and their properties.
- Documented request parameters, response schemas, existing 200/404 status codes, and six request-body examples.
- Added an OpenAPI operation filter for anonymous `{ data: ... }` response envelopes and examples.
- Documented existing behavior, including conference-level registration and serialization limitations.

No new dependencies, API routes, authentication, validation rules, or business logic were introduced.

### Independent review and human intervention

A fresh Codex review identified one MAJOR issue: generated examples contained `null` elements in navigation collections, but the OpenAPI schemas rejected them.

I investigated the proposed fix and required validation of both `null` and non-null entity objects to avoid overlapping `oneOf` matches.

The agent implemented `NullableNavigationItemsSchemaFilter` for five affected collections, preserving the original referenced entity schemas.

Subsequent validation exposed another issue: five singular navigation properties used nullable `allOf` wrappers that rejected `null`. The failing examples specifically involved `Session.speaker` and `Session.room`.

I requested a targeted follow-up correction rather than accepting partially valid documentation.

The final solution uses an OpenAPI 3.0-compatible null-only branch:

```yaml
oneOf:
  - $ref: '#/components/schemas/Speaker'
  - type: object
    nullable: true
    enum: [null]
```

A second schema filter corrects the five singular references. Both fixes affect only the generated OpenAPI document, not runtime serialization.

### Final verification

The following checks passed against the freshly generated specification:

| Check | Result |
| --- | --- |
| Docker backend build | PASS |
| OpenAPI 3.0 document validation | PASS |
| Redocly CLI 2.58.2 | 0 errors, 25 warnings |
| Response example validation | 33/33 |
| Request example validation | 18/18 |
| Nullable navigation schema tests | PASS |
| API operation preservation | 12/12 |
| Existing xUnit tests | 7/7 passed |
| Swagger UI browser verification | PASS |
| `git diff --check` | PASS |

The schema checks used `openapi-schema-validator 0.6.3` and `openapi-spec-validator 0.7.2`. They covered valid entities, null values, mixed collections, and invalid input types.

Swagger UI was verified with headless Chrome. All 12 operations expanded successfully without browser or network errors.

### Known limitations

- Redocly reports 25 non-blocking warnings: one missing server declaration, 12 missing `operationId` fields, and 12 missing security declarations.
- XML documentation generation exposes 53 CS1591 warnings for public members outside the documented scope.
- The existing EF Core Relational assembly-version conflict warning remains.
- Runtime HTTP write scenarios were not executed against PostgreSQL. Validation covered generated contracts, examples, Swagger UI, and existing service tests.
- EF Core lazy-loading proxies may produce additional serialization details and cycle-dependent response graphs not fully represented by the declared entity schemas.

These limitations were not addressed because they do not prevent completion of the current documentation task.

## BEVN-001 — Codebase Mapping

**Date:** 2026-11-10
**Branch:** `BEVN-001-codebase-map`

### Approach

Mapped the existing BrownEvents architecture as a prerequisite for the Track B migration.

I deliberately separated analysis and documentation into two stages: understanding and verifying the source code first, then writing the architecture document.

### Session 1 — Analysis and verification

Started Codex with `BEVN-001:` and instructed it to read `AGENTS.md` and Issue #2, inspect the existing backend and frontend, propose the architecture document structure, and wait for approval before writing.

The agent produced:

- Layer responsibilities and entity relationships.
- Four HTTP endpoint traces.
- End-to-end registration walkthrough.
- Existing test coverage inventory.
- Unknowns and assumptions.
- Proposed 10-section document structure.

No files were modified during this session.

### Human verification

Before approving documentation, I asked the agent to substantiate six high-risk findings with exact source references:

| Finding | Verification |
| --- | --- |
| Singleton `AppDbContext` | Confirmed in `Program.cs:11–18` |
| Three blocking `.Result` calls | Confirmed in `ConferenceService.cs` and `SessionService.cs` |
| `EnsureCreated()` instead of migrations | Confirmed in `Program.cs:54–60`; no migration files found |
| Modal `sessionId` ignored by backend | Confirmed through frontend payload, `Attendee` model, and backend binding configuration |
| Missing DELETE registration endpoint | Checked all three controllers |
| Missing authentication/authorization | Checked startup configuration and controller declarations |

I independently verified four critical findings using `rg` and source inspection.

I also required the agent to:

- Support architectural claims with file references.
- Document both covered and uncovered test scenarios.
- Distinguish static-analysis findings from runtime-verified behavior.
- Preserve unknowns rather than present assumptions as facts.

### Key architectural discoveries

The main architectural risks identified were:

- A singleton EF Core `AppDbContext` shared across scoped services.
- Synchronous blocking of asynchronous database operations.
- Registration persistence split across two `SaveChangesAsync()` calls without a shared explicit transaction.
- A frontend/backend mismatch: the modal sends `sessionId`, but the backend does not persist session-level registration.
- No direct tests of `RegistrationService` in the existing seven-test suite.

These findings were documented, not fixed. Remediation is outside BEVN-001 scope.

### Validation and limitations

This task used static source analysis. Application builds, tests, and runtime API verification were not performed.

Findings requiring runtime verification are explicitly distinguished from confirmed source-code behavior in `docs/architecture.md`.

### Lessons learned

The agent's initial analysis was useful, but its conclusions required independent verification. Requesting exact code snippets and checking critical claims myself helped distinguish actual implementation behavior from plausible architectural assumptions.

### Outcome

A second Codex session (high reasoning, fresh context) re-reviewed the document
against issue #2. Findings: no blockers, no major issues; one minor devlog
placeholder (this entry). Verified: ER relationships, three GET traces,
registration persistence, negative claims (no auth, no DELETE, no migrations),
test coverage, and document structure (204 links, anchors, Mermaid syntax).
Unverified and left as stated: runtime query counts, concurrency outcomes,
exception responses.

## EXT-100 — Import Track B as GitHub Issues

Date: 2026-11-09. Branch: `EXT-100-import-issues`.

- I chose **Track B** because it moves the backend from ASP.NET Core to NestJS
while preserving the React frontend and the existing API contract — putting the
whole application on TypeScript, which matches my current stack.
- Read `AGENTS.md`, the assignment README, EXT-100, and every task in the complete Track B route. No other task was implemented.

### GitHub MCP setup

Configured the GitHub MCP server for Codex CLI as follows:

- Created a **fine-grained PAT** scoped to `maiano/brown-events-pilot` with
  `Issues: Read and Write`, `Contents: Read`, `Metadata: Read`. Public
  repositories (like `rolling-scopes-school/ai-native`) are readable by default
  under "Only select repositories", so no explicit grant was needed there.
- Stored the token in `.env`, which is in `.gitignore`. Loading is automatic via
  `direnv`: `.envrc` contains only `dotenv_if_exists .env`, so it is safe to
  commit.
- In `~/.codex/config.toml` the GitHub MCP server is configured with
  `bearer_token_env_var = "GITHUB_PAT_TOKEN"` — the config references the
  variable name, never the token itself, and lives outside the repo.
- Verified with `codex mcp list` (`Auth: Bearer token`, env var
  `GITHUB_PAT_TOKEN`) and `/mcp` inside the session (`github: connected`,
  non-zero tools).

The agent never read `.env`, the MCP config, or the token value. The token was
supplied by the shell environment, not pasted into the session.

### Troubleshooting: MCP failed inside the sandbox

The first launch with `--sandbox workspace-write` showed `github: failed
(0 tools)` in `/mcp`, even though `printenv GITHUB_PAT_TOKEN` was set and
`codex mcp list` reported `Auth: Bearer token`.

Root cause: `workspace-write` blocks outbound network by default. The MCP server
could not reach `api.githubcopilot.com`, and Codex surfaced the failure as an
MCP load error rather than a network error. Confirmed with a `curl` probe inside
the same sandbox profile — without network access the request did not resolve;
with network enabled it returned HTTP 200.

Fix: launch Codex with

    codex --sandbox workspace-write --ask-for-approval on-request \
      -c sandbox_workspace_write.network_access=true

### Imported route

Source: [assignment README](https://github.com/rolling-scopes-school/ai-native/blob/81437a21bd801ad32d6990020c662a6e3a87cdea/test-task/README.md), revision `81437a21bd801ad32d6990020c662a6e3a87cdea`. Task files were read from the same revision. Each issue body is the complete, verbatim task Markdown, including all applicable Pilot additions and original relative links.

GitHub MCP tools used:

- `get_me` — confirmed authenticated account (`maiano`).
- `get_file_contents` — read the README and every Track B task file at the
  pinned revision.
- `search_issues` + `list_issues` — checked for existing open and closed issues
  before creating anything; both returned zero.
- `issue_write` — created 11 issues sequentially in route order.
- `list_issues` — post-write read to verify stored titles, bodies, ordering, and
  the `optional` label on BEVN-101.

| Order | Task | Issue |
| --- | --- | --- |
| 1 | EXT-100 | [#1](https://github.com/maiano/brown-events-pilot/issues/1) |
| 2 | BEVN-001 | [#2](https://github.com/maiano/brown-events-pilot/issues/2) |
| 3 | BEVN-002 | [#3](https://github.com/maiano/brown-events-pilot/issues/3) |
| 4 | BEVN-003 | [#4](https://github.com/maiano/brown-events-pilot/issues/4) |
| 5 | EXT-110 | [#5](https://github.com/maiano/brown-events-pilot/issues/5) |
| Optional, before EXT-150 | BEVN-101 | [#6](https://github.com/maiano/brown-events-pilot/issues/6) |
| 6 | EXT-150 | [#7](https://github.com/maiano/brown-events-pilot/issues/7) |
| 7 | BEVN-115 | [#8](https://github.com/maiano/brown-events-pilot/issues/8) |
| 8 | BEVN-202 | [#9](https://github.com/maiano/brown-events-pilot/issues/9) |
| 9 | BEVN-203 | [#10](https://github.com/maiano/brown-events-pilot/issues/10) |
| 10 | BEVN-205 | [#11](https://github.com/maiano/brown-events-pilot/issues/11) |

### Validation and review status

- Reviewed the `issue_write` payload for the first issue before approving:
  `owner`/`repo` must be `maiano/brown-events-pilot` and not upstream, `title`
  must be `<ID> — <Task name>` with an em dash, and `body` must be complete
  without truncation.
- Approved each `issue_write` individually rather than auto-approving the batch,
  so a bad payload on any of the 11 would be caught before the rest were
  created.
- After creation, opened #1, #6 (label check), and #11 (last in route) in the
  browser to confirm rendering: full body present, correct title format,
  `optional` label on #6.

### Status at end of task

- 11 open issues, matching the Track B route in order.
- All titles and bodies verified against the pinned source revision.
- No commits, pushes, PRs, merges, or issue closures were performed by the
  agent during this task.
- The EXT-100 PR (opened from `EXT-100-import-issues`) references `Closes #1`,
  states Track B, and includes the rationale above.

### Notes

- Relative links inside issue bodies (`../README.md`, `../side-quests/...`)
  point back to the source repository and are preserved verbatim per DoD.
