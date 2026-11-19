export const API_BASE = (import.meta.env.VITE_API_URL || 'http://localhost:8080').replace(/\/$/, '')

const TOKEN_KEY = 'admissions.token'

export function getToken() { return localStorage.getItem(TOKEN_KEY) }
export function setToken(t) { t ? localStorage.setItem(TOKEN_KEY, t) : localStorage.removeItem(TOKEN_KEY) }

export function friendlyMessage(err) {
  const map = {
    NO_SEATS: 'No seats are available — this class is full.',
    DUPLICATE_ACTIVE_HOLD: 'You already have an active application or confirmed admission for this class.',
    EXPIRED: 'Your application has expired. Please apply again.',
    CANCELLED: 'This application was cancelled.',
    FORBIDDEN: 'This application belongs to another student.',
    NOT_FOUND: 'The class or application could not be found.',
    NID_EXISTS: 'An account with this NID already exists.',
  }
  if (err?.code && map[err.code]) return map[err.code]
  if (err?.message?.startsWith('SERVER')) return 'Server error. Please try again in a moment.'
  return err?.message || 'Something went wrong. Please try again.'
}

export async function apiFetch(path, { method = 'GET', body, token = getToken() } = {}) {
  const headers = { 'Content-Type': 'application/json' }
  if (token) headers['Authorization'] = 'Bearer ' + token
  let res
  try {
    res = await fetch(`${API_BASE}${path}`, { method, headers, body: body ? JSON.stringify(body) : undefined })
  } catch {
    const e = new Error('Cannot reach the server. Is the backend running on ' + API_BASE + '?')
    e.code = 'NETWORK'
    throw e
  }
  if (res.status === 204) return null
  const text = await res.text()
  let data = null
  try { data = text ? JSON.parse(text) : null } catch { data = null }
  if (!res.ok) {
    const e = new Error(data?.message || `Request failed (${res.status})`)
    e.code = data?.error || `HTTP_${res.status}`
    e.status = res.status
    throw e
  }
  return data
}
