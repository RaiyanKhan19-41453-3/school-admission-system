import { useCallback, useEffect, useState } from 'react'
import { apiFetch, friendlyMessage, getToken, setToken } from './api.js'

function useNow() {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(id)
  }, [])
  return now
}

export default function App() {
  const [token, setTok] = useState(getToken())
  const [profile, setProfile] = useState(null)
  const [classes, setClasses] = useState([])
  const [years, setYears] = useState([])
  const [myApps, setMyApps] = useState([])
  const [auth, setAuth] = useState({ nid: '', password: '', fullName: '', mode: 'login' })
  const [payFor, setPayFor] = useState(null)
  const [pay, setPay] = useState({ method: 'BKASH', transactionRef: '' })
  const [banner, setBanner] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const now = useNow()

  const authed = !!token

  const loadAll = useCallback(async () => {
    if (!token) return
    try {
      const [c, y, a, p] = await Promise.all([
        apiFetch('/classes'),
        apiFetch('/api/years'),
        apiFetch('/api/applications/my'),
        apiFetch('/api/auth/me'),
      ])
      setClasses(c); setYears(y); setMyApps(a); setProfile(p)
    } catch (e) {
      if (e.status === 401) { setToken(null); setTok(null); setProfile(null) }
      else setBanner(friendlyMessage(e))
    }
  }, [token])

  useEffect(() => { loadAll(); const id = setInterval(loadAll, 3000); return () => clearInterval(id) }, [loadAll])

  async function submitAuth(e) {
    e.preventDefault(); setBusy(true); setBanner('')
    try {
      const path = auth.mode === 'login' ? '/api/auth/login' : '/api/auth/register'
      const body = auth.mode === 'login'
        ? { nid: auth.nid, password: auth.password }
        : { nid: auth.nid, password: auth.password, fullName: auth.fullName }
      const r = await apiFetch(path, { method: 'POST', body, token: null })
      setToken(r.token); setTok(r.token); setNotice('Signed in as ' + r.fullName)
    } catch (err) { setBanner(friendlyMessage(err)) } finally { setBusy(false) }
  }

  function logout() { setToken(null); setTok(null); setProfile(null); setMyApps([]); setNotice('Signed out.') }

  async function apply(classId) {
    setBusy(true); setBanner('')
    try {
      await apiFetch(`/api/applications/classes/${classId}/apply`, { method: 'POST' })
      setNotice('Application submitted — pay the fee before it expires.'); await loadAll()
    } catch (e) { setBanner(friendlyMessage(e)) } finally { setBusy(false) }
  }

  async function doPay() {
    setBusy(true); setBanner('')
    try {
      await apiFetch(`/api/applications/${payFor}/pay`, { method: 'POST', body: pay })
      setNotice('Payment recorded — admission confirmed.'); setPayFor(null); await loadAll()
    } catch (e) { setBanner(friendlyMessage(e)) } finally { setBusy(false) }
  }

  async function cancel(id) {
    setBusy(true); setBanner('')
    try { await apiFetch(`/api/applications/${id}`, { method: 'DELETE' }); setNotice('Application cancelled.'); await loadAll() }
    catch (e) { setBanner(friendlyMessage(e)) } finally { setBusy(false) }
  }

  function effective(s) {
    if (s.status === 'HELD' && new Date(s.expiresAt).getTime() <= now) return 'EXPIRED'
    return s.status
  }
  function left(s) {
    if (effective(s) !== 'HELD') return null
    const sec = Math.floor((new Date(s.expiresAt).getTime() - now) / 1000)
    if (sec <= 0) return 'Expired'
    return `${Math.floor(sec / 60)}m ${String(sec % 60).padStart(2, '0')}s left`
  }
  const money = n => 'TK ' + Number(n).toLocaleString()

  return (
    <div className="page">
      <header className="card">
        <h1>School Admission System</h1>
        <p>First come, first served · seat held until you pay · {years.find(y => y.isActive)?.name || 'no active year'}</p>
        {!authed ? (
          <form onSubmit={submitAuth} className="row">
            <input placeholder="17-digit NID" value={auth.nid} onChange={e => setAuth({ ...auth, nid: e.target.value.replace(/\D/g, '').slice(0, 17) })} />
            {auth.mode === 'register' && <input placeholder="Full name" value={auth.fullName} onChange={e => setAuth({ ...auth, fullName: e.target.value })} />}
            <input type="password" placeholder="Password" value={auth.password} onChange={e => setAuth({ ...auth, password: e.target.value })} />
            <button disabled={busy} type="submit">{auth.mode === 'login' ? 'Sign in' : 'Register'}</button>
            <button type="button" onClick={() => setAuth({ ...auth, mode: auth.mode === 'login' ? 'register' : 'login' })}>
              {auth.mode === 'login' ? 'Need an account?' : 'Have an account?'}
            </button>
          </form>
        ) : (
          <div className="row">
            <span>Signed in as <strong>{profile?.fullName}</strong> ({profile?.nid})</span>
            <button type="button" onClick={logout}>Sign out</button>
          </div>
        )}
      </header>

      {banner && <div className="alert error" role="alert">{banner}</div>}
      {notice && <div className="alert ok" role="status">{notice}</div>}

      <section className="card">
        <h2>Classes</h2>
        {classes.length === 0 && <p className="muted">No classes yet — an admin creates them for the active year.</p>}
        <div className="grid">
          {classes.map(c => (
            <article key={c.id} className="tile">
              <h3>{c.name}</h3>
              <p className="muted">{c.grade} · {c.shift || 'General'} · Fee {money(c.feeAmount)}</p>
              <p className="muted">Total {c.totalSeats} · Held {c.heldSeats} · Confirmed {c.confirmedSeats} · Available <strong>{c.availableSeats}</strong></p>
              <div className="bar"><span style={{ width: `${c.totalSeats ? ((c.heldSeats + c.confirmedSeats) / c.totalSeats) * 100 : 0}%` }} /></div>
              <button disabled={busy || !authed || !c.isActive || c.availableSeats <= 0} onClick={() => apply(c.id)}>
                {c.availableSeats <= 0 ? 'Full' : 'Apply (আবেদন করুন)'}
              </button>
            </article>
          ))}
        </div>
      </section>

      <section className="card">
        <h2>My applications</h2>
        {!authed && <p className="muted">Sign in to see your applications.</p>}
        {authed && myApps.length === 0 && <p className="muted">You have no applications yet.</p>}
        {myApps.map(a => {
          const st = effective(a)
          return (
            <div key={a.holdId} className="hold">
              <div>
                <strong>{a.className}</strong> {a.grade} · {a.shift} <span className={`badge ${st.toLowerCase()}`}>{st}</span>
                <div className="muted">Fee {money(a.feeAmount)} · Expires {new Date(a.expiresAt).toLocaleString()}{left(a) ? ` · ${left(a)}` : ''}{a.transactionRef ? ` · Ref ${a.transactionRef}` : ''}</div>
              </div>
              <div className="row">
                {st === 'HELD' && <button disabled={busy || new Date(a.expiresAt).getTime() <= now} onClick={() => setPayFor(a.holdId)}>Pay admission fee</button>}
                {['HELD', 'CONFIRMED'].includes(st) && <button disabled={busy} onClick={() => cancel(a.holdId)}>Cancel</button>}
              </div>
            </div>
          )
        })}
      </section>

      {payFor && (
        <section className="card">
          <h2>Pay admission fee</h2>
          <p className="muted">Payment gateway is simulated — enter a reference to record it.</p>
          <div className="row">
            <select value={pay.method} onChange={e => setPay({ ...pay, method: e.target.value })}>
              <option>BKASH</option><option>CARD</option><option>BANK</option><option>MOCK</option>
            </select>
            <input placeholder="Transaction reference" value={pay.transactionRef} onChange={e => setPay({ ...pay, transactionRef: e.target.value })} />
            <button disabled={busy} onClick={doPay}>Confirm payment</button>
            <button type="button" onClick={() => setPayFor(null)}>Close</button>
          </div>
        </section>
      )}
    </div>
  )
}
