import { Navigate, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
import AppLayout from '@/components/AppLayout'
import { useAuth } from '@/context/authContext'
import LoginPage from '@/pages/LoginPage'
import RegisterPage from '@/pages/RegisterPage'
import DashboardPage from '@/pages/DashboardPage'
import RequestsPage from '@/pages/RequestsPage'
import NewRequestPage from '@/pages/NewRequestPage'
import RequestDetailPage from '@/pages/RequestDetailPage'
import UsersPage from '@/pages/UsersPage'
import BranchesPage from '@/pages/BranchesPage'
import AuditLogsPage from '@/pages/AuditLogsPage'
import MigrationPage from '@/pages/MigrationPage'
import NotFoundPage from '@/pages/NotFoundPage'

function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()

  // The session is restored synchronously, so this renders on the first pass.
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return <>{children}</>
}

function RequireRole({ roles, children }: { roles: string[]; children: ReactNode }) {
  const { hasAnyRole } = useAuth()
  // An authenticated user without the role falls back to the dashboard
  // instead of seeing a blank page.
  if (!hasAnyRole(...(roles as never[]))) return <Navigate to="/" replace />
  return <>{children}</>
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="requests" element={<RequestsPage />} />
        <Route path="requests/new" element={<NewRequestPage />} />
        <Route path="requests/:id" element={<RequestDetailPage />} />

        <Route
          path="users"
          element={
            <RequireRole roles={['Admin']}>
              <UsersPage />
            </RequireRole>
          }
        />
        <Route
          path="branches"
          element={
            <RequireRole roles={['Admin', 'Manager']}>
              <BranchesPage />
            </RequireRole>
          }
        />
        <Route
          path="audit-logs"
          element={
            <RequireRole roles={['Admin', 'Manager']}>
              <AuditLogsPage />
            </RequireRole>
          }
        />
        <Route
          path="migration"
          element={
            <RequireRole roles={['Admin']}>
              <MigrationPage />
            </RequireRole>
          }
        />
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
