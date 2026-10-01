import { createContext, useContext } from 'react'
import type { Role } from '@/types'

export interface SessionUser {
  userId: string
  email: string
  fullName: string
  employeeNumber: string
  branchName: string | null
  roles: string[]
}

export interface RegisterInput {
  firstName: string
  lastName: string
  email: string
  password: string
  employeeNumber?: string
  branchId?: number
}

export interface AuthContextValue {
  user: SessionUser | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (input: RegisterInput) => Promise<void>
  logout: () => void
  hasRole: (...roles: Role[]) => boolean
  hasAnyRole: (...roles: Role[]) => boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used inside an AuthProvider.')
  }
  return context
}
