# Legacy request CSV migration

The portal ships with two CSV import paths that share the same validation and
persistence logic in `BankService.Infrastructure/Services/CsvImportService.cs`:

| Path | Use it for |
| --- | --- |
| `POST /api/migration/import` (Admin role) | Normal operation from the **CSV migration** page in the UI |
| `BankService.CsvImporter` | One-off or bulk imports, and environments where you want to run the import without the API |

Both paths require the same columns and produce the same result shape, so you
can test with one and run with the other.

## 1. Column format

The header row is mandatory and must contain every column below. Column order
does not matter, and the check is case-insensitive.

| Column | Required | Accepted values |
| --- | --- | --- |
| `LegacyRef` | yes | Any unique, non-empty identifier from the legacy system |
| `Title` | yes | Up to 200 characters |
| `Description` | yes | Up to 4000 characters |
| `Category` | yes | Up to 100 characters |
| `Priority` | yes | `Low`, `Medium`, `High`, `Critical` (also accepts `L`, `M`, `Med`, `H`, `C`, `Crit`) |
| `Status` | yes | `Open`, `InProgress`, `Resolved`, `Closed` (also accepts `O`, `IP`, `WIP`, `In progress`, `R`, `Done`, `Complete`, `CL`) |
| `BranchCode` | yes | Must match an existing branch `Code`, e.g. `NY02`. Unknown codes are rejected |
| `RequesterEmail` | yes | Must match an existing user's email. Unknown emails are rejected |
| `DueDate` | no | ISO date, e.g. `2026-11-15`. Blank means no due date |

Values containing commas or quotes are parsed as standard RFC 4180 CSV, so
quote those fields.

## 2. Sample files

- `database/csv/sample-legacy-requests.csv` — 10 valid rows, safe to import.
- `database/csv/sample-legacy-requests-with-errors.csv` — deliberately mixes
  duplicates, an unknown requester and an unknown branch so you can see how
  each failure is reported.

## 3. Importing from the UI

1. Sign in as an Admin (for example `admin@bankportal.com`).
2. Open **CSV migration** in the sidebar.
3. Drop the file in (or click to choose it), then click **Run import**.
4. Read the summary — total, imported, duplicates, rejected — and the per-row
   list underneath.

## 4. Importing from the API

```
TOKEN=$(curl -s -X POST http://localhost:8081/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@bankportal.com","password":"Password@123"}' \
  | jq -r .token)

curl -X POST http://localhost:8081/api/migration/import \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@database/csv/sample-legacy-requests.csv"
```

Response shape:

```json
{
  "totalRecords": 10,
  "imported": 8,
  "duplicates": 1,
  "rejected": 1,
  "rowResults": [
    { "rowNumber": 2, "status": "Imported", "legacyReference": "LEGACY-0001", "message": "Created as SR-2026-00036." },
    { "rowNumber": 3, "status": "Duplicate", "legacyReference": "LEGACY-0001", "message": "Legacy reference already exists." },
    { "rowNumber": 4, "status": "Rejected", "legacyReference": "LEGACY-0020", "message": "Requester 'nobody@bankportal.com' was not found." }
  ]
}
```

The endpoint requires the `Admin` role; other roles receive `403`.

## 5. Importing with the standalone CLI

The CLI connects straight to SQL Server, which is useful when you want to run
the import before the API is up, or against a restored backup.

```
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=BankServicePortal;User Id=sa;Password=<password>;TrustServerCertificate=True"

# Run from the repository root so the sample path resolves.
dotnet run --project BankService.CsvImporter -- database/csv/sample-legacy-requests.csv
```

The identity used for the audit entry comes from `Importer:UserId` and
`Importer:UserName` in `BankService.CsvImporter/appsettings.json` (override with
`Importer__UserId` / `Importer__UserName`).

## 6. How the import behaves

- **Validate, then write.** Every row is validated before anything is inserted,
  so a single bad row never leaves a half-applied import behind.
- **Request numbers are reissued.** Imported rows get a fresh sequential number
  (`SR-YYYY-NNNNN`); the `LegacyRef` is preserved in `LegacyReference` for
  traceability and is what future imports deduplicate on.
- **Duplicates are skipped, never inserted.** A `LegacyRef` that already exists
  in the database, or that repeats within the same file, is reported as
  `Duplicate`.
- **Unknown references are rejected.** Rows pointing at a missing requester or
  branch code are reported with the exact value that could not be resolved.
- **Malformed rows are rejected.** A row whose column count does not match the
  header is rejected rather than silently mis-mapped.
- **Import is audited.** A single `CsvImport` audit entry records the totals, so
  the Admin audit log always shows what was imported.

## 7. Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `CSV is missing required columns: ...` | Header misspelled or absent | Use the exact column names from the table above |
| Every row reported as `Duplicate` | The sample file was already imported | Expected on a second run; check the audit log |
| Rows rejected for unknown requester | The email does not exist yet | Create the user first (Admin → Users), then re-import |
| Rows rejected for unknown branch | The `BranchCode` is not in the DB | Create the branch (Admin → Branches) and use its `Code` |
| `Login` returns `429` while testing | Sign-in rate limit (10 per minute, per client IP) | Wait a minute, or retry through a different forwarded client address |
