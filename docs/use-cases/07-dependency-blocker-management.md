# 7. Dependency And Blocker Management

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](06-incident-bug-repro-escalation.md) | [Next](08-release-coordination-changelog.md)

## Goal

Maintain an accurate dependency graph and keep blocker ownership visible.

## Commands used

- `acjr3 search list`
- `acjr3 issue view`
- `acjr3 issuelink`
- `acjr3 issue comment`
- `acjr3 issue transition`

## Step-by-step

1. Find blocked issues.

```bash
acjr3 search list \
  --jql "project = ACJ AND status = Blocked ORDER BY updated DESC" \
  --fields "key,summary,status,issuelinks,assignee" \
  --max-results 100 --compact
```

2. Validate blocker links for each issue.

```bash
acjr3 issue view ACJ-410 --fields "status,issuelinks,assignee,priority" --compact
```

3. Correct stale or missing links.

```bash
acjr3 issuelink --type "Blocks" --inward "ACJ-410" --outward "ACJ-350" --yes --allow-non-success
```

4. Notify blocker owners (ADF-first).

```bash
acjr3 issue comment add ACJ-410 --text-file blocker-owner-ping.adf.json --yes
```

5. Transition blocked/unblocked states when workflow supports it.

```bash
acjr3 issue transition ACJ-410 --to "Blocked" --yes --allow-non-success
acjr3 issue transition ACJ-410 --to "In Progress" --yes --allow-non-success
```

6. Re-run query for verification.

```bash
acjr3 search list \
  --jql "project = ACJ AND status = Blocked" \
  --fields "key,summary,issuelinks" --compact
```

## Safety checks

- Only update links when both issue keys are validated by `issue view`.
- Add a comment whenever dependency links change.


