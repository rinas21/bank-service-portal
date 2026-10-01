import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Pencil, Plus, RotateCcw, UserCheck, UserX, Users } from 'lucide-react'
import { branchApi, userApi } from '@/lib/apiClient'
import { errorMessage } from '@/lib/api'
import { useToast } from '@/context/toastContext'
import type { Branch, Role, User } from '@/types'
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
import { ConfirmDialog, Modal } from '@/components/Modal'

const ALL_ROLES: Role[] = ['Admin', 'Manager', 'Support', 'Employee']

function formatDate(value: string | null) {
  if (!value) return 'Never'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Never' : date.toLocaleString()
}

interface UserFormState {
  firstName: string
  lastName: string
  email: string
  employeeNumber: string
  branchId: string
  password: string
  roles: string[]
}

const EMPTY_FORM: UserFormState = {
  firstName: '',
  lastName: '',
  email: '',
  employeeNumber: '',
  branchId: '',
  password: '',
  roles: ['Employee'],
}

export default function UsersPage() {
  const { showToast } = useToast()

  const [users, setUsers] = useState<User[]>([])
  const [branches, setBranches] = useState<Branch[]>([])
  const [search, setSearch] = useState('')
  const [searchDraft, setSearchDraft] = useState('')
  const [role, setRole] = useState('')
  const [isActive, setIsActive] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 10

  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  const [error, setError] = useState('')
  // Key of the query whose results are currently displayed. Loading is derived
  // from it rather than toggled by hand, so an interaction that does not change
  // the query cannot leave a spinner running with nothing to finish it.
  const [loadedKey, setLoadedKey] = useState<string | null>(null)
  const queryKey = useMemo(() => JSON.stringify([search, role, isActive, page]), [search, role, isActive, page])
  const isLoading = loadedKey !== queryKey

  const [isFormOpen, setIsFormOpen] = useState(false)
  const [editingUser, setEditingUser] = useState<User | null>(null)
  const [form, setForm] = useState<UserFormState>(EMPTY_FORM)
  const [formError, setFormError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const [pendingStatusChange, setPendingStatusChange] = useState<{ user: User; activate: boolean } | null>(null)

  const load = useCallback(
    (signal?: AbortSignal) => {
      userApi
        .list(
          {
            search: search || undefined,
            role: role || undefined,
            isActive: isActive === '' ? '' : isActive === 'true',
            page,
            pageSize,
          },
          signal,
        )
        .then((result) => {
          setUsers(result.items)
          setTotalCount(result.totalCount)
          setTotalPages(result.totalPages)
        })
        .catch((err) => {
          if ((err as Error).name === 'AbortError') return
          setError(errorMessage(err, 'We could not load the users.'))
        })
        .finally(() => setLoadedKey(queryKey))
    },
    [search, role, isActive, page, queryKey],
  )

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal)
    return () => controller.abort()
  }, [load])

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

  // Re-runs the fetch for the current query. Called for retry-after-error and
  // after mutations; a no-op filter change still refetches because the request
  // is issued directly rather than inferred from a dependency change.
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

  function openCreate() {
    setEditingUser(null)
    setForm(EMPTY_FORM)
    setFormError('')
    setIsFormOpen(true)
  }

  function openEdit(user: User) {
    setEditingUser(user)
    setForm({
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      employeeNumber: user.employeeNumber ?? '',
      branchId: user.branchId ? String(user.branchId) : '',
      password: '',
      roles: user.roles.length > 0 ? user.roles : ['Employee'],
    })
    setFormError('')
    setIsFormOpen(true)
  }

  function toggleFormRole(roleToToggle: string) {
    setForm((current) => ({
      ...current,
      roles: current.roles.includes(roleToToggle)
        ? current.roles.filter((value) => value !== roleToToggle)
        : [...current.roles, roleToToggle],
    }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError('')

    if (form.roles.length === 0) {
      setFormError('Select at least one role.')
      return
    }

    if (!editingUser && form.password.length < 8) {
      setFormError('The password must be at least 8 characters long.')
      return
    }

    setIsSubmitting(true)
    try {
      if (editingUser) {
        await userApi.update(editingUser.id, {
          firstName: form.firstName.trim(),
          lastName: form.lastName.trim(),
          email: form.email.trim(),
          employeeNumber: form.employeeNumber.trim(),
          branchId: form.branchId ? Number(form.branchId) : null,
          roles: form.roles,
        })
        showToast('User updated.', 'success')
      } else {
        await userApi.create({
          firstName: form.firstName.trim(),
          lastName: form.lastName.trim(),
          email: form.email.trim(),
          password: form.password,
          employeeNumber: form.employeeNumber.trim() || undefined,
          branchId: form.branchId ? Number(form.branchId) : null,
          roles: form.roles,
        })
        showToast('User created.', 'success')
      }

      setIsFormOpen(false)
      refresh()
    } catch (err) {
      setFormError(errorMessage(err, 'The user could not be saved.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  async function confirmStatusChange() {
    if (!pendingStatusChange) return
    const { user, activate } = pendingStatusChange
    setIsSubmitting(true)
    try {
      if (activate) await userApi.reactivate(user.id)
      else await userApi.deactivate(user.id)
      showToast(activate ? 'User reactivated.' : 'User deactivated.', 'success')
      setPendingStatusChange(null)
      refresh()
    } catch (err) {
      showToast(errorMessage(err, 'The user status could not be changed.'), 'error')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-ink-900">User administration</h1>
          <p className="mt-1 text-sm text-ink-500">Create accounts, assign roles and control access.</p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="size-4" aria-hidden="true" />
          New user
        </Button>
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
            <Field label="Search" htmlFor="user-search">
              <TextInput
                id="user-search"
                placeholder="Name, email or employee number"
                value={searchDraft}
                onChange={(event) => setSearchDraft(event.target.value)}
              />
            </Field>
          </div>
          <Field label="Role" htmlFor="user-role">
            <Select
              id="user-role"
              className="w-40"
              value={role}
              onChange={(event) => changeFilter(() => setRole(event.target.value))}
            >
              <option value="">Any role</option>
              {ALL_ROLES.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Status" htmlFor="user-active">
            <Select
              id="user-active"
              className="w-36"
              value={isActive}
              onChange={(event) => changeFilter(() => setIsActive(event.target.value))}
            >
              <option value="">Any</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </Select>
          </Field>
          <Button type="submit" variant="secondary">
            Search
          </Button>
        </form>
      </div>

      <section className="card overflow-hidden" aria-label="User list">
        {isLoading ? (
          <LoadingState label="Loading users…" />
        ) : error ? (
          <ErrorState message={error} onRetry={refresh} />
        ) : users.length === 0 ? (
          <EmptyState
            icon={Users}
            title="No users match your filters"
            description="Adjust the filters or create a new user account."
            action={
              <Button className="mt-2" onClick={openCreate}>
                <Plus className="size-4" aria-hidden="true" />
                New user
              </Button>
            }
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead className="bg-ink-50 text-xs uppercase tracking-wide text-ink-500">
                  <tr>
                    <th scope="col" className="px-5 py-3 font-semibold">User</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Employee no.</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Branch</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Roles</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Status</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Last login</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-ink-200">
                  {users.map((user) => (
                    <tr key={user.id} className="hover:bg-ink-50">
                      <td className="px-5 py-3">
                        <p className="font-medium text-ink-900">{user.fullName}</p>
                        <p className="text-xs text-ink-500">{user.email}</p>
                      </td>
                      <td className="px-5 py-3 font-mono text-xs text-ink-600">{user.employeeNumber || '—'}</td>
                      <td className="px-5 py-3 text-ink-700">{user.branchName ?? '—'}</td>
                      <td className="px-5 py-3">
                        <div className="flex flex-wrap gap-1">
                          {user.roles.map((userRole) => (
                            <span key={userRole} className="badge bg-brand-100 text-brand-700">
                              {userRole}
                            </span>
                          ))}
                        </div>
                      </td>
                      <td className="px-5 py-3">
                        <span
                          className={`badge ${
                            user.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-ink-200 text-ink-700'
                          }`}
                        >
                          {user.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td className="px-5 py-3 text-xs text-ink-600">{formatDate(user.lastLoginAt)}</td>
                      <td className="px-5 py-3">
                        <div className="flex gap-1.5">
                          <Button variant="ghost" className="px-2.5 py-1.5 text-xs" onClick={() => openEdit(user)}>
                            <Pencil className="size-3.5" aria-hidden="true" />
                            Edit
                          </Button>
                          <Button
                            variant="ghost"
                            className="px-2.5 py-1.5 text-xs"
                            onClick={() => setPendingStatusChange({ user, activate: !user.isActive })}
                          >
                            {user.isActive ? (
                              <>
                                <UserX className="size-3.5" aria-hidden="true" />
                                Deactivate
                              </>
                            ) : (
                              <>
                                <RotateCcw className="size-3.5" aria-hidden="true" />
                                Reactivate
                              </>
                            )}
                          </Button>
                        </div>
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

      <Modal
        isOpen={isFormOpen}
        title={editingUser ? `Edit ${editingUser.fullName}` : 'Create a user'}
        description={
          editingUser
            ? 'Update the profile, branch and roles for this account.'
            : 'The account can sign in immediately with the password you set.'
        }
        onClose={() => setIsFormOpen(false)}
        footer={
          <>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setIsFormOpen(false)}
              disabled={isSubmitting}
            >
              Cancel
            </button>
            <button type="submit" form="user-form" className="btn-primary" disabled={isSubmitting}>
              <UserCheck className="size-4" aria-hidden="true" />
              {isSubmitting ? 'Saving…' : editingUser ? 'Save changes' : 'Create user'}
            </button>
          </>
        }
      >
        <form id="user-form" onSubmit={handleSubmit} className="space-y-4" noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="First name" htmlFor="user-first-name" required>
              <TextInput
                id="user-first-name"
                required
                value={form.firstName}
                onChange={(event) => setForm({ ...form, firstName: event.target.value })}
              />
            </Field>
            <Field label="Last name" htmlFor="user-last-name" required>
              <TextInput
                id="user-last-name"
                required
                value={form.lastName}
                onChange={(event) => setForm({ ...form, lastName: event.target.value })}
              />
            </Field>
            <div className="sm:col-span-2">
              <Field label="Email address" htmlFor="user-email" required>
                <TextInput
                  id="user-email"
                  type="email"
                  required
                  value={form.email}
                  onChange={(event) => setForm({ ...form, email: event.target.value })}
                />
              </Field>
            </div>
            <Field label="Employee number" htmlFor="user-employee-number">
              <TextInput
                id="user-employee-number"
                maxLength={20}
                value={form.employeeNumber}
                onChange={(event) => setForm({ ...form, employeeNumber: event.target.value })}
              />
            </Field>
            <Field label="Branch" htmlFor="user-branch">
              <Select
                id="user-branch"
                value={form.branchId}
                onChange={(event) => setForm({ ...form, branchId: event.target.value })}
              >
                <option value="">No branch</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name} — {branch.city}
                  </option>
                ))}
              </Select>
            </Field>
            {!editingUser && (
              <div className="sm:col-span-2">
                <Field label="Initial password" htmlFor="user-password" hint="At least 8 characters" required>
                  <TextInput
                    id="user-password"
                    type="password"
                    required
                    minLength={8}
                    value={form.password}
                    onChange={(event) => setForm({ ...form, password: event.target.value })}
                  />
                </Field>
              </div>
            )}
          </div>

          <fieldset>
            <legend className="label">Roles</legend>
            <div className="flex flex-wrap gap-2">
              {ALL_ROLES.map((value) => (
                <label
                  key={value}
                  className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-sm ${
                    form.roles.includes(value)
                      ? 'border-brand-500 bg-brand-50 text-brand-800'
                      : 'border-ink-300 text-ink-600'
                  }`}
                >
                  <input
                    type="checkbox"
                    className="size-4"
                    checked={form.roles.includes(value)}
                    onChange={() => toggleFormRole(value)}
                  />
                  {value}
                </label>
              ))}
            </div>
          </fieldset>

          {formError && (
            <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
              {formError}
            </p>
          )}
        </form>
      </Modal>

      <ConfirmDialog
        isOpen={pendingStatusChange !== null}
        title={pendingStatusChange?.activate ? 'Reactivate user' : 'Deactivate user'}
        message={
          pendingStatusChange?.activate
            ? `${pendingStatusChange.user.fullName} will be able to sign in again.`
            : `${pendingStatusChange?.user.fullName} will no longer be able to sign in. Their history is kept.`
        }
        confirmLabel={pendingStatusChange?.activate ? 'Reactivate' : 'Deactivate'}
        isBusy={isSubmitting}
        onConfirm={confirmStatusChange}
        onCancel={() => setPendingStatusChange(null)}
      />
    </div>
  )
}
