import { useCallback, useEffect, useState } from 'react'
import { apiFetch, friendlyMessage } from './api.js'

const STORAGE_KEY = 'schoolAdmission.studentId'

function useNow(active) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) return
    const id = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(id)
  }, [active])
  return now
}

export default function App() {
  const [studentId, setStudentId] = useState(() => localStorage.getItem(STORAGE_KEY) || '')
  const [draftId, setDraftId] = useState(() => localStorage.getItem(STORAGE_KEY) || '')
  const [classes, setClasses] = useState([])
  const [holds, setHolds] = useState([])
  const [banner, setBanner] = useState('')
  const [notice, setNotice] = useState('')
  const [loading, setLoading] = useState(false)
  const [newClass, setNewClass] = useState({ name: '', totalSeats: '' })
  const now = useNow(true)

  const loadClasses = useCallback(async () => {
    try {
      const data = await apiFetch('/classes')
      setClasses(Array.isArray(data) ? data : [])
      setBanner('')
    } catch (e) {
      setBanner(friendlyMessage(e))
    }
  }, [])

  const loadHolds = useCallback(async () => {
    if (!studentId) { setHolds([]); return }
    try {
      const data = await apiFetch('/me/holds', { studentId })
      setHolds(Array.isArray(data) ? data : [])
      setBanner('')
    } catch (e) {
      // Invalid/missing ID: clear holds, explain
      const msg = friendlyMessage(e)
      setBanner(msg)
      if (e.status === 400) setHolds([])
    }
  }, [studentId])

  // Poll seat counts "every few seconds" (3s) and my holds (2s)
  useEffect(() => { loadClasses(); const id = setInterval(loadClasses, 3000); return () => clearInterval(id) }, [loadClasses])
  useEffect(() => { loadHolds(); const id = setInterval(loadHolds, 2000); return () => clearInterval(id) }, [loadHolds])

  function saveStudentId() {
    const id = draftId.trim()
    if (!/^\d{17}$/.test(id)) { setBanner('Please enter the 17-digit birth certificate number (digits only).'); return }
    localStorage.setItem(STORAGE_KEY, id)
    setStudentId(id)
    setNotice('Student ID saved.')
  }

  async function createClass(e) {
    e.preventDefault()
    const seats = Number(newClass.totalSeats)
    if (!newClass.name.trim()) { setBanner('Enter a class name, e.g. "Class 6 - Morning Shift".'); return }
    if (!Number.isInteger(seats) || seats < 1) { setBanner('Total seats must be a whole number of 1 or more.'); return }
    setLoading(true); setBanner('')
    try {
      await apiFetch('/classes', { method: 'POST', body: { name: newClass.name.trim(), totalSeats: seats } })
      setNewClass({ name: '', totalSeats: '' }); setNotice('Class created.'); await loadClasses()
    } catch (err) { setBanner(friendlyMessage(err)) } finally { setLoading(false) }
  }

  async function apply(classId) {
    if (!studentId) { setBanner('Enter your 17-digit birth certificate number above first.'); return }
    if (loading) return
    setLoading(true); setBanner('')
    try {
      await apiFetch(`/classes/${classId}/holds`, { method: 'POST', studentId })
      setNotice('Application submitted — seat held. Pay the fee before it expires.')
      await Promise.all([loadClasses(), loadHolds()])
    } catch (err) { setBanner(friendlyMessage(err)) } finally { setLoading(false) }
  }

  async function confirm(h) {
    setLoading(true); setBanner('')
    try {
      await apiFetch(`/holds/${h.holdId}/confirm`, { method: 'POST', studentId })
      setNotice('Admission confirmed. Payment recorded.')
      await Promise.all([loadClasses(), loadHolds()])
    } catch (err) { setBanner(friendlyMessage(err)); await loadHolds() } finally { setLoading(false) }
  }

  async function cancel(h) {
    setLoading(true); setBanner('')
    try {
      await apiFetch(`/holds/${h.holdId}`, { method: 'DELETE', studentId })
      setNotice('Application cancelled. The seat is free again.')
      await Promise.all([loadClasses(), loadHolds()])
    } catch (err) { setBanner(friendlyMessage(err)) } finally { setLoading(false) }
  }

  function remaining(h) {
    const diff = new Date(h.expiresAt).getTime() - now
    if (h.status !== 'HELD') return null
    if (diff <= 0) return 'Expired'
    const s = Math.floor(diff / 1000)
    const m = Math.floor(s / 60)
    return `${m}m ${String(s % 60).padStart(2, '0')}s left`
  }

  function effectiveStatus(h) {
    if (h.status === 'HELD' && new Date(h.expiresAt).getTime() <= now) return 'EXPIRED'
    return h.status
  }

  return (
    <div className="page">
      <header className="card">
        <h1>School Admission System</h1>
        <p>First come, first served. Applying holds a seat for a limited time — pay the admission fee before it expires to confirm.</p>
        <label className="label">Student birth certificate number (17 digits)</label>
        <div className="row">
          <input value={draftId} onChange={e => setDraftId(e.target.value.replace(/\D/g, '').slice(0, 17))} placeholder="e.g. 20152691234567890" inputMode="numeric" maxLength={17} />
          <button onClick={saveStudentId}>Set student</button>
        </div>
        {studentId && <p className="muted">Current student: <strong>{studentId}</strong></p>}
      </header>

      {banner && <div className="alert error" role="alert">{banner}</div>}
      {notice && <div className="alert ok" role="status">{notice}</div>}

      <section className="card">
        <h2>Create a class (school admin)</h2>
        <form onSubmit={createClass} className="row">
          <input placeholder="Class name e.g. Class 6 - Morning Shift" value={newClass.name} onChange={e => setNewClass({ ...newClass, name: e.target.value })} />
          <input placeholder="Total seats" type="number" min="1" value={newClass.totalSeats} onChange={e => setNewClass({ ...newClass, totalSeats: e.target.value })} />
          <button disabled={loading} type="submit">Create class</button>
        </form>
      </section>

      <section className="card">
        <h2>Classes</h2>
        {classes.length === 0 && <p className="muted">No classes yet. Create one above. Seat counts update automatically every few seconds.</p>}
        <div className="grid">
          {classes.map(c => (
            <article key={c.id} className="tile">
              <h3>{c.name}</h3>
              <p className="muted">Total {c.totalSeats} · Held {c.heldSeats} · Confirmed {c.confirmedSeats} · Available <strong>{c.availableSeats}</strong></p>
              <div className="bar"><span style={{ width: `${c.totalSeats ? ((c.heldSeats + c.confirmedSeats) / c.totalSeats) * 100 : 0}%` }} /></div>
              <button disabled={loading || !studentId || c.availableSeats <= 0} onClick={() => apply(c.id)}>
                {c.availableSeats <= 0 ? 'Full' : 'Apply (আবেদন করুন)'}
              </button>
              {!studentId && <p className="muted">Set a student ID above to apply.</p>}
            </article>
          ))}
        </div>
      </section>

      <section className="card">
        <h2>My applications</h2>
        {!studentId && <p className="muted">Enter your student ID above to see your applications.</p>}
        {studentId && holds.length === 0 && <p className="muted">You have no applications yet.</p>}
        {holds.map(h => {
          const st = effectiveStatus(h)
          const left = remaining(h)
          const canPay = st === 'HELD' && new Date(h.expiresAt).getTime() > now
          return (
            <div key={h.holdId} className="hold">
              <div>
                <strong>{h.className}</strong> <span className={`badge ${st.toLowerCase()}`}>{st}</span>
                <div className="muted">ID {h.holdId} · Expires {new Date(h.expiresAt).toLocaleString()}{left ? ` · ${left}` : ''}</div>
              </div>
              <div className="row">
                <button disabled={loading || !canPay} onClick={() => confirm(h)}>Pay admission fee</button>
                <button disabled={loading || !['HELD', 'CONFIRMED'].includes(st)} onClick={() => cancel(h)}>Cancel</button>
              </div>
            </div>
          )
        })}
      </section>
    </div>
  )
}
