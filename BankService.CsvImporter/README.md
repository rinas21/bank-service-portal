# CSV Importer (standalone)

Imports legacy service requests into SQL Server without running the API.

## Usage

```
dotnet run --project BankService.CsvImporter -- <path-to-csv>
```

The connection string is read from `ConnectionStrings:DefaultConnection` in
`appsettings.json` or from the environment:

```
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=BankServicePortal;User Id=sa;Password=...;TrustServerCertificate=True"
dotnet run --project BankService.CsvImporter -- database/csv/sample-legacy-requests.csv
```

The CSV format is identical to the in-app importer at `POST /api/migration/import`.
