import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { Paper, Title, Text, TextInput, PasswordInput, Button, Alert, Stack, Anchor } from '@mantine/core'
import { IconLogin, IconUserPlus, IconAlertCircle } from '@tabler/icons-react'
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
    <div style={{ maxWidth: 440, margin: '40px auto' }}>
      <Paper shadow="md" radius="lg" p="xl" withBorder>
        <Stack gap="md">
          <div>
            <Title order={2}>{isLogin ? 'Welcome back' : 'Create your account'}</Title>
            <Text c="dimmed" size="sm" mt={4}>
              {isLogin ? 'Sign in to apply for admission.' : 'Register with your birth certificate number.'}
            </Text>
          </div>
          {err && <Alert color="red" icon={<IconAlertCircle size={16} />}>{err}</Alert>}
          <form onSubmit={submit}>
            <Stack gap="sm">
              <TextInput
                label="Birth certificate number" placeholder="17-digit NID" required
                value={form.nid} maxLength={17}
                onChange={e => setForm({ ...form, nid: e.target.value.replace(/\D/g, '').slice(0, 17) })} />
              {!isLogin && <TextInput label="Full name" placeholder="Your full name" required
                value={form.fullName} onChange={e => setForm({ ...form, fullName: e.target.value })} />}
              <PasswordInput label="Password" placeholder="At least 6 characters" required
                value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} />
              <Button type="submit" fullWidth loading={busy}
                leftSection={isLogin ? <IconLogin size={16} /> : <IconUserPlus size={16} />}>
                {isLogin ? 'Sign in' : 'Create account'}
              </Button>
            </Stack>
          </form>
          <Text size="sm" c="dimmed" ta="center">
            {isLogin ? <>No account? <Anchor component={Link} to="/register">Register</Anchor></>
                     : <>Already registered? <Anchor component={Link} to="/login">Sign in</Anchor></>}
          </Text>
        </Stack>
      </Paper>
    </div>
  )
}
