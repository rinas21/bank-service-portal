import type { ApiErrorBody } from '@/types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

const TOKEN_STORAGE_KEY = 'bsp.auth'

/** A failed API call, carrying the server's structured error payload. */
export class ApiError extends Error {
  readonly status: number
  readonly errors: Record<string, string[]>
  readonly traceId: string | undefined

  constructor(status: number, body: ApiErrorBody | null, fallbackMessage: string) {
    super(body?.message || fallbackMessage)
    this.name = 'ApiError'
    this.status = status
    this.errors = body?.errors ?? {}
    this.traceId = body?.traceId
  }
}

/** Turns any thrown value into a message suitable for showing to the user. */
export function errorMessage(error: unknown, fallback = 'Something went wrong. Please try again.') {
  if (error instanceof ApiError) {
    const details = Object.values(error.errors).flat()
    return details.length > 0 ? `${error.message} ${details.join(' ')}` : error.message
  }
  if (error instanceof Error) return error.message
  return fallback
}

interface StoredAuth {
  token: string
  expiresAt: string
  userId: string
  email: string
  fullName: string
  employeeNumber: string
  branchName: string | null
  roles: string[]
}

function readStoredAuth(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(TOKEN_STORAGE_KEY)
    return raw ? (JSON.parse(raw) as StoredAuth) : null
  } catch {
    // A corrupted entry must not stop the app from loading.
    localStorage.removeItem(TOKEN_STORAGE_KEY)
    return null
  }
}

/**
 * Reads the persisted session synchronously so the very first render already
 * knows whether a user is signed in, avoiding a loading flash and an extra
 * effect.
 */
export function restoreSession(): StoredAuth | null {
  const stored = readStoredAuth()
  if (!stored) return null

  // Drop the session locally once the token has expired so the UI redirects to
  // login instead of firing requests that will 401.
  if (new Date(stored.expiresAt).getTime() <= Date.now()) {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
    return null
  }

  return stored
}

export const authStore = {
  get: restoreSession,
  set(auth: StoredAuth) {
    localStorage.setItem(TOKEN_STORAGE_KEY, JSON.stringify(auth))
  },
  clear() {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
  },
}

let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

type QueryValue = string | number | boolean | null | undefined

function buildUrl(path: string, query?: Record<string, QueryValue>) {
  const url = `${API_BASE_URL}${path}`
  if (!query) return url

  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value === null || value === undefined || value === '') continue
    params.append(key, String(value))
  }

  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: ApiErrorBody | null
  try {
    body = (await response.json()) as ApiErrorBody
  } catch {
    body = null
  }
  return new ApiError(
    response.status,
    body,
    `Request failed with status ${response.status}.`,
  )
}

async function request<T>(
  method: string,
  path: string,
  options: { query?: Record<string, QueryValue>; body?: unknown; formData?: FormData; signal?: AbortSignal } = {},
): Promise<T> {
  const headers: Record<string, string> = {}
  const storedAuth = readStoredAuth()
  const auth = restoreSession()

  if (auth) {
    headers.Authorization = `Bearer ${auth.token}`
  }

  let payload: BodyInit | undefined
  if (options.formData) {
    payload = options.formData
  } else if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(options.body)
  }

  const response = await fetch(buildUrl(path, options.query), {
    method,
    headers,
    body: payload,
    signal: options.signal,
  })

  if (response.status === 401) {
    // A 401 while a session is stored means the session is no longer valid, so
    // drop it and sign the user out. restoreSession() removes an already-expired
    // token before the request is sent, so `storedAuth` is checked as well as
    // `auth`; otherwise an expired token would leave the user stuck on
    // authenticated screens. With no stored session this is the response to the
    // credential check itself, and the server's message is the useful thing to
    // show.
    if (auth || storedAuth) {
      authStore.clear()
      onUnauthorized?.()
      throw new ApiError(401, null, 'Your session has expired. Please sign in again.')
    }

    throw await toApiError(response)
  }

  if (!response.ok) {
    throw await toApiError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string, query?: Record<string, QueryValue>, signal?: AbortSignal) =>
    request<T>('GET', path, { query, signal }),
  post: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<T>('POST', path, { body, signal }),
  put: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<T>('PUT', path, { body, signal }),
  patch: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    request<T>('PATCH', path, { body, signal }),
  delete: <T>(path: string, signal?: AbortSignal) => request<T>('DELETE', path, { signal }),
  upload: <T>(path: string, formData: FormData, signal?: AbortSignal) =>
    request<T>('POST', path, { formData, signal }),
}
