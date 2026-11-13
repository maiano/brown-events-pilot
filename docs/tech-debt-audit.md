# BEVN-003 — Technical debt audit

## 1. Executive summary

The approved audit contains **22 distinct findings: 2 High, 16 Medium, 4 Low**. Each finding has one primary area so counts do not overlap; cross-layer effects are described within the finding.

| Primary area | High | Medium | Low | Total |
| --- | ---: | ---: | ---: | ---: |
| Backend | 1 | 11 | 0 | 12 |
| Frontend | 1 | 5 | 2 | 8 |
| Infrastructure | 0 | 0 | 2 | 2 |
| **Total** | **2** | **16** | **4** | **22** |

Top five priorities:

1. **TD-001:** Give each request its own DbContext; the current shared lifetime affects all database services and is inexpensive to correct.
2. **TD-015:** Replace the browser's loopback API origin so remote users reach the deployed backend.
3. **TD-010:** Define allowlisted request DTOs before the rewrite can inherit nested entity writes.
4. **TD-006:** Specify repeat-registration behavior and enforce the intended identity/uniqueness guarantees.
5. **TD-008:** Make attendee creation and registration one atomic operation.

This audit does not claim measured N+1 queries, demonstrated database corruption, reproduced deadlock, authorization bypass, credential theft, or a secret-containing image.

## 2. Scope and methodology

