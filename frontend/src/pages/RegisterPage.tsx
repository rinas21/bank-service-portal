import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { ArrowLeft, BarChart3 } from 'lucide-react'
import { useAuth } from '@/context/authContext'
import { errorMessage } from '@/lib/api'
import { Button, Field, TextInput } from '@/components/ui'

export default function RegisterPage() {
  const { register, isAuthenticated } = useAuth()
  const navigate = useNavigate()

  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [employeeNumber, setEmployeeNumber] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')

    if (password !== confirmPassword) {
      setError('The two passwords do not match.')
      return
    }

    setIsSubmitting(true)
    try {
      await register({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        password,
        employeeNumber: employeeNumber.trim() || undefined,
      })
      navigate('/', { replace: true })
    } catch (err) {
      setError(errorMessage(err, 'Unable to create your account.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-ink-900 px-4 py-10">
      <div className="w-full max-w-lg">
        <div className="mb-8 text-center text-white">
          <span className="mx-auto mb-4 flex size-12 items-center justify-center rounded-xl bg-brand-600">
            <BarChart3 className="size-6" aria-hidden="true" />
          </span>
          <h1 className="text-2xl font-bold">Create your account</h1>
          <p className="mt-1 text-sm text-ink-300">
            New accounts start with the Employee role. An administrator can assign a branch later.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="card p-6" noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="First name" htmlFor="firstName" required>
              <TextInput
                id="firstName"
                required
                autoComplete="given-name"
                value={firstName}
                onChange={(event) => setFirstName(event.target.value)}
              />
            </Field>

            <Field label="Last name" htmlFor="lastName" required>
              <TextInput
                id="lastName"
                required
                autoComplete="family-name"
                value={lastName}
                onChange={(event) => setLastName(event.target.value)}
              />
            </Field>

            <div className="sm:col-span-2">
              <Field label="Email address" htmlFor="register-email" required>
                <TextInput
                  id="register-email"
                  type="email"
                  required
                  autoComplete="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                />
              </Field>
            </div>

            <div className="sm:col-span-2">
              <Field label="Employee number" htmlFor="employeeNumber" hint="Optional">
                <TextInput
                  id="employeeNumber"
                  value={employeeNumber}
                  onChange={(event) => setEmployeeNumber(event.target.value)}
                  placeholder="EMP-1234"
                />
              </Field>
            </div>

            <Field label="Password" htmlFor="register-password" hint="At least 8 characters" required>
              <TextInput
                id="register-password"
                type="password"
                required
                minLength={8}
                autoComplete="new-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </Field>

            <Field label="Confirm password" htmlFor="confirmPassword" required>
              <TextInput
                id="confirmPassword"
                type="password"
                required
                minLength={8}
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
              />
            </Field>
          </div>

          {error && (
            <div
              className="mt-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-800"
              role="alert"
            >
              {error}
            </div>
          )}

          <Button type="submit" className="mt-5 w-full" isLoading={isSubmitting}>
            {isSubmitting ? 'Creating account…' : 'Create account'}
          </Button>

          <p className="mt-5 border-t border-ink-200 pt-4 text-center text-sm text-ink-500">
            <Link
              to="/login"
              className="inline-flex items-center gap-1 font-semibold text-brand-600 hover:text-brand-700"
            >
              <ArrowLeft className="size-4" aria-hidden="true" />
              Back to sign in
            </Link>
          </p>
        </form>
      </div>
    </div>
  )
}
