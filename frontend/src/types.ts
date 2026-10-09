/**
 * Types mirroring the API contracts in BankService.Application/DTOs.
 * Enums are transmitted as strings by the API (JsonStringEnumConverter).
 */

export type RequestStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed'
export type RequestPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected'
export type Role = 'Admin' | 'Manager' | 'Support' | 'Employee'

export interface AuthResponse {
  token: string
  expiresAt: string
  userId: string
  email: string
  fullName: string
  employeeNumber: string
  branchName: string | null
  roles: string[]
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ServiceRequestSummary {
  id: number
  requestNumber: string
  title: string
  category: string
  status: RequestStatus
  priority: RequestPriority
  requesterName: string
  assignedToName: string | null
  branchName: string | null
  requiresApproval: boolean
  isMigrated: boolean
  createdAt: string
  dueDate: string | null
  resolvedAt: string | null
  commentCount: number
}

export interface Comment {
  id: number
  authorId: string
  authorName: string
  body: string
  isInternal: boolean
  createdAt: string
}

export interface StatusHistoryEntry {
  id: number
  fromStatus: string
  toStatus: string
  changedByName: string
  reason: string | null
  changedAt: string
}

export interface Assignment {
  id: number
  assigneeId: string
  assigneeName: string
  assignedByName: string
  note: string | null
  assignedAt: string
  unassignedAt: string | null
}

export interface Approval {
  id: number
  requestedByName: string
  approverName: string | null
  status: ApprovalStatus
  reason: string | null
  decisionNote: string | null
  requestedAt: string
  decidedAt: string | null
}

export interface ServiceRequestDetail {
  id: number
  requestNumber: string
  title: string
  description: string
  category: string
  status: RequestStatus
  priority: RequestPriority
  requesterId: string
  requesterName: string
  branchId: number | null
  branchName: string | null
  assignedToId: string | null
  assignedToName: string | null
  requiresApproval: boolean
  isMigrated: boolean
  legacyReference: string | null
  createdAt: string
  updatedAt: string | null
  resolvedAt: string | null
  closedAt: string | null
  dueDate: string | null
  comments: Comment[]
  statusHistory: StatusHistoryEntry[]
  assignments: Assignment[]
  approvals: Approval[]
}

export interface CountByStatus {
  status: RequestStatus
  count: number
}

export interface CountByPriority {
  priority: RequestPriority
  count: number
}

export interface CountByCategory {
  category: string
  count: number
}

export interface RecentRequest {
  id: number
  requestNumber: string
  title: string
  status: RequestStatus
  priority: RequestPriority
  requesterName: string
  createdAt: string
}

export interface DashboardStats {
  totalRequests: number
  openRequests: number
  inProgressRequests: number
  resolvedRequests: number
  closedRequests: number
  criticalRequests: number
  pendingApprovals: number
  overdueRequests: number
  requestsThisWeek: number
  requestsByStatus: CountByStatus[]
  requestsByPriority: CountByPriority[]
  requestsByCategory: CountByCategory[]
  recentRequests: RecentRequest[]
}

export interface User {
  id: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  employeeNumber: string | null
  branchId: number | null
  branchName: string | null
  isActive: boolean
  createdAt: string
  lastLoginAt: string | null
  roles: string[]
}

export interface AssignableUser {
  id: string
  fullName: string
  roles: string[]
}

export interface Branch {
  id: number
  code: string
  name: string
  city: string
  address: string | null
  phone: string | null
  isActive: boolean
  userCount: number
  openRequestCount: number
  createdAt: string
}

export type AuditAction =
  | 'UserCreated'
  | 'UserUpdated'
  | 'UserDeactivated'
  | 'BranchCreated'
  | 'BranchUpdated'
  | 'RequestCreated'
  | 'RequestUpdated'
  | 'RequestAssigned'
  | 'StatusChanged'
  | 'CommentAdded'
  | 'ApprovalRequested'
  | 'ApprovalGranted'
  | 'ApprovalRejected'
  | 'CsvImport'
  | 'Login'
  | 'LoginFailed'

export interface AuditLog {
  id: number
  userId: string | null
  userName: string | null
  action: AuditAction
  entityType: string
  entityId: string | null
  details: string | null
  ipAddress: string | null
  timestamp: string
}

export interface CsvImportRowResult {
  rowNumber: number
  status: 'Imported' | 'Duplicate' | 'Rejected'
  legacyReference: string | null
  message: string
}

export interface CsvImportResult {
  totalRecords: number
  imported: number
  duplicates: number
  rejected: number
  rowResults: CsvImportRowResult[]
}

export interface ApiErrorBody {
  statusCode: number
  message: string
  errors?: Record<string, string[]>
  traceId?: string
}
