import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { BarChart3, ShieldCheck } from 'lucide-react'
import { useAuth } from '@/context/authContext'
import { errorMessage } from '@/lib/api'
import { Button, Field, TextInput } from '@/components/ui'

export default function LoginPage() {
  const { login, isAuthenticated } = useAuth()
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      await login(email, password)
      navigate('/', { replace: true })
    } catch (err) {
      setError(errorMessage(err, 'Unable to sign in. Check your credentials and try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-ink-900 px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center text-white">
          <span className="mx-auto mb-4 flex size-12 items-center justify-center rounded-xl bg-brand-600">
            <BarChart3 className="size-6" aria-hidden="true" />
          </span>
          <h1 className="text-2xl font-bold">Bank Service Portal</h1>
          <p className="mt-1 text-sm text-ink-300">Sign in to manage service requests</p>
        </div>

        <form onSubmit={handleSubmit} className="card p-6" noValidate>
          <div className="space-y-4">
            <Field label="Email address" htmlFor="email" required>
              <TextInput
                id="email"
                type="email"
                name="email"
                autoComplete="email"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="you@bankportal.com"
              />
            </Field>

            <Field label="Password" htmlFor="password" required>
              <TextInput
                id="password"
                type="password"
                name="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                placeholder="••••••••"
              />
            </Field>
          </div>

          {error && (
            <div className="mt-4 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-800" role="alert">
              <ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}

          <Button type="submit" className="mt-5 w-full" isLoading={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>

          <p className="mt-5 border-t border-ink-200 pt-4 text-center text-sm text-ink-500">
            Need an account?{' '}
            <Link to="/register" className="font-semibold text-brand-600 hover:text-brand-700">
              Create one
            </Link>
          </p>
        </form>

        <div className="mt-6 rounded-lg border border-ink-700 bg-ink-800/70 p-4 text-xs text-ink-300">
          <p className="mb-2 font-semibold text-white">Demo accounts (password: Password@123)</p>
          <ul className="grid gap-1 sm:grid-cols-2">
            <li>admin@bankportal.com — Admin</li>
            <li>manager@bankportal.com — Manager</li>
            <li>support1@bankportal.com — Support</li>
            <li>employee1@bankportal.com — Employee</li>
          </ul>
        </div>
      </div>
    </div>
  )
}
