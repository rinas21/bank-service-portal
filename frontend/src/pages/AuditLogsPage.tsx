import { useCallback, useEffect, useMemo, useState } from 'react'
import { ScrollText, Search } from 'lucide-react'
import { auditApi } from '@/lib/apiClient'
import { errorMessage } from '@/lib/api'
import type { AuditAction, AuditLog } from '@/types'
import {
  Button,
  EmptyState,
  ErrorState,
  Field,
  LoadingState,
  Pagination,
  Select,
  TextInput,
} from '@/components/ui'

const ACTIONS: AuditAction[] = [
  'UserCreated',
  'UserUpdated',
  'UserDeactivated',
  'BranchCreated',
  'BranchUpdated',
  'RequestCreated',
  'RequestUpdated',
  'RequestAssigned',
  'StatusChanged',
  'CommentAdded',
  'ApprovalRequested',
  'ApprovalGranted',
  'ApprovalRejected',
  'CsvImport',
  'Login',
  'LoginFailed',
]

const ACTION_STYLES: Partial<Record<AuditAction, string>> = {
  UserCreated: 'bg-brand-100 text-brand-700',
  UserUpdated: 'bg-brand-100 text-brand-700',
  UserDeactivated: 'bg-amber-100 text-amber-800',
  BranchCreated: 'bg-brand-100 text-brand-700',
  BranchUpdated: 'bg-brand-100 text-brand-700',
  RequestCreated: 'bg-blue-100 text-blue-800',
  RequestUpdated: 'bg-blue-100 text-blue-800',
  RequestAssigned: 'bg-indigo-100 text-indigo-800',
  StatusChanged: 'bg-violet-100 text-violet-800',
  CommentAdded: 'bg-ink-200 text-ink-700',
  ApprovalRequested: 'bg-amber-100 text-amber-800',
  ApprovalGranted: 'bg-emerald-100 text-emerald-800',
  ApprovalRejected: 'bg-red-100 text-red-800',
  CsvImport: 'bg-ink-900 text-white',
  Login: 'bg-emerald-100 text-emerald-800',
  LoginFailed: 'bg-red-100 text-red-800',
}

