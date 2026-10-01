import { Link } from 'react-router-dom'
import { Compass } from 'lucide-react'
import { useAuth } from '@/context/authContext'

export default function NotFoundPage() {
  const { isAuthenticated } = useAuth()

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 px-4 text-center">
      <span className="flex size-14 items-center justify-center rounded-full bg-ink-200 text-ink-500">
        <Compass className="size-7" aria-hidden="true" />
      </span>
      <h1 className="text-2xl font-bold text-ink-900">Page not found</h1>
      <p className="max-w-md text-sm text-ink-500">
        The page you are looking for does not exist or has been moved.
      </p>
      <Link to={isAuthenticated ? '/' : '/login'} className="btn-primary">
        {isAuthenticated ? 'Back to dashboard' : 'Go to sign in'}
      </Link>
    </div>
  )
}
