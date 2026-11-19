import { useEffect, useState } from 'react'
import { apiFetch, friendlyMessage } from '../api.js'

const money = n => 'TK ' + Number(n).toLocaleString()

export default function AdminPage() {
  const [tab, setTab] = useState('overview')
  const [dash, setDash] = useState(null)
  const [years, setYears] = useState([])
  const [classes, setClasses] = useState([])
  const [apps, setApps] = useState(null)
  const [students, setStudents] = useState(null)
  const [form, setForm] = useState({})
  const [banner, setBanner] = useState('')
  const [notice, setNotice] = useState('')

  const load = async () => {
    try {
      if (tab === 'overview') setDash(await apiFetch('/api/admin/dashboard'))
      if (tab === 'years') setYears(await apiFetch('/api/admin/years'))
      if (tab === 'classes') setClasses(await apiFetch('/api/admin/classes'))
      if (tab === 'applications') setApps(await apiFetch('/api/admin/applications'))
      if (tab === 'students') setStudents(await apiFetch('/api/admin/students'))
    } catch (e) { setBanner(friendlyMessage(e)) }
  }
  useEffect(() => { load() }, [tab])

  async function action(path, method = 'POST', body) {
    setBanner(''); setNotice('')
    try { const r = await apiFetch(path, { method, body }); setNotice(r?.message || 'Saved.'); setForm({}); await load() }
    catch (e) { setBanner(friendlyMessage(e)) }
  }

  return (
    <div>
      <h1>Admin dashboard</h1>
      <div className="tabs">
        {['overview', 'years', 'classes', 'applications', 'students'].map(t =>
          <button key={t} type="button" className={tab === t ? 'active' : ''} onClick={() => setTab(t)}>{t}</button>)}
      </div>
      {banner && <div className="alert error">{banner}</div>}
      {notice && <div className="alert ok">{notice}</div>}

      {tab === 'overview' && dash && (
        <div className="stats">
          <div className="stat"><small>Active year</small><strong>{dash.activeYear}</strong></div>
          <div className="stat"><small>Classes</small><strong>{dash.activeClasses}</strong></div>
          <div className="stat"><small>Seats filled</small><strong>{dash.seatsFilled}/{dash.totalSeats}</strong></div>
          <div className="stat"><small>Confirmed</small><strong>{dash.confirmedApplications}</strong></div>
          <div className="stat"><small>Held</small><strong>{dash.heldApplications}</strong></div>
          <div className="stat"><small>Revenue</small><strong>{money(dash.revenueCollected)}</strong></div>
        </div>
      )}

      {tab === 'years' && (
        <div className="card">
          <h3>Create academic year</h3>
          <div className="row">
            <input placeholder="2027-2028" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} />
            <input type="date" value={form.startDate || ''} onChange={e => setForm({ ...form, startDate: e.target.value })} />
            <input type="date" value={form.endDate || ''} onChange={e => setForm({ ...form, endDate: e.target.value })} />
            <button onClick={() => action('/api/admin/years', 'POST', {
              name: form.name, startDate: form.startDate, endDate: form.endDate })}>Create</button>
          </div>
          {years.map(y => (
            <div key={y.id} className="hold">
              <span><strong>{y.name}</strong> {y.isActive && <span className="badge confirmed">ACTIVE</span>} · {y.classCount} classes</span>
              {!y.isActive && <button onClick={() => action(`/api/admin/years/${y.id}/activate`, 'PUT')}>Activate</button>}
            </div>
          ))}
        </div>
      )}

      {tab === 'classes' && (
        <div className="card">
          <h3>Create class / shift</h3>
          <div className="row">
            <input placeholder="Name (e.g. Class 6)" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} />
            <input placeholder="Grade" value={form.grade || ''} onChange={e => setForm({ ...form, grade: e.target.value })} />
            <input placeholder="Shift" value={form.shift || ''} onChange={e => setForm({ ...form, shift: e.target.value })} />
            <input placeholder="Seats" type="number" value={form.totalSeats || ''} onChange={e => setForm({ ...form, totalSeats: e.target.value })} />
            <input placeholder="Fee" type="number" value={form.feeAmount || ''} onChange={e => setForm({ ...form, feeAmount: e.target.value })} />
            <button onClick={() => action('/api/admin/classes', 'POST', {
              name: form.name, grade: form.grade, shift: form.shift,
              totalSeats: Number(form.totalSeats), feeAmount: Number(form.feeAmount) })}>Create</button>
          </div>
          <table className="data-table">
            <thead><tr><th>Class</th><th>Grade</th><th>Shift</th><th>Seats</th><th>Fee</th><th>Available</th></tr></thead>
            <tbody>
              {classes.map(c => (
                <tr key={c.id}><td>{c.name}</td><td>{c.grade}</td><td>{c.shift}</td><td>{c.totalSeats}</td><td>{money(c.feeAmount)}</td><td>{c.availableSeats}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'applications' && apps && (
        <div className="card">
          <h3>All applications ({apps.total})</h3>
          <table className="data-table">
            <thead><tr><th>Class</th><th>Student</th><th>NID</th><th>Status</th><th>Ref</th></tr></thead>
            <tbody>
              {apps.items.map(a => (
                <tr key={a.holdId}><td>{a.className}</td><td>{a.studentName}</td><td>{a.studentNid}</td>
                  <td><span className={`badge ${a.status.toLowerCase()}`}>{a.status}</span></td><td>{a.transactionRef || '-'}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'students' && students && (
        <div className="card">
          <h3>Students ({students.total})</h3>
          <table className="data-table">
            <thead><tr><th>Name</th><th>NID</th><th>Role</th><th>Apps</th></tr></thead>
            <tbody>
              {students.items.map(s => (
                <tr key={s.id}><td>{s.fullName}</td><td>{s.nid}</td><td>{s.role}</td><td>{s.totalApplications}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
