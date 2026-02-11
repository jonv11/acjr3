# 1. Ticket Triage And Inbox Processing

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
  --raw
```

2. Open one issue with details needed for triage.

```bash
acjr3 issue view ACJ-123 \
  --fields "summary,description,priority,labels,components,duedate,assignee,issuelinks" \
  --expand "renderedFields" \
  --raw
```

3. Apply a deterministic triage rubric.
- Missing acceptance criteria
- Missing scope boundaries
- Missing repro steps (for bugs)
- Missing owner or due date

4. Update fields in one controlled change.

```bash
acjr3 issue update ACJ-123 --body-file triage-update.json --fail-on-non-success
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

5. Post triage outcome comment.

```bash
acjr3 issue comment ACJ-123 --text "Triage complete. Missing AC and repro details. Added needs-info. Next: requester to provide details by Friday."
```

6. Transition to the next workflow state.

```bash
acjr3 issue transition ACJ-123 --to "Needs Triage" --fail-on-non-success
```

7. Verify state and fields.

```bash
acjr3 issue view ACJ-123 --fields "status,priority,labels,duedate" --raw
```

## Safety checks

- Use constrained JQL (project + updated window + status).
- Keep field updates in a JSON file for review.
- Always add an audit comment when changing status or priority.
