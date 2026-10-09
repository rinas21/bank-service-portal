import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'
import type { RequestPriority, RequestStatus } from '@/types'
import { AlertCircle, Loader2 } from 'lucide-react'

const STATUS_STYLES: Record<RequestStatus, string> = {
  Open: 'bg-blue-100 text-blue-800',
  InProgress: 'bg-amber-100 text-amber-800',
  Resolved: 'bg-emerald-100 text-emerald-800',
  Closed: 'bg-ink-200 text-ink-700',
}

const PRIORITY_STYLES: Record<RequestPriority, string> = {
  Low: 'bg-ink-100 text-ink-700',
  Medium: 'bg-blue-100 text-blue-800',
  High: 'bg-amber-100 text-amber-800',
  Critical: 'bg-red-100 text-red-800',
}

export function StatusBadge({ status }: { status: RequestStatus }) {
  return <span className={`badge ${STATUS_STYLES[status]}`}>{status}</span>
}

export function PriorityBadge({ priority }: { priority: RequestPriority }) {
  return <span className={`badge ${PRIORITY_STYLES[priority]}`}>{priority}</span>
}

export function Spinner({ className = 'size-5' }: { className?: string }) {
  return <Loader2 className={`${className} animate-spin`} aria-hidden="true" />
}

export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-16 text-sm text-ink-500" role="status">
      <Spinner />
      <span>{label}</span>
    </div>
  )
}

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
}: {
  icon: typeof AlertCircle
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
      <span className="flex size-12 items-center justify-center rounded-full bg-ink-100 text-ink-400">
        <Icon className="size-6" aria-hidden="true" />
      </span>
      <h3 className="text-sm font-semibold text-ink-800">{title}</h3>
      {description && <p className="max-w-md text-sm text-ink-500">{description}</p>}
      {action}
    </div>
  )
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div
      className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center"
      role="alert"
    >
      <span className="flex size-12 items-center justify-center rounded-full bg-red-50 text-red-500">
        <AlertCircle className="size-6" aria-hidden="true" />
      </span>
      <h3 className="text-sm font-semibold text-ink-800">Unable to load this data</h3>
      <p className="max-w-md text-sm text-ink-500">{message}</p>
      {onRetry && (
        <button type="button" className="btn-secondary mt-1" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  isLoading?: boolean
}

export function Button({ variant = 'primary', isLoading, children, className, disabled, ...props }: ButtonProps) {
  const variantClass = {
    primary: 'btn-primary',
    secondary: 'btn-secondary',
    danger: 'btn-danger',
    ghost: 'btn-ghost',
  }[variant]

  return (
    <button className={`${variantClass} ${className ?? ''}`} disabled={isLoading || disabled} {...props}>
      {isLoading && <Spinner className="size-4" />}
      {children}
    </button>
  )
}

interface FieldProps {
  label: string
  htmlFor: string
  error?: string
  hint?: string
  required?: boolean
  children: ReactNode
}

export function Field({ label, htmlFor, error, hint, required, children }: FieldProps) {
  return (
    <div>
      <label className="label" htmlFor={htmlFor}>
        {label}
        {required && <span className="ml-0.5 text-red-500">*</span>}
      </label>
      {children}
      {hint && !error && <p className="mt-1 text-xs text-ink-500">{hint}</p>}
      {error && (
        <p className="mt-1 text-xs font-medium text-red-600" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={`input ${props.className ?? ''}`} />
}

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={`input ${props.className ?? ''}`} />
}

export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={`input ${props.className ?? ''}`} />
}

export function Pagination({
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
}: {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  onPageChange: (page: number) => void
}) {
  if (totalCount === 0) return null

  const first = (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, totalCount)

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-ink-200 px-5 py-3 text-sm text-ink-600">
      <p>
        Showing <span className="font-semibold">{first}</span>–<span className="font-semibold">{last}</span> of{' '}
        <span className="font-semibold">{totalCount}</span>
      </p>
      <div className="flex items-center gap-2">
        <Button variant="secondary" onClick={() => onPageChange(page - 1)} disabled={page <= 1}>
          Previous
        </Button>
        <span className="px-2 text-xs font-medium">
          Page {page} of {Math.max(totalPages, 1)}
        </span>
        <Button
          variant="secondary"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
        >
          Next
        </Button>
      </div>
    </div>
  )
}
