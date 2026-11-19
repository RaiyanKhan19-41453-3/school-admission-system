import { useEffect, useState } from 'react'
import { apiFetch, friendlyMessage } from '../api.js'

function useNow() {
  const [now, setNow] = useState(Date.now())
  useEffect(() => { const i = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(i) }, [])
  return now
}
const eff = (s, now) => s.status === 'HELD' && new Date(s.expiresAt).getTime() <= now ? 'EXPIRED' : s.status
const money = n => 'TK ' + Number(n).toLocaleString()

export default function ParentPage() {
  const now = useNow()
  const [classes, setClasses] = useState([])
  const [apps, setApps] = useState([])
  const [banner, setBanner] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [payFor, setPayFor] = useState(null)
  const [pay, setPay] = useState({ method: 'BKASH', transactionRef: '' })

  const load = async () => {
    try {
      const [c, a] = await Promise.all([apiFetch('/classes'), apiFetch('/api/applications/my')])
      setClasses(c); setApps(a)
    } catch (e) { setBanner(friendlyMessage(e)) }
  }
  useEffect(() => { load(); const i = setInterval(load, 3000); return () => clearInterval(i) }, [])

  async function apply(id) {
    setBusy(true); setBanner('')
    try { await apiFetch(`/api/applications/classes/${id}/apply`, { method: 'POST' }); setNotice('Seat held — pay before it expires.'); await load() }
    catch (e) { setBanner(friendlyMessage(e)) } finally { setBusy(false) }
  }
  async function doPay() {
    setBusy(true); setBanner('')
    try {
      await apiFetch(`/api/applications/${payFor}/pay`, { method: 'POST', body: pay })
      setNotice('Payment recorded — admission confirmed.'); setPayFor(null); await load()
    } catch (e) { setBanner(friendlyMessage(e)) } finally { setBusy(false) }
  }
  async function cancel(id) {
    setBusy(true)
    try { await apiFetch(`/api/applications/${id}`, { method: 'DELETE' }); setNotice('Application cancelled.'); await load() }
    catch (e) { setBanner(friendlyMessage(e)) } finally { setBusy(false) }
  }

  return (
    <div>
      <h1>Available classes</h1>
      <p className="muted">Seat counts refresh automatically. Apply to hold a seat, then pay the fee before it expires.</p>
      {banner && <div className="alert error">{banner}</div>}
      {notice && <div className="alert ok">{notice}</div>}

      <div className="grid">
        {classes.map(c => (
          <article key={c.id} className="tile">
            <h3>{c.name}</h3>
            <p className="muted">{c.grade} · {c.shift || 'General'} · Fee {money(c.feeAmount)}</p>
            <p className="muted">Total {c.totalSeats} · Held {c.heldSeats} · Confirmed {c.confirmedSeats} · Available <strong>{c.availableSeats}</strong></p>
            <div className="bar"><span style={{ width: `${c.totalSeats ? ((c.heldSeats + c.confirmedSeats) / c.totalSeats) * 100 : 0}%` }} /></div>
            <button disabled={busy || !c.isActive || c.availableSeats <= 0} onClick={() => apply(c.id)}>
              {c.availableSeats <= 0 ? 'Full' : 'Apply (আবেদন করুন)'}
            </button>
          </article>
        ))}
        {classes.length === 0 && <p className="muted">No classes yet — an admin will create them.</p>}
      </div>

      <h2 style={{ marginTop: 28 }}>My applications</h2>
      {apps.length === 0 && <p className="muted">You have no applications yet.</p>}
      {apps.map(a => {
        const st = eff(a, now)
        const sec = Math.floor((new Date(a.expiresAt).getTime() - now) / 1000)
        return (
          <div key={a.holdId} className="hold">
            <div>
              <strong>{a.className}</strong> {a.grade} · {a.shift} <span className={`badge ${st.toLowerCase()}`}>{st}</span>
              <div className="muted">Fee {money(a.feeAmount)} · Expires {new Date(a.expiresAt).toLocaleString()}{st === 'HELD' && sec > 0 ? ` · ${Math.floor(sec / 60)}m ${String(sec % 60).padStart(2, '0')}s left` : ''}{a.transactionRef ? ` · Ref ${a.transactionRef}` : ''}</div>
            </div>
            <div className="row">
              {st === 'HELD' && <button disabled={busy || sec <= 0} onClick={() => setPayFor(a.holdId)}>Pay admission fee</button>}
              {['HELD', 'CONFIRMED'].includes(st) && <button disabled={busy} onClick={() => cancel(a.holdId)}>Cancel</button>}
            </div>
          </div>
        )
      })}

      {payFor && (
        <div className="card" style={{ marginTop: 20 }}>
          <h3>Pay admission fee</h3>
          <p className="muted">Payment gateway is simulated — record a transaction reference.</p>
          <div className="row">
            <select value={pay.method} onChange={e => setPay({ ...pay, method: e.target.value })}>
              <option>BKASH</option><option>CARD</option><option>BANK</option><option>MOCK</option>
            </select>
            <input placeholder="Transaction reference" value={pay.transactionRef} onChange={e => setPay({ ...pay, transactionRef: e.target.value })} />
            <button disabled={busy} onClick={doPay}>Confirm payment</button>
            <button type="button" onClick={() => setPayFor(null)}>Close</button>
          </div>
        </div>
      )}
    </div>
  )
}
