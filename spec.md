## Spec: `stryker-agent` (.NET CLI) — AI-accessible interface for Stryker.NET mutation reports

### 1) Purpose

`stryker-agent` is a .NET global tool that turns a Stryker.NET mutation testing JSON report (MutationTestResult) into:

* stable, machine-readable JSON outputs suitable for AI agents,
* deterministic query primitives (summary/files/mutants),
* optional export formats (JSONL/SQLite) for indexing and retrieval.

The tool treats the input report as the source of truth and provides a flattened, agent-friendly schema with stable
identifiers.

---

## 2) Input contract (schema compliance)

### 2.1 Accepted input

A single JSON document matching the MutationTestResult schema with:

* Required: `schemaVersion`, `thresholds`, `files`
* Optional: `config`, `testFiles`, `projectRoot`, `performance`, `framework`, `system`

Conditional requirements when optional sections are present:

* If `performance` exists, it MUST include `setup`, `initialRun`, `mutation` (numbers; ms).
* If `framework` exists, it MUST include `name`. If `framework.branding` exists, it MUST include `homepageUrl`.
* If `system` exists, it MUST include `ci`. If `system.os` exists, it MUST include `platform`. If `system.cpu` exists,
  it MUST include `logicalCores`. If `system.ram` exists, it MUST include `total`.

### 2.2 Report discovery

If `--report` is not provided, the tool attempts, in order:

1. `./mutation-report.json`
2. `./reports/mutation-report.json`
3. `./StrykerOutput/**/reports/mutation-report.json` (most recent)

If no report is found, exit code `3`.

---

## 3) Normalized model (agent-facing)

### 3.1 Status values

The CLI MUST accept/output exactly these statuses:
`Killed | Survived | NoCoverage | CompileError | RuntimeError | Timeout | Ignored | Pending`

### 3.2 `MutantRecord` (flattened)

Derived from each entry in `files[<path>].mutants[]`.

```json
{
  "id": "string",
  "filePath": "string",
  "language": "string",
  "location": { "startLine": 1, "startColumn": 1, "endLine": 1, "endColumn": 1 },
  "mutator": "string",
  "status": "Killed|Survived|NoCoverage|CompileError|RuntimeError|Timeout|Ignored|Pending",
  "description": "string|null",
  "replacement": "string|null",
  "statusReason": "string|null",
  "durationMs": "number|null",
  "testsCompleted": "number|null",
  "coveredBy": [{ "id": "string", "name": "string|null" }],
  "killedBy": [{ "id": "string", "name": "string|null" }],
  "isStatic": "boolean|null"
}
```

Notes:

* `location` line/column are 1-based. Start is inclusive, end is exclusive.
* `coveredBy`/`killedBy` are arrays of test IDs from the report, enriched with optional test names when resolvable.
* Any field not present in the input MUST be represented as `null` (for scalars) or `[]` (for arrays) in the normalized
  output.

### 3.3 `FileSummary`

One per file entry in `files`.

```json
{
  "filePath": "string",
  "language": "string",
  "mutationScore": 0.0,
  "statusCounts": {
    "Killed": 0,
    "Survived": 0,
    "NoCoverage": 0,
    "CompileError": 0,
    "RuntimeError": 0,
    "Timeout": 0,
    "Ignored": 0,
    "Pending": 0
  },
  "totalMutants": 0
}
```

### 3.4 `RunSummary`

Computed from all mutants plus selected report metadata.

```json
{
  "schemaVersion": "string",
  "thresholds": { "high": 80, "low": 60 },
  "totals": { "files": 0, "mutants": 0 },
  "statusCounts": {
    "Killed": 0,
    "Survived": 0,
    "NoCoverage": 0,
    "CompileError": 0,
    "RuntimeError": 0,
    "Timeout": 0,
    "Ignored": 0,
    "Pending": 0
  },
  "mutationScore": {
    "percent": 0.0,
    "numeratorKilled": 0,
    "denominatorEffective": 0
  },
  "performanceMs": { "setup": 0.0, "initialRun": 0.0, "mutation": 0.0 },
  "framework": { "name": "string", "version": "string|null" },
  "system": { "ci": true }
}
```

Rules:

* `thresholds.high` and `thresholds.low` are integers in the range 0–100 (percentage points).
* `performanceMs` is included only if `performance` exists in the report.
* `framework` is included only if `framework` exists in the report.
* `system` is included only if `system` exists in the report.
* `mutationScore.percent` is computed as:

    * `numeratorKilled / denominatorEffective * 100`
    * Default denominator: count of mutants with status in
      `{Killed, Survived, Timeout, NoCoverage, CompileError, RuntimeError}`.
    * Default excluded from denominator: `{Ignored, Pending}`.
    * These choices MUST be documented and stable.

---

## 4) Field mapping contract (input → normalized)

### 4.1 Top-level

