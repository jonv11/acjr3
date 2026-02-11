# Documentation Conventions

Use these rules when adding or editing docs in this repo.

## File structure

- Prefer many short files over one large file.
- Keep one topic per file.
- Put command details under `docs/commands/`.
- Keep top-level `README.md` as a quick entry point.
- Keep `src` and `tests` organized by domain folders.
- Mirror source domains in tests when adding new features.

## Writing style

- Start with what the command/feature does.
- Show exact syntax before long explanation.
- Use runnable examples.
- Call out defaults and constraints explicitly.
- Use consistent names matching CLI help output.

## Accuracy checks

Before merging doc changes:

1. Run `dotnet build acjr3.sln`.
2. Compare docs with `acjr3 --help` and relevant subcommand help.
3. Verify option defaults against source (`src/acjr3`).
4. Update limits/notes when behavior changes.
