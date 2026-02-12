# 3. Refine Tickets To Ready For Dev

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](02-create-high-quality-tickets.md) | [Next](04-sprint-planning-support.md)

## Goal

Turn a vague issue into an implementable ticket with clear scope, acceptance criteria, and dependencies.

## Commands used

- `acjr3 issue view`
- `acjr3 issue comment`
- `acjr3 issue update`
- `acjr3 issuelink`
- `acjr3 issue transition`

## Step-by-step

1. Pull candidate issues for refinement.

```bash
acjr3 search list \
  --jql "project = ACJ AND status = \"Needs Refinement\" ORDER BY updated DESC" \
  --fields "key,summary,status,labels,priority" \
  --max-results 30 --compact
```

2. Read current detail and comments.

```bash
acjr3 issue view ACJ-789 --expand "renderedFields" --compact
acjr3 issue comment list ACJ-789 --max-results 50 --compact
```

3. Run readiness checklist.
- Repro or user-flow steps exist
- non-goals listed
- acceptance criteria testable
- dependency list explicit

4. Ask only missing questions (ADF-first).

```bash
acjr3 issue comment add ACJ-789 --in ready-questions-comment.adf.json --input-format adf --yes
```

5. Rewrite description using ADF, then apply non-description fields.

```bash
acjr3 issue update ACJ-789 --field description --field-file ready-description.adf.json --field-format adf --yes --fail-on-non-success
acjr3 issue update ACJ-789 --in ready-update.json --input-format json --yes --fail-on-non-success
```

6. Add or fix dependency links when required.

```bash
acjr3 issuelink --type "Blocks" --inward "ACJ-789" --outward "ACJ-740" --yes --fail-on-non-success
```

7. Transition to ready status.

```bash
acjr3 issue transition ACJ-789 --to "Ready" --yes --fail-on-non-success
```

8. Verify readiness.

```bash
acjr3 issue view ACJ-789 --fields "status,labels,description,issuelinks" --compact
```


