# Essential Coverage Testing Guide

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Coverage Roadmap](essential-coverage-priority.md)

This guide defines the manual validation workflow for `docs/commands/essential-coverage-priority.md` and serves as a handoff log.

## Safety Rules

- Use a dedicated test project for all create/update/delete testing.
- Do not run destructive commands against production project keys.
- If a test requires deletion, only delete data created by this test run.
- Prefer read-only checks when a roadmap item can be validated without writes.

## Environment Prerequisites

- `ACJR3_SITE_URL`
- `ACJR3_AUTH_MODE`
- `ACJR3_EMAIL` + `ACJR3_API_TOKEN` for basic auth, or `ACJR3_BEARER_TOKEN` for bearer auth

Validation command:

```bash
dotnet run --project src/acjr3 -- config check
```

## Dedicated Test Project Strategy

Primary approach:
- Create one Jira project for command coverage testing.
- Reuse that project for issue/comment/transition/metadata tests.
- Keep naming explicit (for example key prefix `ACJR3T`) to simplify cleanup.

Create project (admin permission required) with universal request command:

```bash
dotnet run --project src/acjr3 -- request POST /rest/api/3/project ^
  --content-type application/json ^
  --body "{\"key\":\"ACJR3T\",\"name\":\"acjr3 CLI Test Project\",\"projectTypeKey\":\"software\",\"projectTemplateKey\":\"com.pyxis.greenhopper.jira:gh-simplified-scrum-classic\",\"description\":\"Temporary project for acjr3 command coverage testing\",\"leadAccountId\":\"<ACCOUNT_ID>\"}" ^
  --fail-on-non-success
```

If project creation is not permitted:
- Use an existing non-production sandbox project key.
- Record that key in the execution log below.

## Test Execution Pattern Per Roadmap Item

For each unchecked roadmap item:
1. Implement code changes.
2. Update docs for syntax/behavior changes.
3. Run `dotnet build acjr3.sln`.
4. Run `dotnet test acjr3.sln`.
5. Run command-level manual checks:
   - `--help` for modified command/subcommand.
   - successful and error-path example invocations.
   - CRUD validation for write operations in test project only.
6. Mark the roadmap checkbox only after steps 1-5 pass.
7. Append results to the execution log.

## Execution Log

- Date: 2026-02-11
- Operator: Codex
- Config check: PASS
- Test project key: ACJRT
- Notes: Started roadmap implementation; detailed per-item logs will be appended as items are completed.

### Item Log: Phase 0.1 `search list` endpoint migration

- Status: COMPLETE
- Code validation:
  - `dotnet build acjr3.sln` -> PASS
  - `dotnet test acjr3.sln` -> PASS
- Manual validation:
  - `dotnet run --project src/acjr3 -- search list --help` -> PASS (shows `--max-results`, `--next-page-token`, `--fields`)
  - `dotnet run --project src/acjr3 -- search list --project MFBC --max-results 5 --raw` -> PASS (`HTTP 200 OK`, active `/search/jql` behavior)
  - `dotnet run --project src/acjr3 -- search list --max-results 5 --raw` -> EXPECTED API ERROR (`HTTP 400`, unbounded JQL not allowed without restriction)

### Item Log: Phase 0.2 `project list` endpoint migration

- Status: COMPLETE
- Manual validation:
  - `dotnet run --project src/acjr3 -- project list --help` -> PASS (shows search filters and pagination options)
  - `dotnet run --project src/acjr3 -- project list --max-results 5 --raw` -> PASS (`HTTP 200 OK`, paged values payload)

### Item Log: Phase 0.3 `group list` endpoint migration

- Status: COMPLETE
- Manual validation:
  - `dotnet run --project src/acjr3 -- group list --help` -> PASS (shows bulk endpoint filter options)
  - `dotnet run --project src/acjr3 -- group list --max-results 5 --raw` -> PASS (`HTTP 200 OK`, paged values payload)

### Item Log: Phase 0.4 `resolution list` endpoint migration

- Status: COMPLETE
- Manual validation:
  - `dotnet run --project src/acjr3 -- resolution list --help` -> PASS (shows search/paging options)
  - `dotnet run --project src/acjr3 -- resolution list --max-results 5 --raw` -> PASS (`HTTP 200 OK`, paged values payload)

### Item Log: Phase 1.1 `issue comment` CRUD coverage

- Status: COMPLETE
- Test data setup:
  - Created dedicated project: `ACJRT` (`POST /rest/api/3/project`)
  - Created dedicated issue: `ACJRT-1`
- Manual validation:
  - `issue comment --help` -> PASS (legacy + CRUD subcommands visible)
  - `issue comment add ACJRT-1 --text ...` -> PASS (`HTTP 201`, comment created)
  - `issue comment list ACJRT-1 --max-results 10` -> PASS (`HTTP 200`)
  - `issue comment get ACJRT-1 <id>` -> PASS (`HTTP 200`)
  - `issue comment update ACJRT-1 <id> --text ...` -> PASS (`HTTP 200`)
  - `issue comment delete ACJRT-1 <id>` -> PASS (`HTTP 204`)
  - Legacy add form `issue comment ACJRT-1 --text ...` -> PASS (`HTTP 201`) then deleted (`HTTP 204`)

