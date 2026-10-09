import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, ClipboardList } from 'lucide-react'
import { branchApi, requestApi } from '@/lib/apiClient'
import { errorMessage } from '@/lib/api'
import type { Branch, RequestPriority } from '@/types'
import { Button, Field, Select, Textarea, TextInput } from '@/components/ui'

const PRIORITIES: RequestPriority[] = ['Low', 'Medium', 'High', 'Critical']

const SUGGESTED_CATEGORIES = [
  'Account Services',
  'Cards & Payments',
  'Loans',
  'Technical Support',
  'Complaints',
  'Other',
]

export default function NewRequestPage() {
  const navigate = useNavigate()

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState(SUGGESTED_CATEGORIES[0])
  const [priority, setPriority] = useState<RequestPriority>('Medium')
  const [branchId, setBranchId] = useState('')
  const [dueDate, setDueDate] = useState('')

  const [branches, setBranches] = useState<Branch[]>([])
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    branchApi
      .list({ isActive: true, pageSize: 100 }, controller.signal)
      .then((result) => setBranches(result.items))
      .catch(() => setBranches([]))
    return () => controller.abort()
  }, [])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      const created = await requestApi.create({
        title: title.trim(),
        description: description.trim(),
        category: category.trim(),
        priority,
        branchId: branchId ? Number(branchId) : null,
        // Send the date as-is: a due date is a calendar date, not an instant, so
        // anchoring it to UTC midnight would shift it by a day in local timezones.
        dueDate: dueDate || null,
      })
      navigate(`/requests/${created.id}`, { replace: true })
    } catch (err) {
      setError(errorMessage(err, 'We could not create the request.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <Link to="/requests" className="inline-flex items-center gap-1 text-sm font-semibold text-brand-600 hover:text-brand-700">
        <ArrowLeft className="size-4" aria-hidden="true" />
        Back to requests
      </Link>

      <header>
        <h1 className="text-2xl font-bold text-ink-900">New service request</h1>
        <p className="mt-1 text-sm text-ink-500">
          Describe what you need. The service desk will assign an agent and keep you updated.
        </p>
      </header>

      <form onSubmit={handleSubmit} className="card p-6" noValidate>
        <div className="space-y-4">
          <Field label="Title" htmlFor="title" required>
            <TextInput
              id="title"
              required
              maxLength={200}
              placeholder="Short summary of the request"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
          </Field>

          <Field label="Description" htmlFor="description" required>
            <Textarea
              id="description"
              required
              rows={6}
              maxLength={4000}
              placeholder="Include the details our agents will need to help you."
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </Field>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Category" htmlFor="category" required>
              <Select id="category" value={category} onChange={(event) => setCategory(event.target.value)}>
                {SUGGESTED_CATEGORIES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Priority" htmlFor="priority" required>
              <Select
                id="priority"
                value={priority}
                onChange={(event) => setPriority(event.target.value as RequestPriority)}
              >
                {PRIORITIES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Branch" htmlFor="branch" hint="Optional">
              <Select id="branch" value={branchId} onChange={(event) => setBranchId(event.target.value)}>
                <option value="">No branch</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name} — {branch.city}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Preferred completion date" htmlFor="dueDate" hint="Optional">
              <TextInput
                id="dueDate"
                type="date"
                value={dueDate}
                onChange={(event) => setDueDate(event.target.value)}
              />
            </Field>
          </div>
        </div>

        {error && (
          <div
            className="mt-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-800"
            role="alert"
          >
            {error}
          </div>
        )}

        <div className="mt-6 flex flex-wrap items-center justify-end gap-2 border-t border-ink-200 pt-4">
          <Link to="/requests" className="btn-secondary">
            Cancel
          </Link>
          <Button type="submit" isLoading={isSubmitting}>
            <ClipboardList className="size-4" aria-hidden="true" />
            {isSubmitting ? 'Creating…' : 'Create request'}
          </Button>
        </div>
      </form>
    </div>
  )
}
