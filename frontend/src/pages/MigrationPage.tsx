import { useRef, useState } from 'react'
import { CheckCircle2, FileUp, Upload, XCircle, X } from 'lucide-react'
import { migrationApi } from '@/lib/apiClient'
import { errorMessage } from '@/lib/api'
import { useToast } from '@/context/toastContext'
import type { CsvImportResult } from '@/types'
import { Button, Field } from '@/components/ui'

const MAX_FILE_SIZE_BYTES = 2 * 1024 * 1024

export default function MigrationPage() {
  const inputRef = useRef<HTMLInputElement>(null)
  const { showToast } = useToast()

  const [file, setFile] = useState<File | null>(null)
  const [result, setResult] = useState<CsvImportResult | null>(null)
  const [error, setError] = useState('')
  const [isUploading, setIsUploading] = useState(false)
  const [isDragging, setIsDragging] = useState(false)

  function acceptFile(candidate: File | undefined) {
    setError('')
    setResult(null)

    if (!candidate) return

    if (!candidate.name.toLowerCase().endsWith('.csv')) {
      setError('Only .csv files are accepted.')
      return
    }

    if (candidate.size > MAX_FILE_SIZE_BYTES) {
      setError('The file is larger than 2 MB. Please split it and try again.')
      return
    }

    setFile(candidate)
  }

  async function handleImport() {
    if (!file) return

    setIsUploading(true)
    setError('')
    try {
      const outcome = await migrationApi.importLegacyRequests(file)
      setResult(outcome)
      showToast(
        `Imported ${outcome.imported} of ${outcome.totalRecords} records.`,
        outcome.rejected > 0 ? 'info' : 'success',
      )
    } catch (err) {
      setError(errorMessage(err, 'The import could not be completed.'))
    } finally {
      setIsUploading(false)
    }
  }

  function reset() {
    setFile(null)
    setResult(null)
    setError('')
    if (inputRef.current) inputRef.current.value = ''
  }

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <header>
        <h1 className="text-2xl font-bold text-ink-900">CSV migration</h1>
        <p className="mt-1 text-sm text-ink-500">
          Import legacy service requests. Rows are validated first, then inserted with newly generated request
          numbers. Duplicate legacy references are skipped safely.
        </p>
      </header>

      <div className="card p-5">
        <div
          className={`rounded-xl border-2 border-dashed p-8 text-center transition-colors ${
            isDragging ? 'border-brand-500 bg-brand-50' : 'border-ink-300 bg-ink-50'
          }`}
          onDragOver={(event) => {
            event.preventDefault()
            setIsDragging(true)
          }}
          onDragLeave={() => setIsDragging(false)}
          onDrop={(event) => {
            event.preventDefault()
            setIsDragging(false)
            acceptFile(event.dataTransfer.files?.[0])
          }}
        >
          <input
            ref={inputRef}
            type="file"
            accept=".csv,text/csv"
            className="sr-only"
            id="csv-file"
            onChange={(event) => acceptFile(event.target.files?.[0])}
          />

          {file ? (
            <div className="flex flex-col items-center gap-3">
              <span className="flex size-12 items-center justify-center rounded-full bg-brand-100 text-brand-700">
                <FileUp className="size-6" aria-hidden="true" />
              </span>
              <p className="font-semibold text-ink-900">{file.name}</p>
              <p className="text-sm text-ink-500">{(file.size / 1024).toFixed(1)} KB</p>
              <div className="flex gap-2">
                <Button onClick={handleImport} isLoading={isUploading}>
                  <Upload className="size-4" aria-hidden="true" />
                  {isUploading ? 'Importing…' : 'Run import'}
                </Button>
                <Button variant="secondary" onClick={reset} disabled={isUploading}>
                  <X className="size-4" aria-hidden="true" />
                  Clear
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex flex-col items-center gap-3">
              <span className="flex size-12 items-center justify-center rounded-full bg-ink-200 text-ink-500">
                <Upload className="size-6" aria-hidden="true" />
              </span>
              <p className="font-semibold text-ink-900">Drag a CSV file here</p>
              <p className="text-sm text-ink-500">or</p>
              <label htmlFor="csv-file" className="btn-secondary cursor-pointer">
                Choose a file
              </label>
              <p className="text-xs text-ink-500">Maximum 2 MB. See the required columns below.</p>
            </div>
          )}
        </div>

        <div className="mt-5 rounded-lg border border-ink-200 bg-white p-4">
          <h2 className="text-sm font-semibold text-ink-900">Required columns</h2>
          <p className="mt-1 text-xs text-ink-600">
            The header row must contain these columns (order does not matter):
          </p>
          <ul className="mt-2 grid gap-1 font-mono text-xs text-ink-700 sm:grid-cols-2">
            <li>LegacyRef</li>
            <li>Title</li>
            <li>Description</li>
            <li>Category</li>
            <li>Priority</li>
            <li>Status</li>
            <li>BranchCode</li>
            <li>RequesterEmail</li>
            <li>DueDate</li>
          </ul>
          <p className="mt-2 text-xs text-ink-500">
            Sample files live in <span className="font-mono">database/csv/</span>.
          </p>
        </div>

        {error && (
          <div
            className="mt-4 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-800"
            role="alert"
          >
            <XCircle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>{error}</span>
          </div>
        )}
      </div>

      {result && (
        <div className="card" aria-label="Import result">
          <div className="card-header">
            <h2 className="text-sm font-semibold text-ink-900">Import result</h2>
            <span className="text-xs text-ink-500">{file?.name}</span>
          </div>

          <div className="grid gap-4 border-b border-ink-200 p-5 sm:grid-cols-4">
            {[
              { label: 'Total rows', value: result.totalRecords, tone: 'text-ink-900' },
              { label: 'Imported', value: result.imported, tone: 'text-emerald-700' },
              { label: 'Duplicates', value: result.duplicates, tone: 'text-amber-700' },
              { label: 'Rejected', value: result.rejected, tone: 'text-red-700' },
            ].map((stat) => (
              <div key={stat.label}>
                <p className="text-xs font-semibold uppercase tracking-wide text-ink-500">{stat.label}</p>
                <p className={`mt-1 text-2xl font-bold ${stat.tone}`}>{stat.value}</p>
              </div>
            ))}
          </div>

          <ul className="divide-y divide-ink-200">
            {result.rowResults.map((row) => (
              <li key={row.rowNumber} className="flex flex-wrap items-start gap-3 px-5 py-3">
                <span className="w-16 shrink-0 font-mono text-xs text-ink-500">Row {row.rowNumber}</span>
                {row.status === 'Imported' ? (
                  <span className="badge bg-emerald-100 text-emerald-800">
                    <CheckCircle2 className="size-3" aria-hidden="true" />
                    Imported
                  </span>
                ) : row.status === 'Duplicate' ? (
                  <span className="badge bg-amber-100 text-amber-800">Duplicate</span>
                ) : (
                  <span className="badge bg-red-100 text-red-800">
                    <XCircle className="size-3" aria-hidden="true" />
                    Rejected
                  </span>
                )}
                <span className="min-w-0 flex-1 text-sm text-ink-700">{row.message}</span>
                {row.legacyReference && (
                  <span className="font-mono text-xs text-ink-400">{row.legacyReference}</span>
                )}
              </li>
            ))}
          </ul>

          <div className="flex justify-end border-t border-ink-200 bg-ink-50 px-5 py-3">
            <Button variant="secondary" onClick={reset}>
              Import another file
            </Button>
          </div>
        </div>
      )}

      <div className="card p-5">
        <h2 className="text-sm font-semibold text-ink-900">How the import behaves</h2>
        <ul className="mt-2 space-y-1.5 text-sm text-ink-600">
          <li>
            <span className="font-semibold text-ink-800">Validation first.</span> Every row is checked before
            anything is written, so a bad row never leaves a partial import.
          </li>
          <li>
            <span className="font-semibold text-ink-800">Request numbers are reissued.</span> Imported rows get
            fresh sequential numbers; the legacy reference is kept for traceability.
          </li>
          <li>
            <span className="font-semibold text-ink-800">Duplicates are skipped.</span> A legacy reference that
            already exists, or repeats inside the same file, is reported rather than inserted.
          </li>
          <li>
            <span className="font-semibold text-ink-800">Unknown values are rejected.</span> Rows referencing an
            unknown requester or branch code are listed with the reason.
          </li>
          <li>
            <span className="font-semibold text-ink-800">Everything is audited.</span> The import is recorded in
            the audit log with a CSV import action.
          </li>
        </ul>

        <div className="mt-4 border-t border-ink-200 pt-4">
          <Field label="Test the endpoint directly" htmlFor="curl-hint">
            <code
              id="curl-hint"
              className="block overflow-x-auto rounded-lg bg-ink-900 px-4 py-3 text-xs text-ink-100"
            >
              curl -X POST http://localhost:8081/api/migration/import -H &quot;Authorization: Bearer $TOKEN&quot; -F
              &quot;file=@database/csv/sample-legacy-requests.csv&quot;
            </code>
          </Field>
        </div>
      </div>
    </div>
  )
}
