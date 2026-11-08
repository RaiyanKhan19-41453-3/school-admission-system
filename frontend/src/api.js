export const API_BASE = (import.meta.env.VITE_API_URL || 'http://localhost:8080').replace(/\/$/, '')

export function friendlyMessage(err) {
  if (err?.code === 'NO_SEATS') return 'No seats are available — this class is full.'
  if (err?.code === 'DUPLICATE_ACTIVE_HOLD') return 'You already have an active or confirmed application for this class.'
  if (err?.code === 'EXPIRED') return 'Your application has expired. Please apply again.'
  if (err?.code === 'CANCELLED') return 'This application was cancelled.'
  if (err?.code === 'FORBIDDEN') return 'This application belongs to another student.'
  if (err?.code === 'NOT_FOUND') return 'The class or application could not be found.'
  if (err?.message?.startsWith('SERVER')) return 'Server error. Please try again in a moment.'
  return err?.message || 'Something went wrong. Please try again.'
}

export async function apiFetch(path, { method = 'GET', studentId, body } = {}) {
  const headers = { 'Content-Type': 'application/json' }
  if (studentId) headers['X-Student-Id'] = studentId
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
