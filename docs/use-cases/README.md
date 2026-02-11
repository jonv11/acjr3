# Use Case Playbooks

Navigation: [Docs Home](../README.md) | [Commands](../commands/README.md) | [Repository Home](../../README.md)

These playbooks show step-by-step ways to execute common Jira workflows with `acjr3`.

Each file is one use case and follows the same pattern:
- scope with narrow search
- inspect issue details
- decide with a deterministic rubric
- write updates (`update`, `comment`, `transition`, or `request`)
- verify with a follow-up `view` or `search`

## Global workflow rule

For every scenario, use:

1. `search`
2. `view`
3. `decide`
4. `write`
5. `verify`

Prefer small, reversible edits.  
When changing workflow-sensitive fields (status, priority, assignee, sprint), add a short audit comment.

## Sugaring priorities

Prioritized command sugar opportunities based on playbook order and frequency:

1. `acjr3 issue update`: add targeted flags for common fields (`--priority`, `--labels-add`, `--labels-remove`, `--due-date`, `--fix-version`, `--story-points`) to reduce `--body-file` usage in triage, planning, execution, release, and hygiene flows.
2. `acjr3 issue create`: add common creation flags (`--labels`, `--priority`, `--components`, `--due-date`) so fewer scenarios require external JSON payload files.
3. `acjr3 issue transition`: add optional inline update/comment sugar (for example `--comment`, optional `--resolution`) for one-step state changes with audit notes.
4. `acjr3 project version create` and `acjr3 project version update`: add dedicated shortcuts so release workflows do not need `acjr3 request` for version lifecycle operations.
5. `acjr3 issue attach`: add first-class attachment support to cover evidence and artifact-heavy flows currently blocked on external tooling.
6. `acjr3 issuelink delete`: add link-removal sugar (by link ID) for dependency cleanup workflows that currently only support easy link creation.

## Files

1. [Ticket triage and inbox processing](01-ticket-triage-inbox-processing.md)
2. [Create high-quality tickets from chat or spec fragments](02-create-high-quality-tickets.md)
3. [Refine tickets to ready for dev](03-refine-ready-for-dev.md)
4. [Sprint planning support](04-sprint-planning-support.md)
5. [Daily execution loop](05-daily-execution-loop.md)
6. [Incident or bug reproduction and escalation](06-incident-bug-repro-escalation.md)
7. [Dependency and blocker management](07-dependency-blocker-management.md)
8. [Release coordination and changelog generation](08-release-coordination-changelog.md)
9. [Knowledge extraction: decisions and rationale](09-knowledge-extraction-decisions.md)
10. [Bulk maintenance and hygiene](10-bulk-maintenance-hygiene.md)
