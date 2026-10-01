import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardList,
  Clock,
  FolderTree,
  Hourglass,
  ListTodo,
  Plus,
} from 'lucide-react'
import { dashboardApi } from '@/lib/apiClient'
import { useAuth } from '@/context/authContext'
import { errorMessage } from '@/lib/api'
import type { DashboardStats, RequestStatus } from '@/types'
import {
  EmptyState,
  ErrorState,
  LoadingState,
  PriorityBadge,
  StatusBadge,
} from '@/components/ui'

const STATUS_BAR_COLORS: Record<RequestStatus, string> = {
  Open: 'bg-blue-500',
  InProgress: 'bg-amber-500',
  Resolved: 'bg-emerald-500',
  Closed: 'bg-ink-400',
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
}

function StatCard({
  label,
  value,
  icon: Icon,
  tone,
}: {
  label: string
  value: number
  icon: typeof ClipboardList
  tone: string
}) {
  return (
    <div className="card flex items-center gap-4 p-4">
      <span className={`flex size-11 shrink-0 items-center justify-center rounded-lg ${tone}`}>
        <Icon className="size-5" aria-hidden="true" />
      </span>
      <div className="min-w-0">
        <p className="text-2xl font-bold text-ink-900">{value}</p>
        <p className="truncate text-xs font-medium text-ink-500">{label}</p>
      </div>
    </div>
  )
}

