import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { authStore, restoreSession, setUnauthorizedHandler } from '@/lib/api'
import { authApi } from '@/lib/apiClient'
import { AuthContext } from '@/context/authContext'
import type { AuthContextValue, RegisterInput, SessionUser } from '@/context/authContext'
import type { AuthResponse } from '@/types'

function toSessionUser(auth: AuthResponse): SessionUser {
  return {
    userId: auth.userId,
    email: auth.email,
    fullName: auth.fullName,
    employeeNumber: auth.employeeNumber,
    branchName: auth.branchName,
    roles: auth.roles,
  }
}

function persist(auth: AuthResponse) {
  authStore.set({
    token: auth.token,
    expiresAt: auth.expiresAt,
    userId: auth.userId,
    email: auth.email,
    fullName: auth.fullName,
    employeeNumber: auth.employeeNumber,
    branchName: auth.branchName,
    roles: auth.roles,
  })
}

export function AuthProvider({ children }: { children: ReactNode }) {
  // The token store is read synchronously, so the first render already knows
  // the session and no restore effect (or loading state) is needed.
  const [user, setUser] = useState<SessionUser | null>(() => {
    const stored = restoreSession()
    return stored ? toSessionUser(stored) : null
  })

  // A 401 from any request (for example an expired token) ends the session.
  useEffect(() => {
    setUnauthorizedHandler(() => setUser(null))
    return () => setUnauthorizedHandler(null)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const auth = await authApi.login(email, password)
    persist(auth)
    setUser(toSessionUser(auth))
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    const auth = await authApi.register(input)
    persist(auth)
    setUser(toSessionUser(auth))
  }, [])

  const logout = useCallback(() => {
    authStore.clear()
    setUser(null)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      login: async (email, password) => login(email.trim(), password),
      register,
      logout,
      hasRole: (...roles) => roles.some((role) => user?.roles.includes(role) ?? false),
      hasAnyRole: (...roles) => roles.some((role) => user?.roles.includes(role) ?? false),
    }),
    [user, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
