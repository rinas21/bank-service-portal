import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  ArrowLeft,
  ArrowRightLeft,
  Check,
  ClipboardCheck,
  EyeOff,
  History,
  MessageSquare,
  Pencil,
  Send,
  UserCheck,
  X,
} from 'lucide-react'
import { branchApi, requestApi, userApi } from '@/lib/apiClient'
import { useAuth } from '@/context/authContext'
import { errorMessage } from '@/lib/api'
import { useToast } from '@/context/toastContext'
import type {
  Approval,
  AssignableUser,
  Assignment,
  Branch,
  RequestPriority,
  RequestStatus,
  ServiceRequestDetail,
  StatusHistoryEntry,
} from '@/types'
import {
  Button,
  ErrorState,
  Field,
  LoadingState,
  PriorityBadge,
  Select,
  StatusBadge,
  Textarea,
  TextInput,
} from '@/components/ui'
import { Modal } from '@/components/Modal'

const PRIORITIES: RequestPriority[] = ['Low', 'Medium', 'High', 'Critical']

const STATUS_TRANSITIONS: Record<RequestStatus, RequestStatus[]> = {
  Open: ['InProgress', 'Resolved', 'Closed'],
  InProgress: ['Resolved', 'Closed', 'Open'],
  Resolved: ['Closed', 'InProgress'],
  Closed: ['InProgress'],
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

// Due dates are calendar dates, not instants. Formatting the date portion
// directly avoids a UTC-midnight value rendering as the previous day.
function formatDateOnly(value: string | null | undefined) {
  if (!value) return '—'
  const date = new Date(`${value.slice(0, 10)}T00:00:00`)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString(undefined, { dateStyle: 'medium' })
}

function isOverdue(value: string | null | undefined) {
  if (!value) return false
  const due = new Date(`${value.slice(0, 10)}T00:00:00`)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return due.getTime() < today.getTime()
}

type DialogKind = 'status' | 'assign' | 'edit' | 'approval' | 'decision' | null

export default function RequestDetailPage() {
  const { id } = useParams<{ id: string }>()
  const requestId = Number(id)
  const { user, hasRole } = useAuth()
  const { showToast } = useToast()

  const [request, setRequest] = useState<ServiceRequestDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const [dialog, setDialog] = useState<DialogKind>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [formError, setFormError] = useState('')

  const [commentBody, setCommentBody] = useState('')
  const [isInternalComment, setIsInternalComment] = useState(false)

  const [agents, setAgents] = useState<AssignableUser[]>([])
  const [branches, setBranches] = useState<Branch[]>([])

  // Dialog form state.
  const [statusDraft, setStatusDraft] = useState<RequestStatus>('InProgress')
  const [reasonDraft, setReasonDraft] = useState('')
  const [assigneeDraft, setAssigneeDraft] = useState('')
  const [assignNoteDraft, setAssignNoteDraft] = useState('')
  const [editDraft, setEditDraft] = useState({
    title: '',
    description: '',
    category: '',
    priority: 'Medium' as RequestPriority,
    branchId: '',
    dueDate: '',
  })
  const [noteDraft, setNoteDraft] = useState('')
  const [decisionDraft, setDecisionDraft] = useState<'approve' | 'reject'>('approve')

  const load = useCallback(
    (signal?: AbortSignal) => {
      requestApi
        .get(requestId, signal)
        .then(setRequest)
        .catch((err) => {
          if ((err as Error).name === 'AbortError') return
          setError(errorMessage(err, 'We could not load this request.'))
        })
        .finally(() => setIsLoading(false))
    },
    [requestId],
  )

  function refresh() {
    setIsLoading(true)
    setError('')
    load()
  }

  useEffect(() => {
    if (!Number.isInteger(requestId)) return
    const controller = new AbortController()
    load(controller.signal)
    return () => controller.abort()
  }, [load, requestId])

  // Branch data backs the edit dialog, which any request owner can open, so it
  // is loaded for every authenticated role rather than only privileged ones.
  useEffect(() => {
    const controller = new AbortController()
    branchApi
      .list({ isActive: true, pageSize: 100 }, controller.signal)
      .then((result) => setBranches(result.items))
      .catch(() => setBranches([]))
    return () => controller.abort()
  }, [])

  // Agent data backs the assignment dialog, which only staff can reach.
  useEffect(() => {
    if (!hasRole('Admin', 'Manager', 'Support')) return
    const controller = new AbortController()
    userApi
      .assignable(controller.signal)
      .then((result) => setAgents(result))
      .catch(() => setAgents([]))
    return () => controller.abort()
  }, [hasRole])

  const permissions = useMemo(() => {
    if (!request || !user) {
      return {
        canEdit: false,
        canChangeStatus: false,
        canAssign: false,
        canDecide: false,
        canComment: false,
        canRequestApproval: false,
      }
    }
    const isOwner = request.requesterId === user.userId
    const isAssignee = request.assignedToId === user.userId
    const isPrivileged = hasRole('Admin', 'Manager', 'Support')
    const hasPendingApproval = (request.approvals ?? []).some((a) => a.status === 'Pending')

    return {
      // Mirrors the API rules: managers/admins or the request owner, and the API
      // rejects edits once a request is resolved or closed.
      canEdit:
        (hasRole('Admin', 'Manager') || isOwner) &&
        request.status !== 'Resolved' &&
        request.status !== 'Closed',
      // Employees may not change status; support acts on assigned work.
      canChangeStatus: hasRole('Admin', 'Manager') || (hasRole('Support') && (isAssignee || !request.assignedToId)),
      canAssign: hasRole('Admin', 'Manager', 'Support'),
      // The API rejects self-approval, so a requester who is also a manager
      // must not be offered the decision action.
      canDecide: hasRole('Admin', 'Manager') && !isOwner,
      canComment: isPrivileged || isOwner || isAssignee,
      // The API only allows the requester to raise an approval request, and
      // rejects a second concurrent one.
      canRequestApproval: isOwner && !hasPendingApproval && request.status !== 'Closed',
    }
  }, [request, user, hasRole])

  // Ownership beats role for internal notes: a manager who raised the request
// must not read or write internal notes about it, which is also how the API
// filters them. Keeps the composer from offering an action it would reject.
  const isRequestOwner = Boolean(request && user && request.requesterId === user.userId)
  const canSeeInternalComments = hasRole('Admin', 'Manager', 'Support') && !isRequestOwner

  const visibleComments = useMemo(
    () => (request?.comments ?? []).filter((comment) => canSeeInternalComments || !comment.isInternal),
    [request, canSeeInternalComments],
  )

  function openDialog(kind: Exclude<DialogKind, null>) {
    setFormError('')
    if (kind === 'status' && request) {
      // Default to the first legal transition for the current status, so the
      // select always shows an option that actually exists (Resolved -> Closed,
      // Closed -> InProgress, and so on) instead of rendering blank.
      setStatusDraft(STATUS_TRANSITIONS[request.status]?.[0] ?? 'InProgress')
      setReasonDraft('')
    }
    if (kind === 'assign' && request) {
      setAssigneeDraft(request.assignedToId ?? '')
      setAssignNoteDraft('')
    }
    if (kind === 'edit' && request) {
      setEditDraft({
        title: request.title,
        description: request.description,
        category: request.category,
        priority: request.priority,
        branchId: request.branchId ? String(request.branchId) : '',
        dueDate: request.dueDate ? request.dueDate.slice(0, 10) : '',
      })
    }
    if (kind === 'decision') setDecisionDraft('approve')
    setDialog(kind)
  }

  async function runAction(action: () => Promise<ServiceRequestDetail>, successMessage: string) {
    setIsSubmitting(true)
    setFormError('')
    try {
      const updated = await action()
      setRequest(updated)
      setDialog(null)
      showToast(successMessage, 'success')
    } catch (err) {
      const message = errorMessage(err, 'The action could not be completed.')
      setFormError(message)
      showToast(message, 'error')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleCommentSubmit() {
    if (!commentBody.trim()) return
    setIsSubmitting(true)
    try {
      const updated = await requestApi.addComment(requestId, commentBody.trim(), isInternalComment)
      setRequest(updated)
      setCommentBody('')
      setIsInternalComment(false)
      showToast('Comment added.', 'success')
    } catch (err) {
      showToast(errorMessage(err, 'The comment could not be added.'), 'error')
    } finally {
      setIsSubmitting(false)
    }
  }

  if (!Number.isInteger(requestId)) {
    return (
      <div className="card">
        <ErrorState message="That request number is not valid." />
      </div>
    )
  }

  if (isLoading) return <LoadingState label="Loading request…" />

  if (error || !request) {
    return (
      <div className="space-y-4">
        <Link to="/requests" className="inline-flex items-center gap-1 text-sm font-semibold text-brand-600 hover:text-brand-700">
          <ArrowLeft className="size-4" aria-hidden="true" />
          Back to requests
        </Link>
        <div className="card">
          <ErrorState message={error || 'This request is unavailable.'} onRetry={refresh} />
        </div>
      </div>
    )
  }

  const timeline = [
    ...request.statusHistory.map((entry) => ({
      kind: 'status' as const,
      at: entry.changedAt,
      entry,
    })),
    ...request.assignments.map((entry) => ({ kind: 'assignment' as const, at: entry.assignedAt, entry })),
    ...request.approvals.map((entry) => ({ kind: 'approval' as const, at: entry.requestedAt, entry })),
  ].sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime())

  return (
    <div className="space-y-5">
      <Link to="/requests" className="inline-flex items-center gap-1 text-sm font-semibold text-brand-600 hover:text-brand-700">
        <ArrowLeft className="size-4" aria-hidden="true" />
        Back to requests
      </Link>

      <header className="card p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-sm font-semibold text-ink-500">{request.requestNumber}</span>
              <StatusBadge status={request.status} />
              <PriorityBadge priority={request.priority} />
              {request.requiresApproval && (
                <span className="badge bg-violet-100 text-violet-700">
                  <ClipboardCheck className="size-3" aria-hidden="true" />
                  Approval required
                </span>
              )}
              {request.isMigrated && (
                <span className="badge bg-ink-200 text-ink-700">Migrated · {request.legacyReference}</span>
              )}
            </div>
            <h1 className="mt-2 text-2xl font-bold text-ink-900">{request.title}</h1>
            <p className="mt-1 text-sm text-ink-500">
              {request.category} · submitted by {request.requesterName} on {formatDateTime(request.createdAt)}
            </p>
          </div>

          <div className="flex flex-wrap gap-2">
            {permissions.canEdit && (
              <Button variant="secondary" onClick={() => openDialog('edit')}>
                <Pencil className="size-4" aria-hidden="true" />
                Edit
              </Button>
            )}
            {permissions.canAssign && (
              <Button variant="secondary" onClick={() => openDialog('assign')}>
                <ArrowRightLeft className="size-4" aria-hidden="true" />
                {request.assignedToName ? 'Reassign' : 'Assign'}
              </Button>
            )}
            {permissions.canChangeStatus && (
              <Button onClick={() => openDialog('status')}>Change status</Button>
            )}
          </div>
        </div>

        <dl className="mt-5 grid gap-4 border-t border-ink-200 pt-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <dt className="text-xs font-semibold uppercase tracking-wide text-ink-500">Requester</dt>
            <dd className="mt-1 text-sm font-medium text-ink-900">{request.requesterName}</dd>
          </div>
          <div>
            <dt className="text-xs font-semibold uppercase tracking-wide text-ink-500">Assigned agent</dt>
            <dd className="mt-1 text-sm font-medium text-ink-900">{request.assignedToName ?? 'Unassigned'}</dd>
          </div>
          <div>
            <dt className="text-xs font-semibold uppercase tracking-wide text-ink-500">Branch</dt>
            <dd className="mt-1 text-sm font-medium text-ink-900">{request.branchName ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs font-semibold uppercase tracking-wide text-ink-500">Due date</dt>
            <dd
              className={`mt-1 text-sm font-medium ${
                isOverdue(request.dueDate) && request.status !== 'Closed' && request.status !== 'Resolved'
                  ? 'text-red-600'
                  : 'text-ink-900'
              }`}
            >
              {request.dueDate ? formatDateOnly(request.dueDate) : '—'}
            </dd>
          </div>
        </dl>
      </header>

      <div className="grid gap-5 lg:grid-cols-3">
        <div className="space-y-5 lg:col-span-2">
          <section className="card" aria-label="Request description">
            <div className="card-header">
              <h2 className="text-sm font-semibold text-ink-900">Description</h2>
              {request.updatedAt && (
                <span className="text-xs text-ink-500">Last updated {formatDateTime(request.updatedAt)}</span>
              )}
            </div>
            <p className="whitespace-pre-wrap px-5 py-4 text-sm leading-relaxed text-ink-700">
              {request.description}
            </p>
          </section>

          <section className="card" aria-label="Comments">
            <div className="card-header">
              <h2 className="text-sm font-semibold text-ink-900">
                Conversation ({visibleComments.length})
              </h2>
            </div>

            {visibleComments.length === 0 ? (
              <p className="px-5 py-8 text-center text-sm text-ink-500">
                No comments yet. Start the conversation below.
              </p>
            ) : (
              <ul className="divide-y divide-ink-200">
                {visibleComments.map((comment) => (
                  <li key={comment.id} className="px-5 py-4">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-ink-900">{comment.authorName}</span>
                        {comment.isInternal && (
                          <span className="badge bg-amber-100 text-amber-800">
                            <EyeOff className="size-3" aria-hidden="true" />
                            Internal
                          </span>
                        )}
                      </div>
                      <span className="text-xs text-ink-500">{formatDateTime(comment.createdAt)}</span>
                    </div>
                    <p className="mt-1.5 whitespace-pre-wrap text-sm text-ink-700">{comment.body}</p>
                  </li>
                ))}
              </ul>
            )}

            {permissions.canComment ? (
              <form
                className="border-t border-ink-200 bg-ink-50 px-5 py-4"
                onSubmit={(event) => {
                  event.preventDefault()
                  handleCommentSubmit()
                }}
              >
                <Field label="Add a comment" htmlFor="new-comment">
                  <Textarea
                    id="new-comment"
                    rows={3}
                    maxLength={2000}
                    placeholder="Write an update for the requester or your team."
                    value={commentBody}
                    onChange={(event) => setCommentBody(event.target.value)}
                  />
                </Field>
                <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
                  {canSeeInternalComments ? (
                    <label className="flex cursor-pointer items-center gap-2 text-sm text-ink-700">
                      <input
                        type="checkbox"
                        className="size-4 rounded border-ink-300"
                        checked={isInternalComment}
                        onChange={(event) => setIsInternalComment(event.target.checked)}
                      />
                      Internal note (hidden from the requester)
                    </label>
                  ) : (
                    <span className="text-xs text-ink-500">Visible to the requester and the service desk.</span>
                  )}
                  <Button type="submit" isLoading={isSubmitting} disabled={!commentBody.trim()}>
                    <Send className="size-4" aria-hidden="true" />
                    Post comment
                  </Button>
                </div>
              </form>
            ) : (
              <p className="border-t border-ink-200 bg-ink-50 px-5 py-4 text-sm text-ink-500">
                You do not have permission to comment on this request.
              </p>
            )}
          </section>
        </div>

        <div className="space-y-5">
          <section className="card" aria-label="Approvals">
            <div className="card-header">
              <h2 className="text-sm font-semibold text-ink-900">Approvals</h2>
              {permissions.canRequestApproval && (
                <Button
                  variant="secondary"
                  className="px-3 py-1.5 text-xs"
                  isLoading={isSubmitting}
                  onClick={() => {
                    setNoteDraft('')
                    setDialog('approval')
                  }}
                >
                  Request approval
                </Button>
              )}
            </div>
            {request.approvals.length === 0 ? (
              <p className="px-5 py-6 text-center text-sm text-ink-500">No approval requests yet.</p>
            ) : (
              <ul className="divide-y divide-ink-200">
                {request.approvals.map((approval) => (
                  <li key={approval.id} className="px-5 py-3">
                    <div className="flex items-center justify-between gap-2">
                      <span
                        className={`badge ${
                          approval.status === 'Approved'
                            ? 'bg-emerald-100 text-emerald-800'
                            : approval.status === 'Rejected'
                              ? 'bg-red-100 text-red-800'
                              : 'bg-amber-100 text-amber-800'
                        }`}
                      >
                        {approval.status}
                      </span>
                      <span className="text-xs text-ink-500">{formatDateTime(approval.requestedAt)}</span>
                    </div>
                    <p className="mt-1.5 text-xs text-ink-600">
                      Requested by {approval.requestedByName}
                      {approval.approverName && ` · decided by ${approval.approverName}`}
                    </p>
                    {approval.reason && <p className="mt-1 text-sm text-ink-700">{approval.reason}</p>}
                    {approval.decisionNote && (
                      <p className="mt-1 text-sm italic text-ink-600">Note: {approval.decisionNote}</p>
                    )}
                    {approval.status === 'Pending' && permissions.canDecide && (
                      <Button
                        variant="secondary"
                        className="mt-2 px-3 py-1.5 text-xs"
                        onClick={() => openDialog('decision')}
                      >
                        Record decision
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="card" aria-label="Activity timeline">
            <div className="card-header">
              <h2 className="text-sm font-semibold text-ink-900">Activity</h2>
              <History className="size-4 text-ink-400" aria-hidden="true" />
            </div>
            {timeline.length === 0 ? (
              <p className="px-5 py-6 text-center text-sm text-ink-500">No activity recorded yet.</p>
            ) : (
              <ol className="space-y-4 px-5 py-4">
                {timeline.map((item) => {
                  if (item.kind === 'status') {
                    const entry = item.entry as StatusHistoryEntry
                    return (
                      <li key={`status-${entry.id}`} className="flex gap-3">
                        <StatusBadge status={entry.toStatus as RequestStatus} />
                        <div className="min-w-0">
                          <p className="text-sm text-ink-800">
                            <span className="font-semibold">{entry.changedByName}</span> moved it from{' '}
                            {entry.fromStatus} to {entry.toStatus}
                          </p>
                          {entry.reason && <p className="mt-0.5 text-sm text-ink-600">{entry.reason}</p>}
                          <p className="mt-0.5 text-xs text-ink-500">{formatDateTime(entry.changedAt)}</p>
                        </div>
                      </li>
                    )
                  }

                  if (item.kind === 'assignment') {
                    const entry = item.entry as Assignment
                    return (
                      <li key={`assignment-${entry.id}`} className="flex gap-3">
                        <span className="badge bg-blue-100 text-blue-800">
                          <UserCheck className="size-3" aria-hidden="true" />
                          Assigned
                        </span>
                        <div className="min-w-0">
                          <p className="text-sm text-ink-800">
                            <span className="font-semibold">{entry.assignedByName}</span> assigned{' '}
                            {entry.assigneeName}
                          </p>
                          {entry.note && <p className="mt-0.5 text-sm text-ink-600">{entry.note}</p>}
                          <p className="mt-0.5 text-xs text-ink-500">{formatDateTime(entry.assignedAt)}</p>
                        </div>
                      </li>
                    )
                  }

                  const entry = item.entry as Approval
                  return (
                    <li key={`approval-${entry.id}`} className="flex gap-3">
                      <span className="badge bg-violet-100 text-violet-800">
                        <MessageSquare className="size-3" aria-hidden="true" />
                        Approval
                      </span>
                      <div className="min-w-0">
                        <p className="text-sm text-ink-800">
                          <span className="font-semibold">{entry.requestedByName}</span> requested approval
                        </p>
                        <p className="mt-0.5 text-xs text-ink-500">{formatDateTime(entry.requestedAt)}</p>
                      </div>
                    </li>
                  )
                })}
              </ol>
            )}
          </section>
        </div>
      </div>

      {/* Change status */}
      <Modal
        isOpen={dialog === 'status'}
        title="Change request status"
        description={`Current status: ${request.status}`}
        onClose={() => setDialog(null)}
        size="sm"
        footer={
          <>
            <button type="button" className="btn-secondary" onClick={() => setDialog(null)} disabled={isSubmitting}>
              Cancel
            </button>
            <button
              type="button"
              className="btn-primary"
              disabled={isSubmitting}
              onClick={() =>
                runAction(
                  () => requestApi.updateStatus(requestId, statusDraft, reasonDraft.trim() || undefined),
                  'Status updated.',
                )
              }
            >
              Update status
            </button>
          </>
        }
      >
        <div className="space-y-4">
          <Field label="New status" htmlFor="status-draft" required>
            <Select
              id="status-draft"
              value={statusDraft}
              onChange={(event) => setStatusDraft(event.target.value as RequestStatus)}
            >
              {(STATUS_TRANSITIONS[request.status] ?? []).map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Reason" htmlFor="reason-draft" hint="Optional, shown on the timeline">
            <Textarea
              id="reason-draft"
              rows={3}
              maxLength={500}
              value={reasonDraft}
              onChange={(event) => setReasonDraft(event.target.value)}
            />
          </Field>
          {formError && <p className="text-sm font-medium text-red-600">{formError}</p>}
        </div>
      </Modal>

      {/* Assign */}
      <Modal
        isOpen={dialog === 'assign'}
        title={request.assignedToName ? 'Reassign request' : 'Assign request'}
        description="Pick the agent who will work on this request."
        onClose={() => setDialog(null)}
        size="sm"
        footer={
          <>
            <button type="button" className="btn-secondary" onClick={() => setDialog(null)} disabled={isSubmitting}>
              Cancel
            </button>
            <button
              type="button"
              className="btn-primary"
              disabled={isSubmitting || !assigneeDraft}
              onClick={() =>
                runAction(
                  () => requestApi.assign(requestId, assigneeDraft, assignNoteDraft.trim() || undefined),
                  'Request assigned.',
                )
              }
            >
              Assign
            </button>
          </>
        }
      >
        <div className="space-y-4">
          <Field label="Agent" htmlFor="assignee-draft" required>
            <Select
              id="assignee-draft"
              value={assigneeDraft}
              onChange={(event) => setAssigneeDraft(event.target.value)}
            >
              <option value="">Select an agent…</option>
              {agents.map((agent) => (
                <option key={agent.id} value={agent.id}>
                  {agent.fullName} — {agent.roles.join(', ') || 'No role'}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Assignment note" htmlFor="assign-note" hint="Optional">
            <Textarea
              id="assign-note"
              rows={3}
              maxLength={500}
              value={assignNoteDraft}
              onChange={(event) => setAssignNoteDraft(event.target.value)}
            />
          </Field>
          {formError && <p className="text-sm font-medium text-red-600">{formError}</p>}
        </div>
      </Modal>

      {/* Edit */}
      <Modal
        isOpen={dialog === 'edit'}
        title="Edit request"
        onClose={() => setDialog(null)}
        footer={
          <>
            <button type="button" className="btn-secondary" onClick={() => setDialog(null)} disabled={isSubmitting}>
              Cancel
            </button>
            <button
              type="button"
              className="btn-primary"
              disabled={isSubmitting}
              onClick={() =>
                runAction(
                  () =>
                    requestApi.update(requestId, {
                      title: editDraft.title.trim(),
                      description: editDraft.description.trim(),
                      category: editDraft.category.trim(),
                      priority: editDraft.priority,
                      branchId: editDraft.branchId ? Number(editDraft.branchId) : null,
                      dueDate: editDraft.dueDate || null,
                    }),
                  'Request updated.',
                )
              }
            >
              Save changes
            </button>
          </>
        }
      >
        <div className="space-y-4">
          <Field label="Title" htmlFor="edit-title" required>
            <TextInput
              id="edit-title"
              maxLength={200}
              value={editDraft.title}
              onChange={(event) => setEditDraft({ ...editDraft, title: event.target.value })}
            />
          </Field>
          <Field label="Description" htmlFor="edit-description" required>
            <Textarea
              id="edit-description"
              rows={5}
              maxLength={4000}
              value={editDraft.description}
              onChange={(event) => setEditDraft({ ...editDraft, description: event.target.value })}
            />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Category" htmlFor="edit-category" required>
              <TextInput
                id="edit-category"
                maxLength={100}
                value={editDraft.category}
                onChange={(event) => setEditDraft({ ...editDraft, category: event.target.value })}
              />
            </Field>
            <Field label="Priority" htmlFor="edit-priority" required>
              <Select
                id="edit-priority"
                value={editDraft.priority}
                onChange={(event) =>
                  setEditDraft({ ...editDraft, priority: event.target.value as RequestPriority })
                }
              >
                {PRIORITIES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Branch" htmlFor="edit-branch">
              <Select
                id="edit-branch"
                value={editDraft.branchId}
                onChange={(event) => setEditDraft({ ...editDraft, branchId: event.target.value })}
              >
                <option value="">No branch</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name} — {branch.city}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Due date" htmlFor="edit-due-date">
              <TextInput
                id="edit-due-date"
                type="date"
                value={editDraft.dueDate}
                onChange={(event) => setEditDraft({ ...editDraft, dueDate: event.target.value })}
              />
            </Field>
          </div>
          {formError && <p className="text-sm font-medium text-red-600">{formError}</p>}
        </div>
      </Modal>

      {/* Request approval */}
      <Modal
        isOpen={dialog === 'approval'}
        title="Request approval"
        description="A manager or administrator will review this request."
        onClose={() => setDialog(null)}
        size="sm"
        footer={
          <>
            <button type="button" className="btn-secondary" onClick={() => setDialog(null)} disabled={isSubmitting}>
              Cancel
            </button>
            <button
              type="button"
              className="btn-primary"
              disabled={isSubmitting}
              onClick={() =>
                runAction(
                  () => requestApi.requestApproval(requestId, noteDraft.trim() || undefined),
                  'Approval requested.',
                )
              }
            >
              Send request
            </button>
          </>
        }
      >
        <Field label="Reason" htmlFor="approval-note" hint="Optional">
          <Textarea
            id="approval-note"
            rows={3}
            maxLength={500}
            value={noteDraft}
            onChange={(event) => setNoteDraft(event.target.value)}
          />
        </Field>
      </Modal>

      {/* Approval decision */}
      <Modal
        isOpen={dialog === 'decision'}
        title="Record approval decision"
        onClose={() => setDialog(null)}
        size="sm"
        footer={
          <>
            <button type="button" className="btn-secondary" onClick={() => setDialog(null)} disabled={isSubmitting}>
              Cancel
            </button>
            <button
              type="button"
              className={decisionDraft === 'approve' ? 'btn-primary' : 'btn-danger'}
              disabled={isSubmitting}
              onClick={() =>
                runAction(
                  () =>
                    requestApi.decideApproval(
                      requestId,
                      decisionDraft === 'approve',
                      noteDraft.trim() || undefined,
                    ),
                  decisionDraft === 'approve' ? 'Approval granted.' : 'Approval rejected.',
                )
              }
            >
              {decisionDraft === 'approve' ? (
                <>
                  <Check className="size-4" aria-hidden="true" />
                  Approve
                </>
              ) : (
                <>
                  <X className="size-4" aria-hidden="true" />
                  Reject
                </>
              )}
            </button>
          </>
        }
      >
        <div className="space-y-4">
          <Field label="Decision" htmlFor="decision-draft" required>
            <Select
              id="decision-draft"
              value={decisionDraft}
              onChange={(event) => setDecisionDraft(event.target.value as 'approve' | 'reject')}
            >
              <option value="approve">Approve</option>
              <option value="reject">Reject</option>
            </Select>
          </Field>
          <Field label="Decision note" htmlFor="decision-note" hint="Optional">
            <Textarea
              id="decision-note"
              rows={3}
              maxLength={500}
              value={noteDraft}
              onChange={(event) => setNoteDraft(event.target.value)}
            />
          </Field>
          {formError && <p className="text-sm font-medium text-red-600">{formError}</p>}
        </div>
      </Modal>
    </div>
  )
}
