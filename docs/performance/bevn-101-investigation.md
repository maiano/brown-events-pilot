# BEVN-101 — Sessions endpoint performance investigation

**Investigated, NOT fixed.** The legacy .NET implementation is deferred. Track B proceeds to the NestJS and Prisma migration in EXT-150. Neither prototype was approved for production, and issue #6 remains open. [BEVN-101 issue](https://github.com/maiano/brown-events-pilot/issues/6)

## Root cause and baseline

`GET /api/conferences/{id}/sessions` runs one filtered sessions SELECT and returns tracked EF entities. During JSON serialization, lazy-loading proxies fetch conference, speaker, and room references and their inverse collections. A shared speaker or room can lead to another conference. Registrations can extend the graph through a shared attendee. `IgnoreCycles` emits null for a repeated object on the active serialization path, while navigation getters can still trigger SQL. The singleton context retains loaded relationships across requests, which explains why warm requests conceal the N+1 pattern. [Service query](../../backend/BrownEvents.Api/Services/ConferenceService.cs#L54) · [HTTP action](../../backend/BrownEvents.Api/Controllers/ConferencesController.cs#L73) · [Context and serializer configuration](../../backend/BrownEvents.Api/Program.cs#L12)

PostgreSQL 14.24 server logs captured each complete HTTP response on an isolated database. Application name, PID, SQL window markers, and an unrelated control query verified attribution. Startup and fixture SQL were excluded, and `DISCARD ALL` connection resets were counted separately. At 50 sessions, 25 distinct speakers and 25 distinct rooms produced 100 SELECTs for references and inverse collections. The remaining four covered the root query, conference, inverse conference sessions, and registrations.

The repeated comparison used five independent cold processes per build and fixture, each followed by a warm request on the same process. Timings are median full-body HTTP times in milliseconds. All responses returned HTTP 200 and the expected root session count. Cold and warm values are shown as pairs.

| Sessions | Legacy SELECTs | Candidate B SELECTs | Legacy median ms | Candidate B median ms | Response bytes, both |
|---:|---:|---:|---:|---:|---:|
| 0 | 1 / 1 | 1 / 1 | 370 / 53 | 585 / 62 | 11 |
| 1 | 8 / 1 | 7 / 7 | 857 / 58 | 824 / 58 | 669 |
| 10 | 23 / 1 | 7 / 7 | 748 / 39 | 1,013 / 55 | 67,138 |
| 50 | 104 / 1 | 7 / 7 | 1,126 / 127 | 989 / 149 | 791,260 |

## Candidates and compatibility

- **A — split eager loading.** `Include` and `ThenInclude` reduced the cold 50-session request to five SELECTs. The multi-hop fixture still issued 18 cold SELECTs because the formatter reached navigations beyond the include tree. The prototype also reordered the visible top-level session array on that fixture. Split queries avoided joining sibling collections into one result, but the 50-session inverse conference collection query still joined 50 roots to 50 sessions.
- **B — recursive reachability and bulk loading.** One recursive PostgreSQL query found and loaded reachable conferences. Five bulk entity queries populated the existing tracked graph after the root query. The result was one SELECT for empty or unknown IDs and seven for every tested nonempty response, including multi-hop cold and warm requests. The approach ties SQL reachability and explicit EF navigation bookkeeping to the entity model. Its cost on large connected graphs and its effect on later requests sharing the tracker remain unverified.

The original JSON was stable across fresh processes for these fixtures. B matched the supported data in all 52 paired comparisons, including identifiers, fields, nested membership, duplicates, null counts, and cross-conference references. In six reversed-insertion multi-hop responses, the same null cycle placeholder moved within two nested `attendee.registrations` arrays. Those arrays have no documented order, and the React page does not render them. B preserved the top-level session sequence, which React renders directly. Unknown arrays were compared positionally. [Swagger response schema](../../backend/BrownEvents.Api/Swagger/ApiDocumentationOperationFilter.cs#L104) · [React session list](../../frontend/src/components/SessionList.jsx#L4)

B improved cold median latency at 50 sessions by about 12%, but 10-session cold latency was worse in four of five pairs. Median PostgreSQL execution for that fixture was 1.30 ms for legacy and 1.21 ms for B, including 0.28 ms for B's recursive query. Most of the elapsed difference was outside SQL execution. At 50 sessions, database result rows fell from 201 to 151 cold but rose from 50 to 151 warm. The response remained 791,260 bytes, so bounding SELECTs leaves substantial serialization work. Process memory and production latency were outside these measurements.

## Deferral and EXT-150 verification

A failed the bounded-query and visible-order checks. B preserved the tested response data and bounded SELECTs, but its mixed latency results, warm preload cost, and unverified shared-tracker effects made it unsuitable for rollout before the migration. Scope the migration solution to explicit, set-based Prisma relation loading and preserve all verified response data. Establish the response shape in the EXT-150 spec before implementation. [EXT-150 issue](https://github.com/maiano/brown-events-pilot/issues/7)

### Proposed EXT-150 acceptance criteria

1. On isolated PostgreSQL, exercise 0, 1, 10, and 50 sessions plus unknown IDs, optional speaker and room links, shared references, cross-conference sessions, and multi-hop shared attendees. Assert HTTP 200 with `{"data":[]}` for empty and unknown cases.
2. Verify status, scalar values, field presence, relationship membership, duplicate and null counts, and cross-conference references against legacy HTTP responses. Preserve the displayed root session sequence on fixtures and any documented ordering. Use multiset comparisons only for confirmed unordered collections.
3. Count statements through the complete HTTP response. Target at most one business SELECT for empty or unknown IDs and seven for nonempty fixtures, with a bound independent of session count and relationship depth. Record connection housekeeping separately.
4. Run at least five cold and five warm trials per fixture against both builds under the same conditions. Target no median latency regression at 10 sessions cold or at 10 and 50 sessions warm, and at least a 10% improvement at 50 sessions cold. Report response bytes and database result rows alongside latency.
5. Check conference list, detail, and registration reads, session detail, and speaker list before and after a sessions request. Check post-update and post-registration responses for data freshness. Include a larger connected fixture to expose excessive graph expansion and qualify any resource limits.

### Diagnostic scripts

Recommend retaining the two untracked scripts for migration verification as separately reviewed diagnostics. [`seed.sql`](../../scripts/bevn-101/seed.sql) is a deterministic legacy-schema fixture. Keep it for reproducing the legacy baseline and port its data to a Prisma fixture, adding the multi-hop and shared-attendee cases. [`smoke.py`](../../scripts/bevn-101/smoke.py) provides disposable PostgreSQL setup, complete-response capture, and SQL attribution controls. Reuse those parts and replace its ASP.NET startup checks, connection configuration, and legacy SQL-shape assertion for NestJS. The checked scripts do not contain the temporary repeated-trial or semantic comparison tooling. Both scripts were left unmodified and untracked.
