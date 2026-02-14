# 1. Ticket Triage And Inbox Processing

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Next](02-create-high-quality-tickets.md)

## Goal

Scan new or updated issues, classify urgency, and move each item into a clear next state.

## Commands used

- `acjr3 search list`
- `acjr3 issue view`
- `acjr3 issue update`
- `acjr3 issue comment`
- `acjr3 issue transition`

## Step-by-step

1. Search a narrow triage queue.

```bash
acjr3 search list \
  --jql "project = ACJ AND updated >= -1d AND status not in (Done, Closed) ORDER BY updated DESC" \
  --fields "key,summary,priority,status,assignee,labels,updated" \
  --max-results 50 \
  --compact
```

2. Open one issue with details needed for triage.

```bash
acjr3 issue view ACJ-123 \
  --fields "summary,description,priority,labels,components,duedate,assignee,issuelinks" \
  --expand "renderedFields" \
  --compact
```

3. Apply a deterministic triage rubric.
- Missing acceptance criteria
- Missing scope boundaries
- Missing repro steps (for bugs)
- Missing owner or due date

4. Update fields in one controlled change.

```bash
acjr3 issue update ACJ-123 --in triage-update.json --yes --allow-non-success
```

`triage-update.json` example:

```json
{
  "fields": {
    "priority": { "name": "High" },
    "labels": ["triaged", "needs-info"],
    "duedate": "2026-02-13"
  }
}
```

5. Post triage outcome comment (ADF-first).

```bash
acjr3 issue comment add ACJ-123 --text-file triage-outcome-comment.adf.json --yes
```

6. Transition to the next workflow state.

```bash
acjr3 issue transition ACJ-123 --to "Needs Triage" --yes --allow-non-success
```

7. Verify state and fields.

```bash
acjr3 issue view ACJ-123 --fields "status,priority,labels,duedate" --compact
```

## Safety checks

- Use constrained JQL (project + updated window + status).
- Keep field updates in a JSON file for review.
- Always add an audit comment when changing status or priority.


