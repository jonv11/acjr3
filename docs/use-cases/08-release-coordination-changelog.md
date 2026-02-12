# 8. Release Coordination And Changelog Generation

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](07-dependency-blocker-management.md) | [Next](09-knowledge-extraction-decisions.md)

## Goal

Validate release readiness for a target version and build draft release notes.

## Commands used

- `acjr3 search list`
- `acjr3 issue view`
- `acjr3 project version list`
- `acjr3 issue update`
- `acjr3 request` (version create/release when needed)
- `acjr3 issue comment`

## Step-by-step

1. Inspect existing project versions.

```bash
acjr3 project version list --project ACJ --max-results 200 --compact
```

2. Pull issues for target fixVersion.

```bash
acjr3 search list \
  --jql "project = ACJ AND fixVersion = \"2026.02\" ORDER BY issuetype, key" \
  --fields "key,summary,issuetype,status,fixVersions,labels" \
  --max-results 200 --compact
```

3. Validate stragglers (not done).

```bash
acjr3 search list \
  --jql "project = ACJ AND fixVersion = \"2026.02\" AND statusCategory != Done" \
  --fields "key,summary,status,assignee" --compact
```

4. Fix missing or incorrect fixVersion values.

```bash
acjr3 issue update ACJ-501 --in set-fixversion.json --input-format json --yes --fail-on-non-success
```

5. Generate per-ticket note summary as comments when required (ADF-first).

```bash
acjr3 issue comment add ACJ-501 --in release-notes-comment.adf.json --input-format adf --yes
```

6. Optional version management via generic request API.

```bash
acjr3 request POST /rest/api/3/version --in create-version.json --input-format json --yes --fail-on-non-success
acjr3 request PUT /rest/api/3/version/<VERSION_ID> --in release-version.json --input-format json --yes --fail-on-non-success
```

## Accuracy notes

- `version create/release` shortcut commands are not currently implemented.
- Use `request` for version lifecycle endpoints when needed.


