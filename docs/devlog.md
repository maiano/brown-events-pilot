# Development Log

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
