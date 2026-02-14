# Developer Setup

Navigation: [Docs Home](README.md) | [Contributing](../CONTRIBUTING.md) | [User Getting Started](getting-started.md)

This page is for contributors working from a source checkout.

## Requirements

- .NET 8 SDK
- Jira Cloud site URL and credentials for manual command testing

## Build And Test

```bash
dotnet build acjr3.sln
dotnet test acjr3.sln
```

## Run CLI From Source

```bash
dotnet run --project src/acjr3 -- --help
dotnet run --project src/acjr3 -- config check
```

## Command Style Conversion Rule

User docs use:

```bash
acjr3 <args>
```

Equivalent from a source checkout:

```bash
dotnet run --project src/acjr3 -- <args>
```

## Build Local Distributable Binary

Example (Linux x64):

```bash
dotnet publish src/acjr3/acjr3.csproj -c Release -p:PublishProfile=SingleFileSelfContained -r linux-x64 --self-contained true
```

Change runtime identifier (`-r`) as needed (`win-x64`, `linux-x64`, `osx-x64`).

## Documentation Truth Checks

Before merging doc changes, compare docs to current help output:

```bash
dotnet run --project src/acjr3 -- --help
dotnet run --project src/acjr3 -- <command> --help
```

Use source command definitions in `src/acjr3/Commands/` and integration expectations in `tests/acjr3.Tests/Integration/ProgramE2eTests.cs` as source of truth.
