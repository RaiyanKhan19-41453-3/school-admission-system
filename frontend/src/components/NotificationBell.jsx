import { useEffect, useState } from 'react'
import {
  ActionIcon, Indicator, Popover, Stack, Text, Group, Badge,
  ScrollArea, Divider, Button, Loader,
} from '@mantine/core'
import { IconBell, IconCheck, IconInfoCircle, IconCash, IconX, IconAlertCircle } from '@tabler/icons-react'
import { apiFetch, friendlyMessage } from '../api.js'

const icons = {
  HOLD_CREATED: <IconInfoCircle size={16} />,
  PAYMENT_CONFIRMED: <IconCash size={16} />,
  CANCELLED: <IconX size={16} />,
  EXPIRED: <IconAlertCircle size={16} />,
  EXPIRY_WARNING: <IconAlertCircle size={16} />,
}
const colors = {
  HOLD_CREATED: 'blue', PAYMENT_CONFIRMED: 'green', CANCELLED: 'gray',
  EXPIRED: 'red', EXPIRY_WARNING: 'yellow',
}

export default function NotificationBell() {
  const [data, setData] = useState({ items: [], unreadCount: 0 })
  const [opened, setOpened] = useState(false)
  const [err, setErr] = useState('')

  const load = async () => {
    try { setData(await apiFetch('/api/notifications')) }
    catch (e) { setErr(friendlyMessage(e)) }
  }
  useEffect(() => { load(); const i = setInterval(load, 10000); return () => clearInterval(i) }, [])

  async function markAll() {
    try { await apiFetch('/api/notifications/read-all', { method: 'PUT' }); await load() }
    catch (e) { setErr(friendlyMessage(e)) }
  }
  async function markOne(id) {
    try { await apiFetch(`/api/notifications/${id}/read`, { method: 'PUT' }); await load() }
    catch (e) { setErr(friendlyMessage(e)) }
  }

  return (
    <Popover opened={opened} onChange={setOpened} position="bottom-end" width={360} withArrow shadow="md">
      <Popover.Target>
        <Indicator color="red" size={16} disabled={data.unreadCount === 0} label={data.unreadCount > 9 ? '9+' : data.unreadCount}>
          <ActionIcon variant="subtle" aria-label="Notifications" onClick={() => setOpened(o => !o)}>
            <IconBell size={20} />
          </ActionIcon>
        </Indicator>
      </Popover.Target>
      <Popover.Dropdown p={0}>
        <Group justify="space-between" p="sm" pb={4}>
          <Text fw={700}>Notifications</Text>
          {data.unreadCount > 0 && <Button size="xs" variant="subtle" onClick={markAll}>Mark all read</Button>}
        </Group>
        <Divider />
        {err && <Text p="sm" c="red" size="sm">{err}</Text>}
        <ScrollArea.Autosize mah={360}>
          {data.items.length === 0 ? (
            <Text p="md" c="dimmed" size="sm" ta="center">No notifications yet.</Text>
          ) : (
            <Stack gap={0}>
              {data.items.map(n => (
                <Group key={n.id} p="sm" align="flex-start" wrap="nowrap"
                  onClick={() => !n.isRead && markOne(n.id)}
                  style={{ cursor: n.isRead ? 'default' : 'pointer',
                    background: n.isRead ? undefined : 'var(--mantine-color-blue-0)' }}>
                  <Badge color={colors[n.type] || 'gray'} variant="light" leftSection={icons[n.type] || <IconInfoCircle size={16} />}>
                    {n.type.replace('_', ' ')}
                  </Badge>
                  <div style={{ flex: 1 }}>
                    <Text size="sm" fw={n.isRead ? 400 : 700}>{n.title}</Text>
                    <Text size="xs" c="dimmed">{n.message}</Text>
                    <Text size="xs" c="dimmed" mt={2}>{new Date(n.createdAt).toLocaleString()}</Text>
                  </div>
                </Group>
              ))}
            </Stack>
          )}
        </ScrollArea.Autosize>
      </Popover.Dropdown>
    </Popover>
  )
}
