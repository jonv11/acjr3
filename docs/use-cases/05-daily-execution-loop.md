# 5. Daily Execution Loop

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](04-sprint-planning-support.md) | [Next](06-incident-bug-repro-escalation.md)

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
  --max-results 50 --compact
```

2. Open top candidate and validate blockers.

```bash
acjr3 issue view ACJ-222 --fields "status,priority,duedate,issuelinks,subtasks" --compact
```

3. Start the top unblocked item.

```bash
acjr3 issue transition ACJ-222 --to "In Progress" --yes --allow-non-success
```

4. Post concise status (ADF-first).

```bash
acjr3 issue comment add ACJ-222 --text-file daily-status-comment.adf.json --yes
```

5. If blocked, transition and escalate quickly.

```bash
acjr3 issue transition ACJ-222 --to "Blocked" --yes --allow-non-success
acjr3 issue comment add ACJ-222 --text-file blocked-escalation-comment.adf.json --yes
```

6. If your team tracks estimates, update field payload.

```bash
acjr3 issue update ACJ-222 --in estimate-update.json --yes --allow-non-success
```

7. Verify final state.

```bash
acjr3 issue view ACJ-222 --fields "status,updated,assignee" --compact
```


