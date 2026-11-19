import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiFetch, friendlyMessage, setToken } from '../api.js'

export default function AuthPage({ mode }) {
  const navigate = useNavigate()
  const isLogin = mode === 'login'
  const [form, setForm] = useState({ nid: '', password: '', fullName: '' })
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e) {
    e.preventDefault(); setBusy(true); setErr('')
    try {
      const body = isLogin
        ? { nid: form.nid, password: form.password }
        : { nid: form.nid, password: form.password, fullName: form.fullName }
      const r = await apiFetch(isLogin ? '/api/auth/login' : '/api/auth/register',
        { method: 'POST', body, token: null })
      setToken(r.token)
      navigate(r.role === 'Admin' ? '/admin' : '/parent')
    } catch (e) { setErr(friendlyMessage(e)) } finally { setBusy(false) }
  }

  return (
    <div className="auth-wrap">
      <form className="card auth-card" onSubmit={submit}>
        <h1>{isLogin ? 'Welcome back' : 'Create your account'}</h1>
        <p className="muted">{isLogin ? 'Sign in to apply for admission.' : 'Register with your birth certificate number.'}</p>
        {err && <div className="alert error">{err}</div>}
        <label className="label">Birth certificate number (17 digits)</label>
        <input placeholder="17-digit NID" value={form.nid}
          onChange={e => setForm({ ...form, nid: e.target.value.replace(/\D/g, '').slice(0, 17) })} />
        {!isLogin && <>
          <label className="label">Full name</label>
          <input placeholder="Your full name" value={form.fullName}
            onChange={e => setForm({ ...form, fullName: e.target.value })} />
        </>}
        <label className="label">Password</label>
        <input type="password" placeholder="At least 6 characters" value={form.password}
          onChange={e => setForm({ ...form, password: e.target.value })} />
        <button disabled={busy} type="submit">{isLogin ? 'Sign in' : 'Create account'}</button>
        <p className="muted">
          {isLogin ? <>No account? <Link to="/register">Register</Link></>
                   : <>Already registered? <Link to="/login">Sign in</Link></>}
        </p>
      </form>
    </div>
  )
}