function formatDateTime(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

export default function AuditLogsPage() {
  const [logs, setLogs] = useState<AuditLog[]>([])
  const [search, setSearch] = useState('')
  const [searchDraft, setSearchDraft] = useState('')
  const [action, setAction] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 20

  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  const [error, setError] = useState('')
  // Key of the query whose results are currently displayed. Loading is derived
  // from it rather than toggled by hand, so an interaction that does not change
  // the query cannot leave a spinner running with nothing to finish it.
  const [loadedKey, setLoadedKey] = useState<string | null>(null)

  const queryKey = useMemo(
    () => JSON.stringify([search, action, from, to, page]),
    [search, action, from, to, page],
  )
  const isLoading = loadedKey !== queryKey

  const load = useCallback(
    (signal?: AbortSignal) => {
      auditApi
        .list(
          {
            search: search || undefined,
            action: action as AuditAction | '',
            from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
            to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
            page,
            pageSize,
          },
          signal,
        )
        .then((result) => {
          setLogs(result.items)
          setTotalCount(result.totalCount)
          setTotalPages(result.totalPages)
        })
        .catch((err) => {
          if ((err as Error).name === 'AbortError') return
          setError(errorMessage(err, 'We could not load the audit log.'))
        })
        .finally(() => setLoadedKey(queryKey))
    },
    [search, action, from, to, page, queryKey],
  )

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal)
    return () => controller.abort()
  }, [load])

  // Re-runs the fetch for the current query. Called for retry-after-error and
  // after mutations; a no-op filter change still refetches because the request
  // is issued directly rather than inferred from a dependency change.
  function refresh() {
    setError('')
    load()
  }

  function applyFilters() {
    setError('')
    setPage(1)
    setSearch(searchDraft)
  }

  function changeFilter(update: () => void) {
    setError('')
    setPage(1)
    update()
  }

  function resetFilters() {
    setError('')
    setSearchDraft('')
    setSearch('')
    setAction('')
    setFrom('')
    setTo('')
    setPage(1)
  }

  return (
    <div className="space-y-5">
      <header>
        <h1 className="text-2xl font-bold text-ink-900">Audit log</h1>
        <p className="mt-1 text-sm text-ink-500">
          Every sign-in and change recorded for compliance review.
        </p>
      </header>

      <div className="card p-4">
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(event) => {
            event.preventDefault()
            applyFilters()
          }}
        >
          <div className="min-w-[14rem] flex-1">
            <Field label="Search" htmlFor="audit-search">
              <TextInput
                id="audit-search"
                placeholder="User, entity, entity id or details"
                value={searchDraft}
                onChange={(event) => setSearchDraft(event.target.value)}
              />
            </Field>
          </div>
          <Field label="Action" htmlFor="audit-action">
            <Select
              id="audit-action"
              className="w-48"
              value={action}
              onChange={(event) => changeFilter(() => setAction(event.target.value))}
            >
              <option value="">Any action</option>
              {ACTIONS.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="From" htmlFor="audit-from">
            <TextInput
              id="audit-from"
              type="date"
              value={from}
              onChange={(event) => changeFilter(() => setFrom(event.target.value))}
            />
          </Field>
          <Field label="To" htmlFor="audit-to">
            <TextInput
              id="audit-to"
              type="date"
              value={to}
              onChange={(event) => changeFilter(() => setTo(event.target.value))}
            />
          </Field>
          <Button type="submit" variant="secondary">
            <Search className="size-4" aria-hidden="true" />
            Apply
          </Button>
          <Button
            variant="ghost"
            onClick={resetFilters}
          >
            Reset
          </Button>
        </form>
      </div>

      <section className="card overflow-hidden" aria-label="Audit entries">
        {isLoading ? (
          <LoadingState label="Loading audit log…" />
        ) : error ? (
          <ErrorState message={error} onRetry={refresh} />
        ) : logs.length === 0 ? (
          <EmptyState
            icon={ScrollText}
            title="No audit entries match your filters"
            description="Try widening the date range or clearing the action filter."
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead className="bg-ink-50 text-xs uppercase tracking-wide text-ink-500">
                  <tr>
                    <th scope="col" className="px-5 py-3 font-semibold">Timestamp</th>
                    <th scope="col" className="px-5 py-3 font-semibold">User</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Action</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Entity</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Details</th>
                    <th scope="col" className="px-5 py-3 font-semibold">IP</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-ink-200">
                  {logs.map((log) => (
                    <tr key={log.id} className="align-top hover:bg-ink-50">
                      <td className="whitespace-nowrap px-5 py-3 text-xs text-ink-600">
                        {formatDateTime(log.timestamp)}
                      </td>
                      <td className="px-5 py-3">
                        <p className="font-medium text-ink-900">{log.userName ?? 'System'}</p>
                        {log.userId && <p className="font-mono text-[10px] text-ink-400">{log.userId}</p>}
                      </td>
                      <td className="px-5 py-3">
                        <span className={`badge ${ACTION_STYLES[log.action] ?? 'bg-ink-200 text-ink-700'}`}>
                          {log.action}
                        </span>
                      </td>
                      <td className="px-5 py-3 text-xs text-ink-700">
                        <p className="font-medium">{log.entityType}</p>
                        {log.entityId && <p className="font-mono text-ink-500">{log.entityId}</p>}
                      </td>
                      <td className="max-w-sm px-5 py-3 text-xs text-ink-600">{log.details ?? '—'}</td>
                      <td className="whitespace-nowrap px-5 py-3 font-mono text-xs text-ink-500">
                        {log.ipAddress ?? '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <Pagination
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              totalPages={totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </section>
    </div>
  )
}
