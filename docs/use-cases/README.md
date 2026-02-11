# Use Case Playbooks

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

1. `docs/use-cases/01-ticket-triage-inbox-processing.md`
2. `docs/use-cases/02-create-high-quality-tickets.md`
3. `docs/use-cases/03-refine-ready-for-dev.md`
4. `docs/use-cases/04-sprint-planning-support.md`
5. `docs/use-cases/05-daily-execution-loop.md`
6. `docs/use-cases/06-incident-bug-repro-escalation.md`
7. `docs/use-cases/07-dependency-blocker-management.md`
8. `docs/use-cases/08-release-coordination-changelog.md`
9. `docs/use-cases/09-knowledge-extraction-decisions.md`
10. `docs/use-cases/10-bulk-maintenance-hygiene.md`
