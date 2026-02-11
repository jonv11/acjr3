# 4. Sprint Planning Support

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](03-refine-ready-for-dev.md) | [Next](05-daily-execution-loop.md)

## Goal

Assemble a candidate sprint set from ready issues, balanced by priority, risk, and size.

## Commands used

- `acjr3 search list`
- `acjr3 issue view`
- `acjr3 issue update`
- `acjr3 issue transition`
- `acjr3 request` (if sprint field update requires explicit custom field payload)

## Step-by-step

1. Pull ready backlog candidates.

```bash
acjr3 search list \
  --jql "project = ACJ AND status = Ready ORDER BY priority DESC, Rank ASC" \
  --fields "key,summary,priority,labels,issuelinks" \
  --max-results 100 --raw
```

2. Review top candidates for blockers and size signals.

```bash
acjr3 issue view ACJ-101 --fields "summary,priority,labels,issuelinks,customfield_10016" --raw
```

3. Select a balanced proposal.
- Include high-priority and low-risk mix
- avoid unresolved blocker chains
- avoid only large-point items

4. Set sprint/assignee/points after approval.

```bash
acjr3 issue update ACJ-101 --body-file sprint-assign.json --fail-on-non-success
```

5. Transition to development intake state.

```bash
acjr3 issue transition ACJ-101 --to "Selected for Development" --fail-on-non-success
```

6. Verify set.

```bash
acjr3 search list \
  --jql "project = ACJ AND status = \"Selected for Development\" AND sprint = 1234" \
  --fields "key,summary,assignee,priority" --raw
```

## Accuracy notes

- `board` and `sprint` shortcut commands are not currently implemented.
- Use issue fields and JQL for sprint planning; use `issue update --body-file` for sprint custom-field assignment.