* `schemaVersion` → `RunSummary.schemaVersion`
* `thresholds.high` (integer 0–100) → `RunSummary.thresholds.high`
* `thresholds.low` (integer 0–100) → `RunSummary.thresholds.low`
* Optional passthrough (if present):

    * `performance.setup|initialRun|mutation` → `RunSummary.performanceMs.*`
    * `framework.name|version` → `RunSummary.framework.*`
    * `system.ci` → `RunSummary.system.ci`

### 4.2 Files

For each key/value pair in `files`:

* key (relative file path) → `FileSummary.filePath` and `MutantRecord.filePath`
* `language` → `FileSummary.language` and `MutantRecord.language`
* `source` → available for `mutant get` context output

### 4.3 Mutants

For each mutant in `files[<path>].mutants[]`:

* `id` → `MutantRecord.id`
* `mutatorName` → `MutantRecord.mutator`
* `status` → `MutantRecord.status`
* `location.start.line` → `MutantRecord.location.startLine`
* `location.start.column` → `MutantRecord.location.startColumn`
* `location.end.line` → `MutantRecord.location.endLine`
* `location.end.column` → `MutantRecord.location.endColumn`
* Optional fields:

    * `description` → `MutantRecord.description`
    * `replacement` → `MutantRecord.replacement`
    * `statusReason` → `MutantRecord.statusReason`
    * `duration` (ms) → `MutantRecord.durationMs`
    * `testsCompleted` → `MutantRecord.testsCompleted`
    * `static` → `MutantRecord.isStatic`
    * `coveredBy` (string[]) → `MutantRecord.coveredBy[].id`
    * `killedBy` (string[]) → `MutantRecord.killedBy[].id`

### 4.4 Test resolution (optional enrichment)

If `testFiles` exists:

* Build an index of `testId -> testName` from all `testFiles[*].tests[]`.
* For each mutant:

    * `coveredBy` ids are output as `{id, name}` where `name` is resolved from the index or `null` if not found.
    * `killedBy` ids are output as `{id, name}` where `name` is resolved from the index or `null` if not found.

If `testFiles` is absent:

* Output `{id, name:null}` for each referenced id.

---

## 5) CLI commands

All commands support:

* `--report <path>` (optional)
* `--format json|text|table` (where applicable)
* `--pretty` (pretty-print JSON; default false)

In `--format json` mode:

* stdout MUST contain only JSON
* stderr MUST contain only diagnostics
* exit code signals success/failure

### 5.1 `summary`

```
stryker-agent summary [--report <path>] [--format json|text] [--pretty]
```

Outputs `RunSummary`.

Default format: `json`.

### 5.2 `files`

```
stryker-agent files
  [--report <path>]
  [--status <Status>...]
  [--sort path|score|survived|mutants]
  [--limit <n>]
  [--format json|table]
  [--pretty]
```

Behavior:

* Returns `FileSummary[]`.
* `--status` filters to files having at least one mutant with any of the given statuses.
* Default sort: `path`.

### 5.3 `mutants`

```
stryker-agent mutants
  [--report <path>]
  [--status <Status>...]
  [--file <glob>...]
  [--mutator <name>...]
  [--limit <n>] [--offset <n>]
  [--format json|table]
  [--pretty]
```

Behavior:

* Returns `MutantRecord[]`.
* Filters are conjunctive across groups (status AND file AND mutator).
* Within each group, values are disjunctive (any status / any glob / any mutator).
* Default ordering: `filePath`, `location.startLine`, `id`.

### 5.4 `mutant get`

```
stryker-agent mutant get <id>
  [--report <path>]
  [--format json|text]
  [--pretty]
```

Behavior:

* Returns one mutant by `id`.
* Includes context derived from the report’s file `source`.

JSON output:

```json
{
  "mutant": { /* MutantRecord */ },
  "context": {
    "filePath": "string",
    "language": "string",
    "source": "string",
    "mutantLocation": { "startLine": 1, "startColumn": 1, "endLine": 1, "endColumn": 1 }
  }
}
```

### 5.5 `export`

```
stryker-agent export
  [--report <path>]
  --out <dir>
  [--mode jsonl|sqlite]
  [--overwrite]
```

Behavior:

* Writes agent-ready artifacts for downstream indexing:

    * `summary.json`
    * `files.jsonl` (one `FileSummary` per line)
    * `mutants.jsonl` (one `MutantRecord` per line)
* In `sqlite` mode, creates `stryker-report.sqlite` with normalized tables mirroring these records.

---

## 6) Output determinism

Given the same input report, outputs MUST be identical (except for optional fields that reflect runtime environment,
which SHOULD be omitted by default).

* Default ordering:

    * `files`: sorted by `filePath` unless `--sort` provided.
    * `mutants`: sorted by `filePath`, `location.startLine`, `id` unless pagination changes the slice.

---

## 7) Error handling and exit codes

* `0`: success
* `2`: invalid arguments / usage error
* `3`: report not found (no `--report` match and discovery failed)
* `4`: report parse/schema error (invalid JSON or missing required fields)
* `5`: requested entity not found (e.g., mutant id missing)
* `6`: export destination exists without `--overwrite`
* `7`: unexpected internal error

All errors:

* write a concise message to stderr
* must not emit non-JSON to stdout in `--format json` mode.
