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