### Item Log: Phase 1.2 `issue transition` coverage

- Status: COMPLETE
- Manual validation:
  - `issue transition --help` -> PASS (shows `--id`, `--to`, and `list` subcommand)
  - `issue transition list ACJRT-1 --include-unavailable-transitions false --sort-by-ops-bar-and-status true` -> PASS (`HTTP 200`)
  - `issue transition ACJRT-1 --id <valid-id>` -> PASS (`HTTP 204`)
  - `issue transition ACJRT-1 --to Done` -> PASS (`HTTP 204`, name resolved to ID)

### Item Log: Phase 1.3 `issue view` payload controls

- Status: COMPLETE
- Manual validation:
  - `issue view --help` -> PASS (shows `--fields-by-keys`, `--expand`, `--properties`, `--update-history`, `--fail-fast`)
  - `issue view ACJRT-1 --fields summary,status --fields-by-keys false --expand names --properties '*' --update-history false --fail-fast true --raw` -> PASS (`HTTP 200`)

### Item Log: Phase 1.4 `project version list` paging parity

- Status: COMPLETE
- Manual validation:
  - `project version list --help` -> PASS (shows paging/filter options)
  - `project version list --project ACJRT --max-results 10 --raw` -> PASS (`HTTP 200`, paged response shape)

### Item Log: Phase 1.5 `field search` shortcut

- Status: COMPLETE
- Manual validation:
  - `field search --help` -> PASS
  - `field search --max-results 5 --query summary --raw` -> PASS (`HTTP 200`)

### Item Log: Phase 1.6 `user search` paging/filter parity

- Status: COMPLETE
- Manual validation:
  - `user search --help` -> PASS (shows `--start-at`, `--max-results`, `--account-id`, `--property`)
  - `user search --account-id <current-account-id> --max-results 5 --raw` -> PASS (`HTTP 200`)

### Item Log: Phase 2.1 metadata discovery commands

- Status: COMPLETE
- Manual validation:
  - `issue createmeta --help` -> PASS
  - `issue createmeta --project-keys ACJRT --raw` -> PASS (`HTTP 200`)
  - `issue editmeta --help` -> PASS
  - `issue editmeta ACJRT-1 --raw` -> PASS (`HTTP 200`)

### Item Log: Phase 2.2 config UX expansion

- Status: COMPLETE
- Manual validation:
  - `config show` -> PASS
  - `config set ACJR3_MAX_RETRIES 7 --target process` -> PASS
  - `config init --target process --site-url ... --auth-mode basic ...` -> PASS
- Safety note:
  - Validation used `--target process` only; no persistent user environment variables were changed during these checks.

### Item Log: Phase 3.1 write payload input standardization

- Status: COMPLETE
- Manual validation:
  - `issue transition --help` shows `--body` and `--body-file`.
  - `issue transition ACJRT-1 --body-file <transition-payload.json> --fail-on-non-success` -> PASS (`HTTP 204`)

### Item Log: Phase 3.2 JQL file support

- Status: COMPLETE
- Manual validation:
  - `search list --help` shows `--jql-file`.
  - `search list --jql-file <jql.txt> --jql "status = Done" --max-results 10 --raw --fail-on-non-success` -> PASS (`HTTP 200`)
- Merge behavior:
  - `--jql-file` content, `--jql`, and shortcut filters are combined with `AND`.

### Item Log: Phase 3.3 pagination flag normalization

- Status: COMPLETE
- Manual validation:
  - Verified consistent `--start-at` and `--max-results` naming on paged list/search shortcuts.
  - Verified token-based paging exposure on JQL search via `--next-page-token`.

### Item Log: Phase 3.4 output/failure control normalization

- Status: COMPLETE
- Manual validation:
  - `priority list --help` includes `--fail-on-non-success`.
  - `search list --max-results 5 --raw --fail-on-non-success` returns `HTTP 400` and command exit code `1`.

### Item Log: Phase 3.5 per-command config overrides

- Status: COMPLETE
- Manual validation:
  - Global override options appear in command help output.
  - `--max-retries 1 config show` -> PASS (effective value reflects invocation override).
  - `--site-url https://jonv11.atlassian.net priority list --raw --fail-on-non-success` -> PASS.

## Test Project Manifest

- Test project key: `ACJRT`
- Purpose: isolated write-path validation for issue/comment/transition/metadata commands
- Created resources:
  - Issue `ACJRT-1` for command verification
  - Multiple comments created/deleted during CRUD tests
- Current expected retained data:
  - `ACJRT-1` exists as an audit artifact for this roadmap run
- Cleanup guidance:
  - Safe cleanup can delete `ACJRT-1` and then delete project `ACJRT` if no longer needed.
