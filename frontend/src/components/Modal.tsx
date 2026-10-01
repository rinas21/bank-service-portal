import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'

interface ModalProps {
  isOpen: boolean
  title: string
  description?: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
  size?: 'sm' | 'md' | 'lg'
}

const SIZES = { sm: 'max-w-md', md: 'max-w-2xl', lg: 'max-w-4xl' }

export function Modal({
  isOpen,
  title,
  description,
  onClose,
  children,
  footer,
  size = 'md',
}: ModalProps) {
  useEffect(() => {
    if (!isOpen) return

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    document.addEventListener('keydown', onKeyDown)
    // Prevent the page behind the dialog from scrolling.
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.body.style.overflow = previousOverflow
    }
  }, [isOpen, onClose])

  if (!isOpen) return null

  return createPortal(
    <div className="fixed inset-0 z-40 flex items-end justify-center overflow-y-auto bg-ink-950/50 p-0 sm:items-center sm:p-6">
      <button
        type="button"
        className="absolute inset-0 cursor-default"
        aria-label="Close dialog"
        onClick={onClose}
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        className={`relative z-10 w-full ${SIZES[size]} rounded-t-2xl bg-white shadow-2xl sm:rounded-2xl`}
      >
        <div className="flex items-start justify-between gap-4 border-b border-ink-200 px-6 py-4">
          <div>
            <h2 id="modal-title" className="text-lg font-semibold text-ink-900">
              {title}
            </h2>
            {description && <p className="mt-1 text-sm text-ink-500">{description}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            className="btn-ghost -mr-2 -mt-1 p-2"
            aria-label="Close dialog"
          >
            <X className="size-5" aria-hidden="true" />
          </button>
        </div>

        <div className="max-h-[70vh] overflow-y-auto px-6 py-5">{children}</div>

        {footer && (
          <div className="flex flex-wrap items-center justify-end gap-2 border-t border-ink-200 bg-ink-50 px-6 py-4">
            {footer}
          </div>
        )}
      </div>
    </div>,
    document.body,
  )
}

interface ConfirmDialogProps {
  isOpen: boolean
  title: string
  message: string
  confirmLabel?: string
  isBusy?: boolean
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({
  isOpen,
  title,
  message,
  confirmLabel = 'Confirm',
  isBusy,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Modal
      isOpen={isOpen}
      title={title}
      onClose={onCancel}
      size="sm"
      footer={
        <>
          <button type="button" className="btn-secondary" onClick={onCancel} disabled={isBusy}>
            Cancel
          </button>
          <button type="button" className="btn-danger" onClick={onConfirm} disabled={isBusy}>
            {confirmLabel}
          </button>
        </>
      }
    >
      <p className="text-sm text-ink-600">{message}</p>
    </Modal>
  )
}
