import { useEffect, useState } from 'react'
import {
  Title, Text, SimpleGrid, Card, Group, Badge, Button, Progress, Alert,
  Divider, Paper, Select, TextInput, NumberInput, ActionIcon, Stack, ThemeIcon,
} from '@mantine/core'
import {
  IconBook2, IconCalendarEvent, IconCash, IconCheck, IconX, IconClock,
  IconInfoCircle, IconDeviceMobile,
} from '@tabler/icons-react'
import { apiFetch, friendlyMessage, API_BASE, getToken } from '../api.js'
import ApplicationFormModal from '../components/ApplicationFormModal.jsx'

function useNow() {
  const [now, setNow] = useState(Date.now())
  useEffect(() => { const i = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(i) }, [])
  return now
}
const eff = (s, now) => s.status === 'HELD' && new Date(s.expiresAt).getTime() <= now ? 'EXPIRED' : s.status
const money = n => 'TK ' + Number(n).toLocaleString()
const colors = { HELD: 'yellow', CONFIRMED: 'green', EXPIRED: 'red', CANCELLED: 'gray' }

export default function ParentPage() {
  const now = useNow()
  const [classes, setClasses] = useState([])
  const [apps, setApps] = useState([])
  const [banner, setBanner] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [payFor, setPayFor] = useState(null)
  const [pay, setPay] = useState({ method: 'BKASH', transactionRef: '' })
  const [formFor, setFormFor] = useState(null)

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
    <Stack gap="lg">
      <div>
        <Title order={1}>Available classes</Title>
        <Text c="dimmed">Apply to hold a seat, then pay the fee before it expires. Counts refresh automatically.</Text>
      </div>
      {banner && <Alert color="red" icon={<IconAlertCircle size={16} />}>{banner}</Alert>}
      {notice && <Alert color="green" icon={<IconCheck size={16} />}>{notice}</Alert>}

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }} spacing="md">
        {classes.map(c => {
          const filled = c.heldSeats + c.confirmedSeats
          const pct = c.totalSeats ? (filled / c.totalSeats) * 100 : 0
          return (
            <Card key={c.id} shadow="sm" radius="md" withBorder padding="lg">
              <Group justify="space-between" mb="xs">
                <ThemeIcon color="blue" variant="light"><IconBook2 size={18} /></ThemeIcon>
                <Badge color={c.isActive ? 'green' : 'gray'} variant="light">{c.isActive ? 'Open' : 'Closed'}</Badge>
              </Group>
              <Text fw={700} size="lg">{c.name}</Text>
              <Text size="sm" c="dimmed">{c.grade} · {c.shift || 'General'}</Text>
              <Group mt="md" justify="space-between">
                <Text size="sm">Fee <b>{money(c.feeAmount)}</b></Text>
                <Text size="sm" c="dimmed">Available <b>{c.availableSeats}</b> / {c.totalSeats}</Text>
              </Group>
              <Progress value={pct} color="blue" size="sm" mt="sm" />
              <Button fullWidth mt="md" loading={busy}
                disabled={!c.isActive || c.availableSeats <= 0}
                onClick={() => apply(c.id)}>
                {c.availableSeats <= 0 ? 'Class full' : 'Apply (আবেদন করুন)'}
              </Button>
            </Card>
          )
        })}
      </SimpleGrid>
      {classes.length === 0 && <Paper p="xl" withBorder><Text c="dimmed" ta="center">No classes yet — an admin will create them for the active year.</Text></Paper>}

      <Divider label="My applications" labelPosition="left" />
      {apps.length === 0 ? (
        <Paper p="lg" withBorder><Text c="dimmed">You have no applications yet.</Text></Paper>
      ) : (
        <Stack gap="sm">
          {apps.map(a => {
            const st = eff(a, now)
            const sec = Math.floor((new Date(a.expiresAt).getTime() - now) / 1000)
            return (
              <Paper key={a.holdId} p="md" withBorder radius="md">
                <Group justify="space-between" wrap="wrap">
                  <div>
                    <Group gap="xs"><Text fw={600}>{a.className}</Text><Badge color={colors[st]}>{st}</Badge></Group>
                    <Text size="sm" c="dimmed" mt={4}>
                      {a.grade} · {a.shift} · Fee {money(a.feeAmount)} · Expires {new Date(a.expiresAt).toLocaleString()}
                      {st === 'HELD' && sec > 0 ? ` · ${Math.floor(sec / 60)}m ${String(sec % 60).padStart(2, '0')}s left` : ''}
                      {a.transactionRef ? ` · Ref ${a.transactionRef}` : ''}
                    </Text>
                  </div>
                  <Group>
                    <Button size="sm" variant="light" leftSection={<IconInfoCircle size={15} />}
                      onClick={() => setFormFor(a.holdId)}>Application form</Button>
                    {st === 'HELD' && <Button size="sm" leftSection={<IconCash size={15} />}
                      disabled={busy || sec <= 0} onClick={() => setPayFor(a.holdId)}>Pay fee</Button>}
                    {['HELD', 'CONFIRMED'].includes(st) && <Button size="sm" variant="light" color="red"
                      leftSection={<IconX size={15} />} disabled={busy} onClick={() => cancel(a.holdId)}>Cancel</Button>}
                    {st === 'CONFIRMED' && <Button size="sm" variant="light" color="green" component="a"
                      href={`${API_BASE}/api/applications/${a.holdId}/receipt?token=${encodeURIComponent(getToken() || '')}`}
                      target="_blank" rel="noopener">Download receipt</Button>}
                  </Group>
                </Group>
              </Paper>
            )
          })}
        </Stack>
      )}

      {payFor && (
        <Paper p="lg" withBorder radius="md" mt="md">
          <Title order={4} mb="xs">Pay admission fee</Title>
          <Text size="sm" c="dimmed" mb="md">Payment gateway is simulated — record a transaction reference.</Text>
          <Group align="flex-end">
            <Select label="Method" data={['BKASH', 'CARD', 'BANK', 'MOCK']} value={pay.method}
              onChange={v => setPay({ ...pay, method: v })} allowDeselect={false} />
            <TextInput label="Transaction reference" placeholder="TXN-12345" value={pay.transactionRef}
              onChange={e => setPay({ ...pay, transactionRef: e.target.value })} />
            <Button loading={busy} onClick={doPay}>Confirm payment</Button>
            <Button variant="subtle" onClick={() => setPayFor(null)}>Close</Button>
          </Group>
        </Paper>
      )}
      {formFor && <ApplicationFormModal holdId={formFor} opened={!!formFor}
        onClose={() => setFormFor(null)} onSaved={load} />}
    </Stack>
  )
}
