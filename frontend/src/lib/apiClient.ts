import { api } from '@/lib/api'
import type {
  AuditAction,
  AuditLog,
  AuthResponse,
  Branch,
  CsvImportResult,
  DashboardStats,
  PagedResult,
  RequestPriority,
  RequestStatus,
  ServiceRequestDetail,
  ServiceRequestSummary,
  User,
} from '@/types'

export interface ServiceRequestFilters {
  search?: string
  status?: RequestStatus | ''
  priority?: RequestPriority | ''
  category?: string
  branchId?: number | ''
  assignedToId?: string
  requesterId?: string
  requiresApproval?: boolean | ''
  sortBy?: string
  descending?: boolean
  page?: number
  pageSize?: number
}

export const authApi = {
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/auth/login', { email, password }),

  register: (input: {
    firstName: string
    lastName: string
    email: string
    password: string
    employeeNumber?: string
    branchId?: number
  }) => api.post<AuthResponse>('/auth/register', input),

  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<void>('/auth/change-password', { currentPassword, newPassword }),
}

export const requestApi = {
  list: (filters: ServiceRequestFilters, signal?: AbortSignal) =>
    api.get<PagedResult<ServiceRequestSummary>>(
      '/servicerequests',
      {
        search: filters.search,
        status: filters.status,
        priority: filters.priority,
        category: filters.category,
        branchId: filters.branchId === '' ? undefined : filters.branchId,
        assignedToId: filters.assignedToId,
        requesterId: filters.requesterId,
        requiresApproval: filters.requiresApproval === '' ? undefined : filters.requiresApproval,
        sortBy: filters.sortBy,
        descending: filters.descending,
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 10,
      },
      signal,
    ),

  get: (id: number, signal?: AbortSignal) =>
    api.get<ServiceRequestDetail>(`/servicerequests/${id}`, undefined, signal),

  create: (input: {
    title: string
    description: string
    category: string
    priority: RequestPriority
    branchId?: number | null
    dueDate?: string | null
  }) => api.post<ServiceRequestDetail>('/servicerequests', input),

  update: (
    id: number,
    input: {
      title: string
      description: string
      category: string
      priority: RequestPriority
      branchId?: number | null
      dueDate?: string | null
    },
  ) => api.put<ServiceRequestDetail>(`/servicerequests/${id}`, input),

  updateStatus: (id: number, status: RequestStatus, reason?: string) =>
    api.patch<ServiceRequestDetail>(`/servicerequests/${id}/status`, { status, reason }),

  assign: (id: number, assigneeId: string, note?: string) =>
    api.post<ServiceRequestDetail>(`/servicerequests/${id}/assign`, { assigneeId, note }),

  addComment: (id: number, body: string, isInternal: boolean) =>
    api.post<ServiceRequestDetail>(`/servicerequests/${id}/comments`, { body, isInternal }),

  requestApproval: (id: number, note?: string) =>
    api.post<ServiceRequestDetail>(`/servicerequests/${id}/approval`, { approve: false, note }),

  decideApproval: (id: number, approve: boolean, note?: string) =>
    api.post<ServiceRequestDetail>(`/servicerequests/${id}/approval/decision`, { approve, note }),
}

export const dashboardApi = {
  get: (signal?: AbortSignal) => api.get<DashboardStats>('/dashboard', undefined, signal),
}

export const userApi = {
  list: (params: { search?: string; role?: string; isActive?: boolean | ''; page?: number; pageSize?: number }, signal?: AbortSignal) =>
    api.get<PagedResult<User>>(
      '/users',
      {
        search: params.search,
        role: params.role,
        isActive: params.isActive === '' ? undefined : params.isActive,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 10,
      },
      signal,
    ),

  assignable: (signal?: AbortSignal) => api.get<User[]>('/users/assignable', undefined, signal),

  get: (id: string, signal?: AbortSignal) => api.get<User>(`/users/${id}`, undefined, signal),

  create: (input: {
    firstName: string
    lastName: string
    email: string
    password: string
    employeeNumber?: string
    branchId?: number | null
    roles: string[]
  }) => api.post<User>('/users', input),

  update: (
    id: string,
    input: {
      firstName: string
      lastName: string
      email: string
      employeeNumber?: string
      branchId?: number | null
      roles: string[]
    },
  ) => api.put<User>(`/users/${id}`, input),

  deactivate: (id: string) => api.post<void>(`/users/${id}/deactivate`),
  reactivate: (id: string) => api.post<void>(`/users/${id}/reactivate`),
}

export const branchApi = {
  list: (params: { search?: string; isActive?: boolean | ''; page?: number; pageSize?: number }, signal?: AbortSignal) =>
    api.get<PagedResult<Branch>>(
      '/branches',
      {
        search: params.search,
        isActive: params.isActive === '' ? undefined : params.isActive,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 10,
      },
      signal,
    ),

  get: (id: number, signal?: AbortSignal) => api.get<Branch>(`/branches/${id}`, undefined, signal),

  create: (input: { code: string; name: string; city: string; address?: string; phone?: string }) =>
    api.post<Branch>('/branches', input),

  update: (
    id: number,
    input: { code: string; name: string; city: string; address?: string; phone?: string; isActive: boolean },
  ) => api.put<Branch>(`/branches/${id}`, input),
}

export const auditApi = {
  list: (
    params: {
      search?: string
      action?: AuditAction | ''
      userId?: string
      from?: string
      to?: string
      page?: number
      pageSize?: number
    },
    signal?: AbortSignal,
  ) =>
    api.get<PagedResult<AuditLog>>(
      '/auditlogs',
      {
        search: params.search,
        action: params.action,
        userId: params.userId,
        from: params.from,
        to: params.to,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      },
      signal,
    ),
}

export const migrationApi = {
  importLegacyRequests: (file: File, signal?: AbortSignal) => {
    const formData = new FormData()
    formData.append('file', file)
    return api.upload<CsvImportResult>('/migration/import', formData, signal)
  },
}