**Audited source revision:** `c4390a6`. **Scope and Definition of Done:** [GitHub Issue #4](https://github.com/maiano/brown-events-pilot/issues/4), read through GitHub MCP, including its Pilot requirements for blocking async calls, ORM loading/query efficiency, and CORS. This document records the approved source verification; it does not restart discovery.

| Area | Inspection coverage |
| --- | --- |
| Backend | All three controllers, four service implementations and four interfaces; all six entities; complete DbContext, seeder, and startup |
| API documentation | Swashbuckle configuration, XML comments, operation filter, both navigation schema filters |
| Tests | Both xUnit files containing seven tests; project files and solution |
| Frontend | Four pages, three components, API client, router, entry point, inline hooks, CSS and HTML |
| Infrastructure | Compose, both Dockerfiles, Vite, Nginx, package manifest and lockfile structure, legacy GitLab CI, GitHub PR template and workflow inventory |
| Context | AGENTS.md and architecture documentation; claims were verified against source rather than accepted from documentation |

Credential-bearing configuration and local credentials were excluded. Nonsecret application logging settings were inspected. Deployed schema, ingress/access controls, actual CI integration, dependency vulnerabilities, image contents, SQL counts and load behavior were not independently inspected or measured.

Verification labels mean:

- **Source confirmed:** The cited implementation or omission was checked directly. Runtime consequences remain conditional where stated.
- **Runtime verified:** A specific behavior was reproduced; the finding identifies who reported the check and its limits.
- **Unverified:** A claimed runtime effect still needs measurement. A source-confirmed mechanism can have an unverified performance or security consequence.

The human reported runtime checks for TD-006 and TD-017 when approving documentation. Codex did not execute those checks or inspect their raw artifacts. No new application execution is claimed for writing this document.

Re-verification narrowed the original candidates:

| Candidate | Decision and reason |
| --- | --- |
| TD-010 | High → Medium: graph insertion is supported; privilege escalation and arbitrary existing-record modification were not demonstrated. |
| TD-012 | Medium → Low: wildcard policy is confirmed, but credentialed access is not enabled and the demo's origin restrictions are unspecified. |
| TD-020 | Medium → Low: unsupported confirmation copy is a user expectation problem, not a demonstrated persistence failure. |
| TD-003 | Merged into TD-002: synchronous registration I/O belongs with request-path blocking. |
| TD-011 | Merged into TD-009: invalid relationship errors belong with validation/expected-error handling. |
| TD-019 | Merged into TD-018: persistent errors and stale responses share route-state lifecycle handling. |
| TD-007, TD-013, TD-027 | Removed/moved from confirmed findings; see the appendix for product, deployment and scheduled-CI decisions. |

IDs remain stable. Missing numbers do not represent missing findings.

## 3. Findings grouped by category

### Concurrency and performance

#### TD-001 — Singleton DbContext shared across requests

- **Category:** Concurrency and performance. **Area:** Backend. **Severity:** High.
- **Location/evidence:** [Program.cs:13](../backend/BrownEvents.Api/Program.cs#L13) registers `AddSingleton<AppDbContext>`; scoped services at [Program.cs:22](../backend/BrownEvents.Api/Program.cs#L22) receive it through constructors such as [ConferenceService.cs:11](../backend/BrownEvents.Api/Services/ConferenceService.cs#L11).
- **Description/impact:** Requests share an EF context and its tracker. Overlapping operations can violate EF's concurrency requirements; tracked state survives request boundaries. All database services are exposed. No concurrency exception, memory measurement or corruption was reproduced. High reflects app-wide scope and the trivial lifetime correction (`AddSingleton` → `AddScoped`), not an observed runtime failure.
- **Suggested fix:** Register a context per request/unit of work and keep consuming services scoped. Verify distinct scopes receive distinct contexts and overlapping requests operate independently.
- **Verification status:** Source confirmed — DI lifetime and injection paths checked; runtime failure remains unverified.
- **Migration impact (EXT-150):** Fixed by Prisma/async architecture — this EF context/tracker lifetime defect disappears when EF is removed; do not confuse a shared Prisma client with a shared EF tracker.

#### TD-002 — Blocking database operations on request paths

- **Category:** Concurrency and performance. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** `FindAsync(id).Result` at [ConferenceService.cs:23](../backend/BrownEvents.Api/Services/ConferenceService.cs#L23), [ConferenceService.cs:36](../backend/BrownEvents.Api/Services/ConferenceService.cs#L36), and [SessionService.cs:18](../backend/BrownEvents.Api/Services/SessionService.cs#L18). [RegistrationService.cs:19](../backend/BrownEvents.Api/Services/RegistrationService.cs#L19) uses synchronous `FirstOrDefault` inside an async method (former TD-003).
- **Description/impact:** Tracked `FindAsync` hits can complete immediately; database misses use a task-backed result in EF Core 6.0.25. `.Result` can block until I/O completes, while the attendee lookup directly performs synchronous I/O. Throughput impact requires database latency/load and is unmeasured. No `.Wait()` calls were found; deadlock was not reproduced.
- **Suggested fix:** Await lookups and use asynchronous database query APIs. Verify untracked-ID paths under controlled latency rather than relying on warm tracked fixtures.
- **Verification status:** Source confirmed — three `.Result` calls, synchronous attendee query, and the version-specific EF lookup path checked; deadlock/starvation unverified.
- **Migration impact (EXT-150):** Fixed by Prisma/async architecture — these .NET blocking APIs disappear in the awaited Prisma implementation; all new database promises must still be awaited.

#### TD-004 — Implicit navigation loading during response serialization

- **Category:** Concurrency and performance. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** [Program.cs:17](../backend/BrownEvents.Api/Program.cs#L17) enables lazy proxies; [Conference.cs:30](../backend/BrownEvents.Api/Models/Conference.cs#L30) exposes virtual collections; [ConferenceService.cs:18](../backend/BrownEvents.Api/Services/ConferenceService.cs#L18) materializes entities; [ConferencesController.cs:27](../backend/BrownEvents.Api/Controllers/ConferencesController.cs#L27) returns them directly.
- **Description/impact:** Serializing an unloaded navigation on an eligible proxy can issue more queries and traverse related graphs. Cost and response contents depend on proxy instances, loaded state and tracking history. This is a Medium query-control risk; no N+1 count or slow response was measured.
- **Suggested fix:** Specify bounded response graphs, project DTOs and load their required relationships deliberately. Preserve the agreed API fields; replacing entities with DTOs is not permission to remove existing response fields silently.
- **Verification status:** Source confirmed — proxies, navigation getters, entity-return paths and absence of service `Include`/explicit navigation `Load` checked. Actual SQL counts and scaling effects are unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — Prisma removes lazy proxies, but required relation loading, response bounds and contract compatibility need deliberate `select`/`include` and DTO design.

Actual loading paths:

| Entry path | Root query | Related graph exposed |
| --- | --- | --- |
| Conference list/detail | `ToListAsync` / `FindAsync` | Sessions, registrations |
| Conference sessions | Filtered session `ToListAsync` | Conference, speaker, room |
| Conference registrations | Filtered registration `ToListAsync` | Conference, attendee, attendee registrations |
| Session detail | `FindAsync` | Conference, speaker, room |
| Speaker list | `ToListAsync` | Sessions |

Root evidence: [ConferenceService.cs:16](../backend/BrownEvents.Api/Services/ConferenceService.cs#L16), [ConferenceService.cs:54](../backend/BrownEvents.Api/Services/ConferenceService.cs#L54), [ConferenceService.cs:69](../backend/BrownEvents.Api/Services/ConferenceService.cs#L69), [SessionService.cs:16](../backend/BrownEvents.Api/Services/SessionService.cs#L16), [SpeakerService.cs:16](../backend/BrownEvents.Api/Services/SpeakerService.cs#L16). Navigation evidence: [Conference.cs:30](../backend/BrownEvents.Api/Models/Conference.cs#L30), [Session.cs:27](../backend/BrownEvents.Api/Models/Session.cs#L27), [Registration.cs:19](../backend/BrownEvents.Api/Models/Registration.cs#L19), [Attendee.cs:16](../backend/BrownEvents.Api/Models/Attendee.cs#L16), [Speaker.cs:18](../backend/BrownEvents.Api/Models/Speaker.cs#L18).

No eager `Include`/`ThenInclude` or explicit reference/collection `Load` appears in the services. Tracking fix-up can populate relationships without an explicit load. The seeder constructs ordinary objects with `new` ([DataSeeder.cs:30](../backend/BrownEvents.Api/Data/DataSeeder.cs#L30)); the singleton context retains them, and they must not automatically be treated as lazy proxies. Repeated serialization over unloaded proxies is a potential N+1 path; warm tracking and shared related entities can conceal or reduce queries. **Runtime-verified SQL behavior: none.**

#### TD-005 — Unbounded collection materialization

- **Category:** Concurrency and performance. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** Unrestricted `ToListAsync` at [ConferenceService.cs:18](../backend/BrownEvents.Api/Services/ConferenceService.cs#L18), [ConferenceService.cs:56](../backend/BrownEvents.Api/Services/ConferenceService.cs#L56), [ConferenceService.cs:71](../backend/BrownEvents.Api/Services/ConferenceService.cs#L71), and [SpeakerService.cs:18](../backend/BrownEvents.Api/Services/SpeakerService.cs#L18).
- **Description/impact:** All matching rows are materialized and returned. Root-result memory and response size grow with data independently of TD-004. Small demo datasets may never expose material latency; current resource pressure or denial of service was not demonstrated.
- **Suggested fix:** Agree collection bounds and deterministic ordering, with a compatible pagination/versioning strategy rather than silently changing array contracts. Measure representative datasets.
- **Verification status:** Source confirmed — no bounds/pagination in these query paths; runtime scaling unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — unbounded Prisma `findMany` would preserve the limitation.

### Correctness and write boundaries

#### TD-006 — Repeat registration creates additional records

- **Category:** Correctness and write boundaries. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** [RegistrationService.cs:19](../backend/BrownEvents.Api/Services/RegistrationService.cs#L19) matches email without normalization; [RegistrationService.cs:33](../backend/BrownEvents.Api/Services/RegistrationService.cs#L33) always creates a registration. Complete [AppDbContext.cs:17](../backend/BrownEvents.Api/Data/AppDbContext.cs#L17) declares no unique email or attendee/conference constraint.
- **Description/impact:** Sequential repeats create extra registrations even when the attendee is reused. The human observed two POSTs yielding two rows. If one registration per attendee/conference is intended, retries inflate registrations; intentional repeat registrations would require a different product rule. Concurrent attendee duplication remains conditional on lookup ordering, successful saves and deployed constraints; the singleton may instead cause exceptions.
- **Suggested fix:** Define repeat-registration and normalized email identity semantics, then use suitable unique constraints and conflict/idempotency handling. Verify repeat and concurrent requests against PostgreSQL. A transaction alone does not enforce uniqueness.
- **Verification status:** Runtime verified — human reported two POSTs and two rows during review; source path confirmed. Codex did not execute/reinspect that runtime check; case/whitespace and concurrent variants remain unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — Prisma models do not infer business uniqueness or idempotency.

#### TD-008 — Registration lacks a shared atomic save boundary

- **Category:** Correctness and write boundaries. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** [RegistrationService.cs:24](../backend/BrownEvents.Api/Services/RegistrationService.cs#L24) adds/saves a new attendee; [RegistrationService.cs:41](../backend/BrownEvents.Api/Services/RegistrationService.cs#L41) separately adds/saves registration; no encompassing transaction is present.
- **Description/impact:** For a new attendee, the first save can commit before registration fails, leaving an attendee despite unsuccessful registration. Existing-attendee requests do not take that two-save path. Individual saves retain normal transaction protection; the workflow as a whole lacks atomicity. Partial persistence was not reproduced.
- **Suggested fix:** Persist the connected graph once or use a transaction spanning both writes. Inject failure at registration insertion and inspect from a fresh context/client.
- **Verification status:** Source confirmed — independent save boundaries checked; partial-save reproduction unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — use an atomic nested write or explicit Prisma transaction; separate awaited writes are not automatically atomic.

#### TD-009 — Missing domain validation and expected-error mapping

- **Category:** Correctness and write boundaries. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** [ConferencesController.cs:51](../backend/BrownEvents.Api/Controllers/ConferencesController.cs#L51) forwards entity input without a model-state check; controllers lack `[ApiController]`. [SessionService.cs:32](../backend/BrownEvents.Api/Services/SessionService.cs#L32) assigns relationship IDs before saving; [Program.cs:64](../backend/BrownEvents.Api/Program.cs#L64) onward has no application exception mapping.
- **Description/impact:** Date ordering, positive capacity, status domain and email format have no domain-level enforcement in these paths. Missing reference IDs can reach database errors instead of deliberate client responses. Exact malformed-body and constraint-error HTTP behavior is unverified; this does not establish that all malformed input persists. Former TD-011 is included here.
- **Suggested fix:** Validate dedicated request models and business invariants; map expected reference/conflict failures to contract-approved client responses. Test invalid scalar values, null/malformed bodies and nonexistent references through HTTP.
- **Verification status:** Source confirmed — input forwarding, omitted checks and failure paths inspected; actual error responses unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — NestJS DTOs/pipes and exception mapping require configuration; Prisma constraints do not supply domain validation.

#### TD-010 — Entity binding permits nested graph insertion

- **Category:** Correctness and write boundaries. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** [ConferencesController.cs:51](../backend/BrownEvents.Api/Controllers/ConferencesController.cs#L51) binds `Conference`; [ConferenceService.cs:29](../backend/BrownEvents.Api/Services/ConferenceService.cs#L29) passes it to `Add`; [Conference.cs:32](../backend/BrownEvents.Api/Models/Conference.cs#L32) exposes writable registrations. [RegistrationService.cs:24](../backend/BrownEvents.Api/Services/RegistrationService.cs#L24) similarly adds a supplied new attendee graph.
- **Description/impact:** Valid nested request entities can become part of EF graph insertion outside the intended simple operation. Triggering writes requires a graph compatible with keys, tracking and database constraints. Existing-record overwrite, privilege escalation and arbitrary database modification were not demonstrated, so High was withdrawn.
- **Suggested fix:** Allowlist scalar request fields and construct entities server-side; do not pass bound graphs directly to persistence. Verify an isolated nested payload cannot create unintended related records. Scalar validation remains TD-009.
- **Verification status:** Source confirmed — writable graph-to-`Add` path checked; a nested insertion payload was not runtime-executed.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — request spreads or unrestricted nested Prisma input could recreate the write-boundary problem.

### Security and deployment

#### TD-012 — Wildcard CORS is applied in every environment

- **Category:** Security and deployment. **Area:** Infrastructure. **Severity:** Low.
- **Location/evidence:** [Program.cs:35](../backend/BrownEvents.Api/Program.cs#L35) configures `AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`; [Program.cs:70](../backend/BrownEvents.Api/Program.cs#L70) applies it without an environment condition.
- **Description/impact:** Reachable browser requests from unrelated origins can receive broad CORS permission, subject to network/browser restrictions. Credentials are not enabled. This is a Low configuration concern for an anonymous demo with unspecified allowed origins, not demonstrated authentication bypass. Restricting CORS does not restrict direct HTTP clients or establish authorization.
- **Suggested fix:** Decide allowed deployment origins; prefer existing same-origin proxies or a small environment-configured allowlist. Test preflight and actual responses from allowed/unrelated origins after that decision.
- **Verification status:** Source confirmed — policy and unconditional middleware checked; deployed headers and cross-origin browser behavior unverified.
- **Migration impact (EXT-150):** Product or deployment question — NestJS origin settings require an agreed deployment policy; no authentication system is implied.

#### TD-014 — Personal data is logged in diagnostics

- **Category:** Security and deployment. **Area:** Backend (also affects frontend).
- **Severity:** Medium.
- **Location/evidence:** [Program.cs:18](../backend/BrownEvents.Api/Program.cs#L18) unconditionally enables sensitive EF logging; [api.js:4](../frontend/src/api.js#L4) logs request options including bodies; [RegistrationPage.jsx:29](../frontend/src/pages/RegistrationPage.jsx#L29) and [ConferenceDetailPage.jsx:110](../frontend/src/pages/ConferenceDetailPage.jsx#L110) log attendee form data.
- **Description/impact:** Normal form submission passes personal data to browser diagnostics. Relevant EF diagnostic events may also contain values, depending on log levels and sinks. External collection, unauthorized log access and actual backend disclosure were not observed. Medium reflects explicit personal-data logging, not proven exfiltration.
- **Suggested fix:** Remove browser payload logs; keep sensitive database diagnostics disabled outside controlled development use. Check synthetic attendee values against configured diagnostics.
- **Verification status:** Source confirmed — logging calls/configuration inspected; actual output and external retention unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — remove frontend payload logs and ensure replacement backend logging does not preserve sensitive-value exposure.

#### TD-015 — Browser requests use a hardcoded loopback API origin

- **Category:** Security and deployment. **Area:** Frontend. **Severity:** High.
- **Location/evidence:** [api.js:1](../frontend/src/api.js#L1) sets `BASE_URL = 'http://localhost:5000'`; [api.js:5](../frontend/src/api.js#L5) uses it for every helper. [nginx.conf:13](../frontend/nginx.conf#L13) and [vite.config.js:9](../frontend/vite.config.js#L9) provide bypassed `/api` proxies.
- **Description/impact:** A remote visitor's browser addresses its own machine, not the deployed backend. Without a matching local backend, every API-backed feature fails. Local development can work, so High applies to ordinary remote deployment usability. No browser-specific mixed-content claim is needed.
- **Suggested fix:** Use relative `/api` requests through existing proxies, or an explicitly configured API origin. Verify request destinations from another machine.
- **Verification status:** Source confirmed — shared client and proxies checked; second-machine — unverified.
- **Migration impact (EXT-150):** Frontend-only — replacing the backend does not alter the browser's hardcoded destination.

### Frontend registration and route state

#### TD-016 — Registration submits fields outside the API contract

- **Category:** Frontend registration and route state. **Area:** Frontend (integration).
- **Severity:** Medium.
- **Location/evidence:** [ConferenceDetailPage.jsx:112](../frontend/src/pages/ConferenceDetailPage.jsx#L112) spreads form data and adds `sessionId`; [RegistrationPage.jsx:13](../frontend/src/pages/RegistrationPage.jsx#L13) includes phone/company. [ConferencesController.cs:117](../backend/BrownEvents.Api/Controllers/ConferencesController.cs#L117) binds [Attendee.cs:4](../backend/BrownEvents.Api/Models/Attendee.cs#L4), which has none of those fields; [Registration.cs:14](../backend/BrownEvents.Api/Models/Registration.cs#L14) has no session relationship.
- **Description/impact:** Unknown JSON fields are ignored under the configured defaults and cannot be persisted through this model. Phone/company inputs therefore collect information the application drops. Session selection does not itself prove a promise of session enrollment: the modal explicitly registers for a conference.
- **Suggested fix:** Align forms and DTOs with the existing conference-level contract; remove unsupported inputs/payload fields. Add persistence only if separately approved product scope requires it. Verify submit/reload behavior.
- **Verification status:** Source confirmed — payload, binding, serializer configuration and persisted fields traced; no field-persistence runtime test claimed.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — coordinate frontend payloads and NestJS DTOs rather than silently accepting dropped fields or inventing session registration.

#### TD-017 — Modal success leaves the registration list stale

- **Category:** Frontend registration and route state. **Area:** Frontend. **Severity:** Medium.
- **Location/evidence:** The registrations effect at [ConferenceDetailPage.jsx:68](../frontend/src/pages/ConferenceDetailPage.jsx#L68) depends only on `id` (line 80); success at [ConferenceDetailPage.jsx:116](../frontend/src/pages/ConferenceDetailPage.jsx#L116) updates success/loading state without updating/refetching registrations.
- **Description/impact:** After the initial list fetch settles, a successful modal registration leaves the displayed list unchanged. The human reproduced this in the browser. An already-pending initial fetch might happen to include the row. Confusing stale state can encourage retries, but that consequence was not measured.
- **Suggested fix:** Refetch registrations after success or incorporate the returned record into state. Verify the list after registration and modal close without a reload.
- **Verification status:** Runtime verified — stale list after modal, no reload; source path confirmed. Codex did not rerun the browser check.
- **Migration impact (EXT-150):** Frontend-only — mutation-driven state refresh remains necessary with either backend.

#### TD-018 — Route-dependent asynchronous state is not isolated

- **Category:** Frontend registration and route state. **Area:** Frontend. **Severity:** Medium.
- **Location/evidence:** [ConferenceDetailPage.jsx:37](../frontend/src/pages/ConferenceDetailPage.jsx#L37), line 54 and line 68 issue fetches without obsolete-response guards. [SessionDetailPage.jsx:11](../frontend/src/pages/SessionDetailPage.jsx#L11) starts loading without clearing `error`; line 43 renders the error before details.
- **Description/impact:** When route parameters change without remounting, old responses can overwrite current data if they finish last. A failed session request followed by a successful one can retain its old error. Selection/modal state also survives conference changes. The response-order race requires suitable navigation/timing and was not browser-reproduced. Former TD-019 is included here.
- **Suggested fix:** Cancel or ignore superseded requests, clear errors on new requests, and reset route-owned selection/modal state. Test reversed response order and failure-to-success navigation.
- **Verification status:** Source confirmed — missing guards and reset paths checked; browser race/error-transition behavior unverified.
- **Migration impact (EXT-150):** Frontend-only — effect lifecycle handling is independent of backend technology.

#### TD-020 — Success UI promises an unimplemented email

- **Category:** Frontend registration and route state. **Area:** Frontend. **Severity:** Low.
- **Location/evidence:** [RegistrationPage.jsx:47](../frontend/src/pages/RegistrationPage.jsx#L47) and [ConferenceDetailPage.jsx:303](../frontend/src/pages/ConferenceDetailPage.jsx#L303) promise confirmation delivery; the complete flow at [RegistrationService.cs:15](../backend/BrownEvents.Api/Services/RegistrationService.cs#L15) only saves data.
- **Description/impact:** Successful users expect an email that this repository does not send. An external sender was not established or ruled out. Low reflects misleading copy rather than persistence failure.
- **Suggested fix:** State that registration was saved; retain an email promise only if a verified external delivery mechanism exists. Do not add email dependencies for this audit.
- **Verification status:** Source confirmed — success copy and registration dependencies inspected; external delivery unknown.
- **Migration impact (EXT-150):** Frontend-only — truthful copy does not require backend migration.

#### TD-021 — Session route does not validate conference ownership

- **Category:** Frontend registration and route state. **Area:** Frontend. **Severity:** Medium.
- **Location/evidence:** [SessionDetailPage.jsx:13](../frontend/src/pages/SessionDetailPage.jsx#L13) fetches only by `sessionId`; [SessionDetailPage.jsx:107](../frontend/src/pages/SessionDetailPage.jsx#L107) links registration using unchecked route `id`.
- **Description/impact:** A mismatched conference/session URL can display a session from conference B but offer registration for A. This needs a manually entered or otherwise incorrect URL; no ownership/access-control bypass is claimed.
- **Suggested fix:** Compare returned `conferenceId` with the route, then reject or canonicalize mismatches. Verify with known IDs from different conferences.
- **Verification status:** Source confirmed — lookup and link construction checked; mismatched-route browser behavior unverified.
- **Migration impact (EXT-150):** Frontend-only — route consistency can be checked using the existing response field.

#### TD-022 — Session and registration interactions have accessibility gaps

- **Category:** Frontend registration and route state. **Area:** Frontend. **Severity:** Medium.
- **Location/evidence:** [SessionCard.jsx:11](../frontend/src/components/SessionCard.jsx#L11) uses a click-only `div`; [ConferenceDetailPage.jsx:294](../frontend/src/pages/ConferenceDetailPage.jsx#L294) lacks dialog semantics/focus management; [RegistrationPage.jsx:85](../frontend/src/pages/RegistrationPage.jsx#L85) uses an unassociated label/input pair.
- **Description/impact:** Keyboard session selection and assistive-technology operation of forms/dialogs are incomplete. Existing detail links remain keyboard-accessible; the entire application is not claimed to be unusable. Medium reflects barriers in a core registration journey.
- **Suggested fix:** Use native interactive controls, associated labels, a named dialog and focus management/keyboard dismissal. Verify keyboard-only completion and accessible names.
- **Verification status:** Source confirmed — markup and handlers checked; screen-reader and keyboard walkthroughs unverified.
- **Migration impact (EXT-150):** Frontend-only — backend replacement does not change interaction semantics.

#### TD-023 — Exported registration delete helper has no endpoint

- **Category:** Frontend registration and route state. **Area:** Frontend. **Severity:** Low.
- **Location/evidence:** [api.js:36](../frontend/src/api.js#L36) exports a DELETE helper; complete controller routing, including [ConferencesController.cs:7](../backend/BrownEvents.Api/Controllers/ConferencesController.cs#L7), defines no matching action.
- **Description/impact:** Invoking the helper would target an unsupported route. No current caller was found, so present user impact is limited.
- **Suggested fix:** Remove the unused export unless deletion becomes approved scope. Compare callers and OpenAPI rather than inventing an endpoint during migration.
- **Verification status:** Source confirmed — helper, callers, all controllers and startup routing checked; no HTTP DELETE execution claimed.
- **Migration impact (EXT-150):** Frontend-only — remove the misleading client surface while preserving the actual backend contract.

### Configuration and maintainability

#### TD-024 — Tests omit high-risk execution paths

- **Category:** Configuration and maintainability. **Area:** Backend (also frontend coverage).
- **Severity:** Medium.
- **Location/evidence:** [ConferenceServiceTests.cs:14](../backend/BrownEvents.Tests/ConferenceServiceTests.cs#L14) and [SessionServiceTests.cs:14](../backend/BrownEvents.Tests/SessionServiceTests.cs#L14) use InMemory; [frontend/package.json:6](../frontend/package.json#L6) contains no test script/runner.
- **Description/impact:** Seven direct service tests omit registration, speakers, HTTP binding/serialization, DI concurrency, PostgreSQL constraints and frontend journeys. Ordinary tracked test entities also differ from runtime proxy behavior. Coverage gaps allow migration regressions to escape; existing test failures are not asserted.
- **Suggested fix:** Establish HTTP contract checks and targeted PostgreSQL registration/constraint cases before rewriting; add focused frontend registration/navigation checks. Verify persistence using fresh contexts/clients.
- **Verification status:** Source confirmed — test fixtures/assertions and scripts inventoried; no new suite execution performed for this audit.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — preserve contract fixtures and meaningful failure-path coverage when replacing the test stack.

#### TD-025 — Startup creation has no versioned schema upgrade path

- **Category:** Configuration and maintainability. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** [Program.cs:77](../backend/BrownEvents.Api/Program.cs#L77) calls `EnsureCreated()`; no tracked EF migrations were found.
- **Description/impact:** Empty-database creation does not define upgrades for existing databases when models change. This becomes relevant on schema-changing deployment; no current schema mismatch was observed.
- **Suggested fix:** Replace startup `EnsureCreated` with the target stack's versioned migration/baseline workflow and explicit deployment ownership. Verify both empty and existing-schema installation paths in disposable environments.
- **Verification status:** Source confirmed — startup API and migration inventory checked; upgrade behavior unverified.
- **Migration impact (EXT-150):** Fixed by Prisma/async architecture — the specific EF `EnsureCreated` path disappears. This bucket assumes the target uses Prisma's versioned migration workflow; Prisma Client alone does not automatically upgrade existing schemas or choose a data baseline.

#### TD-026 — Seeder can leave persistent partial datasets

- **Category:** Configuration and maintainability. **Area:** Backend. **Severity:** Medium.
- **Location/evidence:** [DataSeeder.cs:9](../backend/BrownEvents.Api/Data/DataSeeder.cs#L9) returns if any conference exists; independent saves occur at lines 18, 28, 71 and 160.
- **Description/impact:** If startup fails before conference save, restarting can duplicate already-saved speakers/rooms. If it fails after conference save, the guard skips missing sessions on restart. Normal uninterrupted startup need not expose either issue; interruption cases were not reproduced.
- **Suggested fix:** Make demo seeding atomic and retry-safe with a deliberate environment policy. Inject failures at save boundaries and verify restart completeness/idempotency.
- **Verification status:** Source confirmed — save sequence and restart guard inspected; failure injection unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — port demo data with a transaction/idempotency strategy rather than copying the guard.

#### TD-028 — Docker build contexts lack tracked exclusions

- **Category:** Configuration and maintainability. **Area:** Infrastructure. **Severity:** Low.
- **Location/evidence:** [frontend/Dockerfile:6](../frontend/Dockerfile#L6) and [backend Dockerfile:11](../backend/BrownEvents.Api/Dockerfile#L11) use `COPY . .`; no tracked `.dockerignore` exists in either context.
- **Description/impact:** Developer dependencies/artifacts can enter build stages, and frontend host dependencies can overlay container-installed ones. Inclusion requires those files to exist in the context. No secret-containing context or image was observed; the final frontend image only copies built output and Nginx configuration.
- **Suggested fix:** Add context-specific exclusions for local dependencies, artifacts and environment files. Compare clean/populated workspace builds without inspecting real secrets.
- **Verification status:** Source confirmed — broad copy and tracked-file inventory checked; actual context/image contents unverified.
- **Migration impact (EXT-150):** Must be explicitly addressed during migration — apply context hygiene when preparing the replacement backend image; the frontend context also needs its own exclusions.

## 4. Migration impact (EXT-150)

Each of the 22 findings belongs to exactly one bucket. Mixed-layer findings use the bucket that captures the required coordinated work, not an additional count. Removal of legacy APIs fixes only their specific mechanism, not all analogous mistakes in the target stack.

| Bucket | Count | Findings |
| --- | ---: | --- |
| Fixed by Prisma/async architecture | 3 | TD-001, TD-002, TD-025 |
| Must be explicitly addressed during migration | 11 | TD-004, TD-005, TD-006, TD-008, TD-009, TD-010, TD-014, TD-016, TD-024, TD-026, TD-028 |
| Frontend-only | 7 | TD-015, TD-017, TD-018, TD-020, TD-021, TD-022, TD-023 |
| Product or deployment question | 1 | TD-012 |
| **Total** | **22** | |

The migration specification should carry explicit decisions for response graphs/bounds, duplicate handling, normalized identity, registration atomicity, DTO validation, logging, payload alignment, tests, seeding and image contexts. TD-025's versioned migration/baseline workflow is part of the target architecture, not a promise that installing Prisma fixes schema deployment. Removed capacity/access candidates remain questions in the appendix and are excluded from these counts.

## 5. Prioritized remediation recommendations

These are recommendations for subsequent authorized tasks, not changes implemented by BEVN-003. Timing accounts for migration risk and fix cost as well as severity; task order and issue scope still govern execution.

### Fix before migration

| Finding | Timing justification |
| --- | --- |
| TD-001 | A trivial lifetime correction removes app-wide instability that could make baseline contract observations unreliable. |
| TD-015 | A small client-origin change makes remote review and backend comparison possible through the existing proxy. |
| TD-017 | A small refresh correction removes a human-reproduced stale registration view and reduces confusing baseline retests. |
| TD-024 | Capture representative contract and registration behavior before replacing the implementation; retrofitting that evidence later costs more. |

### Fix during migration

| Finding | Timing justification |
| --- | --- |
| TD-002 | Awaited Prisma calls replace legacy blocking APIs; avoid spending migration effort restructuring .NET methods first unless load demands it. |
| TD-004 | Specify explicit relation projections while designing Prisma queries and contract DTOs. |
| TD-005 | Agree collection bounds during endpoint design; preserve current contracts until a compatible change is approved. |
| TD-006 | Decide retry/identity semantics before defining constraints and conflict responses in the new schema. |
| TD-008 | Design the new registration write as one atomic operation rather than porting two independent saves. |
| TD-009 | Configure target validation and error translation together with the endpoint contracts. |
| TD-010 | Request allowlists are inexpensive when DTOs are introduced and prevent copying broad persistence inputs. |
| TD-014 | Remove existing payload logs and establish safe replacement logging while tracing the new request flow. |
| TD-016 | Coordinate DTOs and form payloads while verifying frontend compatibility; retain conference-level registration scope. |
| TD-025 | Establish migration/baseline deployment when introducing Prisma instead of building a short-lived EF migration system. |
| TD-026 | Rebuild demo seeding atomically and idempotently when porting the seed dataset. |

### Defer

| Finding | Timing justification |
| --- | --- |
| TD-012 | Choose allowed origins with the deployment model; wildcard CORS alone does not establish an urgent authentication defect. |
| TD-018 | Add focused route-state regression coverage and guards as frontend stabilization; this does not block the backend schema rewrite. |
| TD-020 | Correct success copy as a small UI follow-up; do not expand the migration into email delivery. |
| TD-021 | Resolve mismatched deep-link context in frontend stabilization; normal generated links already supply matching IDs. |
| TD-022 | Schedule the documented keyboard/form/dialog work explicitly; it is important but independent of migration architecture. |
| TD-023 | Remove an unused misleading export during client cleanup; no active UI path depends on it. |
| TD-028 | Apply low-cost context hygiene when image configuration is next touched; no actual leak or failing build was established. |

## 6. Unknowns and runtime verification needs

Human review evidence is limited to the checks explicitly reported when approving this document:

| Finding | Human review: reproduced / not reproduced |
| --- | --- |
| TD-006 | **Runtime verified:** two POSTs, two rows observed. Concurrent identity creation, normalization variants and deployed uniqueness constraints were not established by that check. |
| TD-015 | **Source confirmed:** second-machine test not executed. |
| TD-002 / former TD-003 | **Source confirmed:** deadlock not reproduced; no load/latency measurement supplied. |
| TD-017 | **Runtime verified in browser:** list remained stale after modal registration. Codex did not rerun the check. |

Further verification priorities:

| Findings | Needed evidence | What could change the assessment |
| --- | --- | --- |
| TD-001 | Overlapping PostgreSQL requests, request-scope identity checks, tracked-state lifetime observations | Frequent failures could increase urgency; absence of one observed failure would not make shared EF context usage valid. |
| TD-002 | Untracked lookups under latency and request-thread/load measurements | Demonstrated widespread starvation would support higher operational severity; no deadlock is currently asserted. |
| TD-004, TD-005 | Command counts through full serialization; cold/warm contexts; increasing root counts; payload size/latency/memory | Repeated per-entity SQL and material resource cost could increase severity; bounded observed graphs could narrow the performance concern. Capture synthetic data without sensitive parameter values. |
| TD-006 | Repeat policy, deployed constraints, case/whitespace variants and concurrent requests after isolating the context defect | Intentional repeat registrations could remove the uniqueness defect; confirmed damaging duplication could raise priority/severity. |
| TD-008, TD-026 | Controlled failure after earlier save, fresh-client inspection and restart checks | Persisted partial workflows would establish actual failure outcomes; severity depends on cleanup/business cost. |
| TD-009, TD-010 | Invalid HTTP input and small nested-graph payloads in a disposable database | Measured crashes or writes across an actual restricted boundary could raise severity; current evidence does not establish those outcomes. |
| TD-012, TD-014 | Intended origins, deployed headers, log levels/sinks and synthetic diagnostic output | Required origin restrictions or externally retained personal data could raise security significance; broad policy/logging alone does not prove exploitation. |
| TD-015 | Access from a second machine with no local backend | Would reproduce the predicted deployment failure; a deliberately local-only deployment would narrow its scope. |
| TD-016, TD-018, TD-021, TD-022 | Submit/reload, reordered route responses, mismatched URLs, keyboard and accessibility checks | Establish actual user-facing consequences; these are source-supported paths, not completed browser tests. |
| TD-020, TD-024, TD-025, TD-028 | External email mechanism, executable coverage, existing-schema deployment and clean/populated image contexts | External evidence could narrow or remove the corresponding assumption; none was established during review. |

Deployed access controls, schema drift, CI integrations and production-scale workloads remain unknown. This documentation session did not run application tests/builds, start services, or perform database writes. Documentation checks and diff review are recorded in the devlog; they do not substitute for runtime verification.

## 7. Appendix: Candidates removed from confirmed findings

| Candidate | Disposition |
| --- | --- |
| TD-013 — Public access boundary | Moved to a product/deployment question: application authorization is absent, but required restrictions, real-data exposure and external ingress controls are unknown; previous High withdrawn. |
| TD-007 — Capacity enforcement | Moved to a product question: `MaxAttendees` is ignored by registration, but hard-limit/waitlist semantics must be agreed before calling that a confirmed defect. |
| TD-027 — GitHub CI absence | Moved to scheduled EXT-110 work: no tracked Actions workflow does not prove external CI is absent or failing; retain legacy CI until its authorized replacement. |

These candidates are excluded from the 22 finding totals, migration bucket counts and remediation tiers. No new authentication, session enrollment, email service or individual GitHub issues are implied by this audit.
