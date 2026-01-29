# StrykerAgent

AI-accessible CLI for querying Stryker.NET mutation testing reports.

## What it does
- Reads a `mutation-report.json` report from disk.
- Produces stable JSON or human-readable outputs for summaries, files, and mutants.
- Exports data to JSONL or SQLite for indexing and retrieval.

## Install (local)
```bash
dotnet build
```

## Install (dotnet tool)
```bash
dotnet tool install --global --add-source ./StrykerAgent/bin/Release stryker-agent
```

## Usage
The CLI is exposed as `stryker-agent` when packed as a dotnet tool. For local runs:

```bash
dotnet run -- summary --format json --pretty
```

Report discovery (if `--report` is not provided):
1. `./mutation-report.json`
2. `./reports/mutation-report.json`
3. `./StrykerOutput/**/reports/mutation-report.json` (most recent by last write time)

### Commands

Summary:
```bash
stryker-agent summary --format json --pretty
```

Files:
```bash
stryker-agent files --format table --sort score --limit 10 --status Survived NoCoverage
```

Mutants:
```bash
stryker-agent mutants --format table --status Survived --file "**/*.cs" --limit 50
```

Mutant get:
```bash
stryker-agent mutant get 5 --format text
```

Export:
```bash
stryker-agent export --mode jsonl --out ./exports --overwrite
```

## Exit codes
- `0` success
- `2` invalid arguments
- `3` report not found
- `4` report parse error
- `5` entity not found
- `6` export exists
- `7` unexpected error