export default function DashboardPage() {
  const { user, hasRole } = useAuth()
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)

  // The fetch itself never touches state synchronously; callers set the
  // loading flag so no state update happens inside the effect body.
  const load = useCallback((signal?: AbortSignal) => {
    dashboardApi
      .get(signal)
      .then(setStats)
      .catch((err) => {
        if ((err as Error).name === 'AbortError') return
        setError(errorMessage(err, 'We could not load the dashboard.'))
      })
      .finally(() => setIsLoading(false))
  }, [])

  function refresh() {
    setIsLoading(true)
    setError('')
    load()
  }

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal)
    return () => controller.abort()
  }, [load])

  if (isLoading) return <LoadingState label="Loading dashboard…" />

  if (error || !stats) {
    return <ErrorState message={error || 'No dashboard data available.'} onRetry={refresh} />
  }

  const maxStatusCount = Math.max(...stats.requestsByStatus.map((item) => item.count), 1)
  const maxCategoryCount = Math.max(...stats.requestsByCategory.map((item) => item.count), 1)

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-ink-900">
            Welcome back, {user?.fullName.split(' ')[0]}
          </h1>
          <p className="mt-1 text-sm text-ink-500">
            {/* Order matches the API's role precedence: a user holding several
                roles is described by their most privileged one. */}
            {hasRole('Admin', 'Manager')
              ? 'Here is what is happening across the service desk today.'
              : hasRole('Support')
                ? 'Here is what is happening with the requests assigned to you.'
                : 'Track the requests you have submitted.'}
          </p>
        </div>
        <Link to="/requests/new" className="btn-primary">
          <Plus className="size-4" aria-hidden="true" />
          New request
        </Link>
      </header>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4" aria-label="Key figures">
        <StatCard label="Total requests" value={stats.totalRequests} icon={ClipboardList} tone="bg-brand-100 text-brand-700" />
        <StatCard label="Open" value={stats.openRequests} icon={FolderTree} tone="bg-blue-100 text-blue-700" />
        <StatCard label="In progress" value={stats.inProgressRequests} icon={Clock} tone="bg-amber-100 text-amber-700" />
        <StatCard label="Resolved" value={stats.resolvedRequests} icon={CheckCircle2} tone="bg-emerald-100 text-emerald-700" />
        <StatCard label="Critical priority" value={stats.criticalRequests} icon={AlertTriangle} tone="bg-red-100 text-red-700" />
        <StatCard label="Pending approvals" value={stats.pendingApprovals} icon={Hourglass} tone="bg-violet-100 text-violet-700" />
        <StatCard label="Overdue" value={stats.overdueRequests} icon={ListTodo} tone="bg-rose-100 text-rose-700" />
        <StatCard label="Created this week" value={stats.requestsThisWeek} icon={ClipboardList} tone="bg-ink-200 text-ink-700" />
      </section>

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="card" aria-label="Requests by status">
          <div className="card-header">
            <h2 className="text-sm font-semibold text-ink-900">Requests by status</h2>
          </div>
          <div className="space-y-3 p-5">
            {stats.requestsByStatus.map((item) => (
              <div key={item.status}>
                <div className="mb-1 flex items-center justify-between text-sm">
                  <span className="font-medium text-ink-700">{item.status}</span>
                  <span className="font-semibold text-ink-900">{item.count}</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-ink-100">
                  <div
                    className={`h-full rounded-full ${STATUS_BAR_COLORS[item.status]}`}
                    style={{ width: `${Math.round((item.count / maxStatusCount) * 100)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="card" aria-label="Requests by priority">
          <div className="card-header">
            <h2 className="text-sm font-semibold text-ink-900">Requests by priority</h2>
          </div>
          <div className="flex flex-wrap gap-2 p-5">
            {stats.requestsByPriority.map((item) => (
              <div key={item.priority} className="flex items-center gap-2 rounded-lg border border-ink-200 px-3 py-2">
                <PriorityBadge priority={item.priority} />
                <span className="text-sm font-bold text-ink-900">{item.count}</span>
              </div>
            ))}
          </div>
        </section>

        <section className="card" aria-label="Requests by category">
          <div className="card-header">
            <h2 className="text-sm font-semibold text-ink-900">Top categories</h2>
          </div>
          <div className="space-y-3 p-5">
            {stats.requestsByCategory.length === 0 ? (
              <p className="text-sm text-ink-500">No categorised requests yet.</p>
            ) : (
              stats.requestsByCategory.slice(0, 6).map((item) => (
                <div key={item.category}>
                  <div className="mb-1 flex items-center justify-between text-sm">
                    <span className="font-medium text-ink-700">{item.category}</span>
                    <span className="font-semibold text-ink-900">{item.count}</span>
                  </div>
                  <div className="h-1.5 overflow-hidden rounded-full bg-ink-100">
                    <div
                      className="h-full rounded-full bg-brand-500"
                      style={{ width: `${Math.round((item.count / maxCategoryCount) * 100)}%` }}
                    />
                  </div>
                </div>
              ))
            )}
          </div>
        </section>

        <section className="card" aria-label="Recent requests">
          <div className="card-header">
            <h2 className="text-sm font-semibold text-ink-900">Recent requests</h2>
            <Link to="/requests" className="text-xs font-semibold text-brand-600 hover:text-brand-700">
              View all
            </Link>
          </div>
          {stats.recentRequests.length === 0 ? (
            <EmptyState
              icon={ClipboardList}
              title="No requests yet"
              description="Requests created by your team will appear here."
              action={
                <Link to="/requests/new" className="btn-primary mt-2">
                  <Plus className="size-4" aria-hidden="true" />
                  Create the first request
                </Link>
              }
            />
          ) : (
            <ul className="divide-y divide-ink-200">
              {stats.recentRequests.map((request) => (
                <li key={request.id}>
                  <Link
                    to={`/requests/${request.id}`}
                    className="flex items-center justify-between gap-3 px-5 py-3 hover:bg-ink-50"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-ink-900">
                        <span className="font-mono text-xs text-ink-500">{request.requestNumber}</span>{' '}
                        {request.title}
                      </p>
                      <p className="mt-0.5 truncate text-xs text-ink-500">
                        {request.requesterName} · {formatDate(request.createdAt)}
                      </p>
                    </div>
                    <div className="flex shrink-0 items-center gap-2">
                      <PriorityBadge priority={request.priority} />
                      <StatusBadge status={request.status} />
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
