import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Building2, Pencil, Plus, Power } from 'lucide-react'
import { branchApi } from '@/lib/apiClient'
import { errorMessage } from '@/lib/api'
import { useToast } from '@/context/toastContext'
import { useAuth } from '@/context/authContext'
import type { Branch } from '@/types'
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
import { Modal } from '@/components/Modal'

interface BranchFormState {
  code: string
  name: string
  city: string
  address: string
  phone: string
  isActive: boolean
}

const EMPTY_FORM: BranchFormState = {
  code: '',
  name: '',
  city: '',
  address: '',
  phone: '',
  isActive: true,
}

export default function BranchesPage() {
  const { showToast } = useToast()
  const { hasRole } = useAuth()
  const canEdit = hasRole('Admin')

  const [branches, setBranches] = useState<Branch[]>([])
  const [search, setSearch] = useState('')
  const [searchDraft, setSearchDraft] = useState('')
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
  const queryKey = useMemo(() => JSON.stringify([search, isActive, page]), [search, isActive, page])
  const isLoading = loadedKey !== queryKey

  const [isFormOpen, setIsFormOpen] = useState(false)
  const [editingBranch, setEditingBranch] = useState<Branch | null>(null)
  const [form, setForm] = useState<BranchFormState>(EMPTY_FORM)
  const [formError, setFormError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  const load = useCallback(
    (signal?: AbortSignal) => {
      branchApi
        .list(
          {
            search: search || undefined,
            isActive: isActive === '' ? '' : isActive === 'true',
            page,
            pageSize,
          },
          signal,
        )
        .then((result) => {
          setBranches(result.items)
          setTotalCount(result.totalCount)
          setTotalPages(result.totalPages)
        })
        .catch((err) => {
          if ((err as Error).name === 'AbortError') return
          setError(errorMessage(err, 'We could not load the branches.'))
        })
        .finally(() => setLoadedKey(queryKey))
    },
    [search, isActive, page, queryKey],
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

  function openCreate() {
    setEditingBranch(null)
    setForm(EMPTY_FORM)
    setFormError('')
    setIsFormOpen(true)
  }

  function openEdit(branch: Branch, nextIsActive = branch.isActive) {
    setEditingBranch(branch)
    setForm({
      code: branch.code,
      name: branch.name,
      city: branch.city,
      address: branch.address ?? '',
      phone: branch.phone ?? '',
      // The status toggle can be pre-flipped when the row action is
      // "Deactivate", so the dialog opens ready to confirm the change.
      isActive: nextIsActive,
    })
    setFormError('')
    setIsFormOpen(true)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError('')
    setIsSubmitting(true)

    try {
      if (editingBranch) {
        await branchApi.update(editingBranch.id, {
          code: form.code.trim(),
          name: form.name.trim(),
          city: form.city.trim(),
          address: form.address.trim(),
          phone: form.phone.trim(),
          isActive: form.isActive,
        })
        showToast('Branch updated.', 'success')
      } else {
        await branchApi.create({
          code: form.code.trim(),
          name: form.name.trim(),
          city: form.city.trim(),
          address: form.address.trim(),
          phone: form.phone.trim(),
        })
        showToast('Branch created.', 'success')
      }

      setIsFormOpen(false)
      refresh()
    } catch (err) {
      setFormError(errorMessage(err, 'The branch could not be saved.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-ink-900">Branches</h1>
          <p className="mt-1 text-sm text-ink-500">
            {canEdit ? 'Create and maintain the branch network.' : 'Read-only view of the branch network.'}
          </p>
        </div>
        {canEdit && (
          <Button onClick={openCreate}>
            <Plus className="size-4" aria-hidden="true" />
            New branch
          </Button>
        )}
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
            <Field label="Search" htmlFor="branch-search">
              <TextInput
                id="branch-search"
                placeholder="Code, name or city"
                value={searchDraft}
                onChange={(event) => setSearchDraft(event.target.value)}
              />
            </Field>
          </div>
          <Field label="Status" htmlFor="branch-active">
            <Select
              id="branch-active"
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

      <section className="card overflow-hidden" aria-label="Branch list">
        {isLoading ? (
          <LoadingState label="Loading branches…" />
        ) : error ? (
          <ErrorState message={error} onRetry={refresh} />
        ) : branches.length === 0 ? (
          <EmptyState
            icon={Building2}
            title="No branches match your filters"
            description="Adjust the filters or create a new branch."
            action={
              canEdit ? (
                <Button className="mt-2" onClick={openCreate}>
                  <Plus className="size-4" aria-hidden="true" />
                  New branch
                </Button>
              ) : undefined
            }
          />
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead className="bg-ink-50 text-xs uppercase tracking-wide text-ink-500">
                  <tr>
                    <th scope="col" className="px-5 py-3 font-semibold">Code</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Branch</th>
                    <th scope="col" className="px-5 py-3 font-semibold">City</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Contact</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Users</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Open requests</th>
                    <th scope="col" className="px-5 py-3 font-semibold">Status</th>
                    {canEdit && <th scope="col" className="px-5 py-3 font-semibold">Actions</th>}
                  </tr>
                </thead>
                <tbody className="divide-y divide-ink-200">
                  {branches.map((branch) => (
                    <tr key={branch.id} className="hover:bg-ink-50">
                      <td className="px-5 py-3 font-mono text-xs font-semibold text-ink-600">{branch.code}</td>
                      <td className="px-5 py-3 font-medium text-ink-900">{branch.name}</td>
                      <td className="px-5 py-3 text-ink-700">{branch.city}</td>
                      <td className="px-5 py-3 text-xs text-ink-600">
                        <p>{branch.address ?? '—'}</p>
                        <p>{branch.phone ?? ''}</p>
                      </td>
                      <td className="px-5 py-3 text-ink-700">{branch.userCount}</td>
                      <td className="px-5 py-3 text-ink-700">{branch.openRequestCount}</td>
                      <td className="px-5 py-3">
                        <span
                          className={`badge ${
                            branch.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-ink-200 text-ink-700'
                          }`}
                        >
                          {branch.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      {canEdit && (
                        <td className="px-5 py-3">
                          <Button
                            variant="ghost"
                            className="px-2.5 py-1.5 text-xs"
                            onClick={() => openEdit(branch, !branch.isActive)}
                          >
                            {branch.isActive ? (
                              <>
                                <Power className="size-3.5" aria-hidden="true" />
                                Deactivate
                              </>
                            ) : (
                              <>
                                <Pencil className="size-3.5" aria-hidden="true" />
                                Edit &amp; activate
                              </>
                            )}
                          </Button>
                        </td>
                      )}
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
        title={
          editingBranch
            ? `${form.isActive ? 'Edit' : 'Deactivate'} ${editingBranch.name}`
            : 'Create a branch'
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
            <button type="submit" form="branch-form" className="btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : editingBranch ? 'Save changes' : 'Create branch'}
            </button>
          </>
        }
      >
        <form id="branch-form" onSubmit={handleSubmit} className="space-y-4" noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Branch code" htmlFor="branch-code" hint="Up to 20 characters" required>
              <TextInput
                id="branch-code"
                required
                maxLength={20}
                placeholder="HQ-CENTRAL"
                value={form.code}
                onChange={(event) => setForm({ ...form, code: event.target.value })}
              />
            </Field>
            <Field label="City" htmlFor="branch-city" required>
              <TextInput
                id="branch-city"
                required
                maxLength={100}
                value={form.city}
                onChange={(event) => setForm({ ...form, city: event.target.value })}
              />
            </Field>
            <div className="sm:col-span-2">
              <Field label="Branch name" htmlFor="branch-name" required>
                <TextInput
                  id="branch-name"
                  required
                  maxLength={150}
                  value={form.name}
                  onChange={(event) => setForm({ ...form, name: event.target.value })}
                />
              </Field>
            </div>
            <div className="sm:col-span-2">
              <Field label="Address" htmlFor="branch-address">
                <TextInput
                  id="branch-address"
                  maxLength={300}
                  value={form.address}
                  onChange={(event) => setForm({ ...form, address: event.target.value })}
                />
              </Field>
            </div>
            <Field label="Phone" htmlFor="branch-phone" hint="Up to 30 characters">
              <TextInput
                id="branch-phone"
                maxLength={30}
                placeholder="+62 21 1234567"
                value={form.phone}
                onChange={(event) => setForm({ ...form, phone: event.target.value })}
              />
            </Field>
            {editingBranch && (
              <Field label="Status" htmlFor="branch-status">
                <Select
                  id="branch-status"
                  value={form.isActive ? 'true' : 'false'}
                  onChange={(event) => setForm({ ...form, isActive: event.target.value === 'true' })}
                >
                  <option value="true">Active</option>
                  <option value="false">Inactive</option>
                </Select>
              </Field>
            )}
          </div>

          {formError && (
            <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
              {formError}
            </p>
          )}
        </form>
      </Modal>
    </div>
  )
}
