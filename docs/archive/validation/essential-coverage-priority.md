# Essential Command Coverage Roadmap

Navigation: [Archive](../README.md) | [Validation Index](README.md) | [Coverage Testing Guide](essential-coverage-testing-guide.md)

Historical note: This roadmap is archived context and not an operational source of truth.

This file is the source of truth for progress toward essential Jira shortcut command coverage.

How to use this roadmap:
- Keep items unchecked until implementation, tests, and docs are complete.
- Mark an item complete only when all listed acceptance checks are satisfied.
- If scope changes, update this file first so current priority stays explicit.

Scope guardrails:
- Jira Cloud REST API v3 docs are the only API source of truth.
- Do not add deprecated or undocumented endpoints.
- Keep CLI naming aligned to Jira URI paths and existing command conventions.

## Phase 0 - Replace Deprecated Or Incorrect Endpoints

- [x] `search list` uses `GET /rest/api/3/search/jql` (not deprecated search endpoint)
Acceptance checks:
- command uses active v3 endpoint
- existing filters still work
- pagination controls are available (`--max-results`, `--next-page-token`)
- docs and tests updated

- [x] `project list` uses `GET /rest/api/3/project/search`
Acceptance checks:
- command no longer calls deprecated project listing endpoint
- project search filters are exposed as CLI options
- docs and tests updated

- [x] `group list` uses `GET /rest/api/3/group/bulk`
Acceptance checks:
- command supports bulk group listing behavior
- pagination/filter options exposed
- docs and tests updated

- [x] `resolution list` uses `GET /rest/api/3/resolution/search`
Acceptance checks:
- command no longer calls deprecated resolution endpoint
- search-oriented options added where supported
- docs and tests updated

## Phase 1 - Add Essential Coverage Gaps

- [x] Expand `issue comment` into full CRUD coverage
Acceptance checks:
- add/list/get/update/delete comment commands exist
- endpoints map to `/issue/{issueIdOrKey}/comment` and `/comment/{id}` forms
- write commands support canonical `--in <PATH|-> --input-format ... --yes|--force`
- docs and tests updated

- [x] Expand `issue transition` coverage
Acceptance checks:
- transition list command exists
- transition execution supports transition ID (not only name)
- transition query options are exposed for transition listing
- docs and tests updated

- [x] Complete `issue view` payload controls
Acceptance checks:
- supports `--fields`, `--fields-by-keys`, `--expand`, `--properties`
- supports query flags like `--update-history` and fail-fast behavior if intended
- docs and tests updated

- [x] Add paged `project version list` parity
Acceptance checks:
- supports Jira paging semantics for project versions
- predictable output for large version sets
- docs and tests updated

- [x] Add `field search` shortcut command
Acceptance checks:
- command maps to `GET /rest/api/3/field/search`
- supports paging and search filters
- supports project-scoped filters where available
- docs and tests updated

- [x] Complete `user search` pagination/filter parity
Acceptance checks:
- supports `--start-at` and `--max-results`
- supports supported user search filters (`--account-id`, `--property`, etc.)
- docs and tests updated

## Phase 2 - Metadata And Config Experience

- [x] Add issue metadata discovery commands
Acceptance checks:
- create metadata command(s) for active v3 endpoints are implemented
- edit metadata command maps to `GET /rest/api/3/issue/{issueIdOrKey}/editmeta`
- docs and tests updated

- [x] Expand config UX beyond `config check`
Acceptance checks:
- config setup and inspection commands exist (for example `init`, `set`, `show`)
- behavior is documented with safe defaults and validation expectations
- docs and tests updated

## Phase 3 - Cross-Cutting CLI Consistency

- [x] Standardize write payload input across write shortcuts
Acceptance checks:
- mutating write shortcuts accept canonical `--in <PATH|-> --input-format ...`
- mutating write shortcuts require `--yes` or `--force`
- description/comment flows expose field helpers where implemented (`--description-file <PATH> --description-format adf`, `--field-file <PATH> --field-format adf`)
- validation and error messaging are consistent
- docs and tests updated

- [x] Add JQL file support to search workflows
Acceptance checks:
- `search list` supports `--jql-file`
- merge behavior between `--jql` and `--jql-file` is defined and tested
- docs and tests updated

- [x] Normalize pagination flags on list/search commands
Acceptance checks:
- `--start-at` and `--max-results` are consistently named and wired
- token-based pagination is exposed where required by endpoint behavior
- docs and tests updated

- [x] Normalize output and failure controls
Acceptance checks:
- `--format json --compact` behavior is consistent for shortcut commands
- fail-on-non-success behavior is consistent and documented
- docs and tests updated

- [x] Add per-command config override support where appropriate
Acceptance checks:
- supports runtime overrides such as site URL/auth mode/timeout/retries where design permits
- precedence rules (env/config/flag) are documented and tested
- docs and tests updated

## Completion Criteria

- [x] Every roadmap item above is complete.
- [x] `dotnet build acjr3.sln` passes.
- [x] `dotnet test acjr3.sln` passes.
- [x] Help output matches docs for updated commands.
- [x] `docs/commands/jira-shortcuts.md` reflects implemented behavior and options.

## Jira API References

- https://developer.atlassian.com/cloud/jira/platform/rest/v3/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issue-search/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issue-comments/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-projects/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-project-versions/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-users/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issue-fields/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-groups/
- https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-resolutions/

