import { useCallback, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { ToastContext } from '@/context/toastContext'
import type { ToastVariant } from '@/context/toastContext'

interface Toast {
  id: number
  variant: ToastVariant
  message: string
}

const VARIANT_STYLES: Record<ToastVariant, { icon: typeof Info; className: string }> = {
  success: { icon: CheckCircle2, className: 'border-emerald-200 bg-emerald-50 text-emerald-800' },
  error: { icon: XCircle, className: 'border-red-200 bg-red-50 text-red-800' },
  info: { icon: Info, className: 'border-brand-200 bg-brand-50 text-brand-800' },
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(1)

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const showToast = useCallback(
    (message: string, variant: ToastVariant = 'info') => {
      const id = nextId.current++
      setToasts((current) => [...current, { id, variant, message }])
      window.setTimeout(() => dismiss(id), 5000)
    },
    [dismiss],
  )

  const value = useMemo(() => ({ showToast }), [showToast])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-4 sm:items-end sm:p-6"
        role="region"
        aria-label="Notifications"
      >
        {toasts.map((toast) => {
          const { icon: Icon, className } = VARIANT_STYLES[toast.variant]
          return (
            <div
              key={toast.id}
              role="status"
              className={`pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-lg border px-4 py-3 shadow-lg ${className}`}
            >
              <Icon className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
              <p className="flex-1 text-sm font-medium">{toast.message}</p>
              <button
                type="button"
                onClick={() => dismiss(toast.id)}
                className="cursor-pointer rounded p-0.5 opacity-70 hover:opacity-100"
                aria-label="Dismiss notification"
              >
                <X className="size-4" aria-hidden="true" />
              </button>
            </div>
          )
        })}
      </div>
    </ToastContext.Provider>
  )
}
