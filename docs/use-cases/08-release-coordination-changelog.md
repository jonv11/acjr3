# 8. Release Coordination And Changelog Generation

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
acjr3 project version list --project ACJ --max-results 200 --raw
```

2. Pull issues for target fixVersion.

```bash
acjr3 search list \
  --jql "project = ACJ AND fixVersion = \"2026.02\" ORDER BY issuetype, key" \
  --fields "key,summary,issuetype,status,fixVersions,labels" \
  --max-results 200 --raw
```

3. Validate stragglers (not done).

```bash
acjr3 search list \
  --jql "project = ACJ AND fixVersion = \"2026.02\" AND statusCategory != Done" \
  --fields "key,summary,status,assignee" --raw
```

4. Fix missing or incorrect fixVersion values.

```bash
acjr3 issue update ACJ-501 --body-file set-fixversion.json --fail-on-non-success
```

5. Generate per-ticket note summary as comments when required.

```bash
acjr3 issue comment ACJ-501 --text "Release notes draft: Adds retry-safe import API and structured error handling for 429/5xx."
```

6. Optional version management via generic request API.

```bash
acjr3 request POST /rest/api/3/version --body-file create-version.json --fail-on-non-success
acjr3 request PUT /rest/api/3/version/<VERSION_ID> --body-file release-version.json --fail-on-non-success
```

## Accuracy notes

- `version create/release` shortcut commands are not currently implemented.
- Use `request` for version lifecycle endpoints when needed.
