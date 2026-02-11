# 5. Daily Execution Loop

## Goal

Generate a dependency-aware next-action list and keep Jira status/comments current.

## Commands used

- `acjr3 search list`
- `acjr3 issue view`
- `acjr3 issue transition`
- `acjr3 issue comment`
- `acjr3 issue update`

## Step-by-step

1. Build the daily queue.

```bash
acjr3 search list \
  --jql "assignee = currentUser() AND status in (\"To Do\",\"In Progress\",\"Blocked\") ORDER BY priority DESC, duedate ASC" \
  --fields "key,summary,status,priority,duedate,issuelinks" \
  --max-results 50 --raw
```

2. Open top candidate and validate blockers.

```bash
acjr3 issue view ACJ-222 --fields "status,priority,duedate,issuelinks,subtasks" --raw
```

3. Start the top unblocked item.

```bash
acjr3 issue transition ACJ-222 --to "In Progress" --fail-on-non-success
```

4. Post concise status.

```bash
acjr3 issue comment ACJ-222 --text "Daily update: started implementation; next checkpoint today 16:00; no blockers yet."
```

5. If blocked, transition and escalate quickly.

```bash
acjr3 issue transition ACJ-222 --to "Blocked" --fail-on-non-success
acjr3 issue comment ACJ-222 --text "Blocked by ACJ-210 API contract decision. Need owner response."
```

6. If your team tracks estimates, update field payload.

```bash
acjr3 issue update ACJ-222 --body-file estimate-update.json --fail-on-non-success
```

7. Verify final state.

```bash
acjr3 issue view ACJ-222 --fields "status,updated,assignee" --raw
```
