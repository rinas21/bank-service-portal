import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  ArrowDownUp,
  ClipboardList,
  Filter,
  MessageSquare,
  Plus,
  Search,
  X,
} from 'lucide-react'
import { branchApi, requestApi } from '@/lib/apiClient'
import type { Branch, RequestPriority, RequestStatus, ServiceRequestSummary } from '@/types'
import { errorMessage } from '@/lib/api'
import {
  Button,
  EmptyState,
  ErrorState,
  Field,
  LoadingState,
  Pagination,
  PriorityBadge,
  Select,
  StatusBadge,
  TextInput,
} from '@/components/ui'

const STATUSES: RequestStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed']
const PRIORITIES: RequestPriority[] = ['Low', 'Medium', 'High', 'Critical']

const SORTABLE_COLUMNS = [
  { value: '', label: 'Newest first' },
  { value: 'requestNumber', label: 'Request number' },
  { value: 'title', label: 'Title' },
  { value: 'status', label: 'Status' },
  { value: 'priority', label: 'Priority' },
  { value: 'createdAt', label: 'Created date' },
  { value: 'dueDate', label: 'Due date' },
]

function formatDate(value: string | null) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString()
}

export default function RequestsPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const [requests, setRequests] = useState<ServiceRequestSummary[]>([])
  const [branches, setBranches] = useState<Branch[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  const [error, setError] = useState('')
  const [showFilters, setShowFilters] = useState(false)
  // Key of the query whose results are currently displayed. Loading is derived
  // from it rather than toggled by hand, so an interaction that does not change
  // the query cannot leave a spinner running with nothing to finish it.
  const [loadedKey, setLoadedKey] = useState<string | null>(null)

  const page = Number(searchParams.get('page') ?? '1')
  const pageSize = Number(searchParams.get('pageSize') ?? '10')
  // Search is applied on submit and kept in local state so typing does not
  // trigger a request or push a history entry on every keystroke.
  const [search, setSearch] = useState('')
  const [searchDraft, setSearchDraft] = useState('')
  const status = (searchParams.get('status') ?? '') as RequestStatus | ''
  const priority = (searchParams.get('priority') ?? '') as RequestPriority | ''
  const category = searchParams.get('category') ?? ''
  const branchId = searchParams.get('branchId') ?? ''
  const requiresApproval = searchParams.get('requiresApproval') ?? ''
  const sortBy = searchParams.get('sortBy') ?? ''
  const descending = searchParams.get('descending') !== 'false'

  const queryKey = useMemo(
    () =>
      JSON.stringify([
        search,
        status,
        priority,
        category,
        branchId,
        requiresApproval,
        sortBy,
        descending,
        page,
        pageSize,
      ]),
    [
      search,
      status,
      priority,
      category,
      branchId,
      requiresApproval,
      sortBy,
      descending,
      page,
      pageSize,
    ],
  )
  const isLoading = loadedKey !== queryKey

  const activeFilterCount = useMemo(
    () => [status, priority, category, branchId, requiresApproval].filter(Boolean).length,
    [status, priority, category, branchId, requiresApproval],
  )

  function updateParams(changes: Record<string, string | null>, resetPage = true) {
    setError('')

    const next = new URLSearchParams(searchParams)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '') next.delete(key)
      else next.set(key, value)
    }
    if (resetPage) next.delete('page')
    // Re-selecting a filter at its current value leaves the URL untouched, so the
    // reload is requested explicitly rather than inferred from a URL change.
    if (next.toString() === searchParams.toString()) refresh()
    else setSearchParams(next, { replace: false })
  }

  const load = useCallback(
    (signal?: AbortSignal) => {
      requestApi
        .list(
          {
            search: search || undefined,
            status,
            priority,
            category: category || undefined,
            branchId: branchId ? Number(branchId) : '',
            requiresApproval: requiresApproval === '' ? '' : requiresApproval === 'true',
            sortBy: sortBy || undefined,
            descending,
            page,
            pageSize,
          },
          signal,
        )
        .then((result) => {
          setRequests(result.items)
          setTotalCount(result.totalCount)
          setTotalPages(result.totalPages)
        })
        .catch((err) => {
          if ((err as Error).name === 'AbortError') return
          setError(errorMessage(err, 'We could not load the requests.'))
        })
        .finally(() => setLoadedKey(queryKey))
    },
    [search, status, priority, category, branchId, requiresApproval, sortBy, descending, page, pageSize, queryKey],
  )

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal)
    return () => controller.abort()
  }, [load])

  // Re-runs the fetch for the current query. Called for retry-after-error, after
  // mutations, and when a filter interaction produces no URL change.
  function refresh() {
    setError('')
    load()
  }

  useEffect(() => {
    const controller = new AbortController()
    branchApi
      .list({ pageSize: 100 }, controller.signal)
      .then((result) => setBranches(result.items))
      .catch(() => setBranches([]))
    return () => controller.abort()
  }, [])

  function clearFilters() {
    setError('')
    setSearchDraft('')
    setSearch('')
    const next = new URLSearchParams()
    if (pageSize !== 10) next.set('pageSize', String(pageSize))
    if (next.toString() === searchParams.toString()) refresh()
    else setSearchParams(next)
  }

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-ink-900">Service requests</h1>
          <p className="mt-1 text-sm text-ink-500">Search, filter and sort every request you can access.</p>
        </div>
        <Link to="/requests/new" className="btn-primary">
          <Plus className="size-4" aria-hidden="true" />
          New request
        </Link>
      </header>

      <div className="card p-4">
        <div className="flex flex-wrap items-center gap-3">
          <form
            className="relative min-w-[16rem] flex-1"
            onSubmit={(event) => {
              event.preventDefault()
              setError('')
              setSearch(searchDraft)
              // The term may already be applied, leaving nothing to change and
              // so nothing that would trigger the fetch effect.
              if (searchDraft === search) refresh()
            }}
          >
            <Search
              className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-400"
              aria-hidden="true"
            />
            <TextInput
              className="pl-9"
              placeholder="Search by number, title or description"
              aria-label="Search requests"
              value={searchDraft}
              onChange={(event) => setSearchDraft(event.target.value)}
            />
          </form>

          <Button
            variant="secondary"
            onClick={() => setShowFilters((open) => !open)}
            aria-expanded={showFilters}
          >
            <Filter className="size-4" aria-hidden="true" />
            Filters
            {activeFilterCount > 0 && (
              <span className="ml-1 rounded-full bg-brand-600 px-1.5 text-xs text-white">{activeFilterCount}</span>
            )}
          </Button>

          <Select
            className="w-auto"
            aria-label="Sort requests"
            value={sortBy}
            onChange={(event) => updateParams({ sortBy: event.target.value })}
          >
            {SORTABLE_COLUMNS.map((column) => (
              <option key={column.value} value={column.value}>
                {column.label}
              </option>
            ))}
          </Select>

          <Button
            variant="secondary"
            onClick={() => updateParams({ descending: String(!descending) }, false)}
            title={descending ? 'Descending' : 'Ascending'}
          >
            <ArrowDownUp className="size-4" aria-hidden="true" />
            {descending ? 'Desc' : 'Asc'}
          </Button>
        </div>

        {showFilters && (
          <div className="mt-4 grid gap-4 border-t border-ink-200 pt-4 sm:grid-cols-2 lg:grid-cols-3">
            <Field label="Status" htmlFor="filter-status">
              <Select
                id="filter-status"
                value={status}
                onChange={(event) => updateParams({ status: event.target.value })}
              >
                <option value="">Any status</option>
                {STATUSES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Priority" htmlFor="filter-priority">
              <Select
                id="filter-priority"
                value={priority}
                onChange={(event) => updateParams({ priority: event.target.value })}
              >
                <option value="">Any priority</option>
                {PRIORITIES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Category" htmlFor="filter-category">
              <TextInput
                id="filter-category"
                placeholder="e.g. Account Services"
                value={category}
                onChange={(event) => updateParams({ category: event.target.value })}
              />
            </Field>

            <Field label="Branch" htmlFor="filter-branch">
              <Select
                id="filter-branch"
                value={branchId}
                onChange={(event) => updateParams({ branchId: event.target.value })}
              >
                <option value="">Any branch</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Approval" htmlFor="filter-approval">
              <Select
                id="filter-approval"
                value={requiresApproval}
                onChange={(event) => updateParams({ requiresApproval: event.target.value })}
              >
                <option value="">Any</option>
                <option value="true">Requires approval</option>
                <option value="false">No approval needed</option>
              </Select>
            </Field>

            <div className="flex items-end">
              <Button variant="ghost" onClick={clearFilters}>
                <X className="size-4" aria-hidden="true" />
                Clear filters
              </Button>
            </div>
          </div>
        )}
      </div>

      <section className="card overflow-hidden" aria-label="Request list">
        {isLoading ? (
          <LoadingState label="Loading requests…" />
        ) : error ? (
          <ErrorState message={error} onRetry={refresh} />
        ) : requests.length === 0 ? (
          <EmptyState
            icon={ClipboardList}
            title="No requests match your filters"
            description="Try widening the search, or create a new service request."
            action={
              activeFilterCount > 0 || search ? (
                <Button variant="secondary" className="mt-2" onClick={clearFilters}>
                  Clear filters
                </Button>
              ) : (
                <Link to="/requests/new" className="btn-primary mt-2">
                  <Plus className="size-4" aria-hidden="true" />
                  New request
                </Link>
              )
            }
          />
        ) : (
          <>
            {/* Desktop table */}
            <div className="hidden overflow-x-auto lg:block">
              <table className="w-full text-left text-sm">
                <thead className="bg-ink-50 text-xs uppercase tracking-wide text-ink-500">
                  <tr>
                    <th scope="col" className="px-5 py-3 font-semibold">Request</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Requester</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Status</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Priority</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Assignee</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Due</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Comments</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-ink-200">
                  {requests.map((request) => (
                    <tr key={request.id} className="hover:bg-ink-50">
                      <td className="px-5 py-3">
                        <Link to={`/requests/${request.id}`} className="font-medium text-brand-700 hover:underline">
                          {request.title}
                        </Link>
                        <p className="font-mono text-xs text-ink-500">{request.requestNumber}</p>
                        <p className="text-xs text-ink-500">{request.category}</p>
                      </td>
                      <td className="px-5 py-3 text-ink-700">{request.requesterName}</td>
                      <td className="px-5 py-3">
                        <div className="flex flex-wrap items-center gap-1.5">
                          <StatusBadge status={request.status} />
                          {request.requiresApproval && (
                            <span className="badge bg-violet-100 text-violet-700">Approval</span>
                          )}
                          {request.isMigrated && (
                            <span className="badge bg-ink-200 text-ink-700">Migrated</span>
                          )}
                        </div>
                      </td>
                      <td className="px-5 py-3">
                        <PriorityBadge priority={request.priority} />
                      </td>
                      <td className="px-5 py-3 text-ink-700">{request.assignedToName ?? '—'}</td>
                      <td className="px-5 py-3 text-ink-700">{formatDate(request.dueDate)}</td>
                      <td className="px-5 py-3 text-ink-700">
                        <span className="inline-flex items-center gap-1">
                          <MessageSquare className="size-3.5 text-ink-400" aria-hidden="true" />
                          {request.commentCount}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile cards */}
            <ul className="divide-y divide-ink-200 lg:hidden">
              {requests.map((request) => (
                <li key={request.id}>
                  <Link to={`/requests/${request.id}`} className="block px-4 py-4 hover:bg-ink-50">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate font-medium text-ink-900">{request.title}</p>
                        <p className="font-mono text-xs text-ink-500">{request.requestNumber}</p>
                      </div>
                      <StatusBadge status={request.status} />
                    </div>
                    <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-ink-600">
                      <PriorityBadge priority={request.priority} />
                      <span>{request.category}</span>
                      <span>· {request.requesterName}</span>
                      {request.dueDate && <span>· due {formatDate(request.dueDate)}</span>}
                    </div>
                  </Link>
                </li>
              ))}
            </ul>

            <Pagination
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              totalPages={totalPages}
              onPageChange={(next) => updateParams({ page: String(next) }, false)}
            />
          </>
        )}
      </section>
    </div>
  )
}
