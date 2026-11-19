import { useEffect, useState } from 'react'
import {
  Title, Text, Tabs, SimpleGrid, Paper, Group, Badge, Button, Table,
  TextInput, NumberInput, Alert, Stack, Modal, Divider,
} from '@mantine/core'
import {
  IconChartBar, IconCalendar, IconBook2, IconList, IconUsers, IconCheck,
  IconAlertCircle, IconPlus,
} from '@tabler/icons-react'
import { apiFetch, friendlyMessage } from '../api.js'

const money = n => 'TK ' + Number(n).toLocaleString()

function Stat({ label, value, icon, color = 'blue' }) {
  return (
    <Paper p="md" withBorder radius="md">
      <Group justify="space-between">
        <div><Text size="xs" c="dimmed" tt="uppercase" fw={700}>{label}</Text><Text size="xl" fw={700} mt={4}>{value}</Text></div>
        <ThemeIcon color={color} variant="light" size="lg">{icon}</ThemeIcon>
      </Group>
    </Paper>
  )
}

export default function AdminPage() {
  const [tab, setTab] = useState('overview')
  const [data, setData] = useState({})
  const [form, setForm] = useState({})
  const [banner, setBanner] = useState('')
  const [notice, setNotice] = useState('')
  const [detailFor, setDetailFor] = useState(null)
  const [detail, setDetail] = useState(null)

  const endpoint = {
    overview: '/api/admin/dashboard', years: '/api/admin/years',
    classes: '/api/admin/classes', applications: '/api/admin/applications',
    students: '/api/admin/students',
  }[tab]

  const load = async () => {
    try { setData({ [tab]: await apiFetch(endpoint) }) }
    catch (e) { setBanner(friendlyMessage(e)) }
  }
  useEffect(() => { load() }, [tab])

  async function action(path, method = 'POST', body) {
    setBanner(''); setNotice('')
    try { const r = await apiFetch(path, { method, body }); setNotice(r?.message || 'Saved.'); setForm({}); await load() }
    catch (e) { setBanner(friendlyMessage(e)) }
  }

  const dash = data.overview
  const years = data.years || []
  const classes = data.classes || []
  const apps = data.applications
  const students = data.students

  return (
    <Stack gap="lg">
      <Title order={1}>Admin dashboard</Title>
      {banner && <Alert color="red" icon={<IconAlertCircle size={16} />}>{banner}</Alert>}
      {notice && <Alert color="green" icon={<IconCheck size={16} />}>{notice}</Alert>}

      <Tabs value={tab} onChange={setTab} variant="pills" radius="md">
        <Tabs.List>
          <Tabs.Tab value="overview" leftSection={<IconChartBar size={16} />}>Overview</Tabs.Tab>
          <Tabs.Tab value="years" leftSection={<IconCalendar size={16} />}>Years</Tabs.Tab>
          <Tabs.Tab value="classes" leftSection={<IconBook2 size={16} />}>Classes</Tabs.Tab>
          <Tabs.Tab value="applications" leftSection={<IconList size={16} />}>Applications</Tabs.Tab>
          <Tabs.Tab value="students" leftSection={<IconUsers size={16} />}>Students</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="overview" pt="md">
          {dash ? (
            <SimpleGrid cols={{ base: 2, sm: 3, lg: 4 }} spacing="md">
              <Stat label="Active year" value={dash.activeYear} icon={<IconCalendar size={20} />} />
              <Stat label="Classes" value={dash.activeClasses} icon={<IconBook2 size={20} />} />
              <Stat label="Seats filled" value={`${dash.seatsFilled}/${dash.totalSeats}`} icon={<IconUsers size={20} />} color="cyan" />
              <Stat label="Confirmed" value={dash.confirmedApplications} icon={<IconCheck size={20} />} color="green" />
              <Stat label="Held" value={dash.heldApplications} icon={<IconList size={20} />} color="yellow" />
              <Stat label="Expired" value={dash.expiredApplications} icon={<IconAlertCircle size={20} />} color="red" />
              <Stat label="Revenue" value={money(dash.revenueCollected)} icon={<IconChartBar size={20} />} color="grape" />
              <Stat label="Students" value={dash.totalStudents} icon={<IconUsers size={20} />} color="teal" />
            </SimpleGrid>
          ) : <Text c="dimmed">Loading…</Text>}
        </Tabs.Panel>

        <Tabs.Panel value="years" pt="md">
          <Paper p="md" withBorder radius="md" mb="md">
            <Title order={4} mb="sm">Create academic year</Title>
            <Group align="flex-end">
              <TextInput label="Name" placeholder="2027-2028" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} />
              <TextInput label="Start date" type="date" value={form.startDate || ''} onChange={e => setForm({ ...form, startDate: e.target.value })} />
              <TextInput label="End date" type="date" value={form.endDate || ''} onChange={e => setForm({ ...form, endDate: e.target.value })} />
              <Button leftSection={<IconPlus size={15} />} onClick={() => action('/api/admin/years', 'POST', {
                name: form.name, startDate: form.startDate, endDate: form.endDate })}>Create</Button>
            </Group>
          </Paper>
          <Stack gap="xs">
            {years.map(y => (
              <Paper key={y.id} p="sm" withBorder radius="md">
                <Group justify="space-between">
                  <Group><Text fw={600}>{y.name}</Text>{y.isActive && <Badge color="green">ACTIVE</Badge>}<Text size="sm" c="dimmed">{y.classCount} classes</Text></Group>
                  {!y.isActive && <Button size="xs" variant="light" onClick={() => action(`/api/admin/years/${y.id}/activate`, 'PUT')}>Activate</Button>}
                </Group>
              </Paper>
            ))}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="classes" pt="md">
          <Paper p="md" withBorder radius="md" mb="md">
            <Title order={4} mb="sm">Create class / shift</Title>
            <Group align="flex-end" wrap="wrap">
              <TextInput label="Name" placeholder="Class 6" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} />
              <TextInput label="Grade" placeholder="Class 6" value={form.grade || ''} onChange={e => setForm({ ...form, grade: e.target.value })} />
              <TextInput label="Shift" placeholder="Morning" value={form.shift || ''} onChange={e => setForm({ ...form, shift: e.target.value })} />
              <NumberInput label="Seats" min={1} value={form.totalSeats || 0} onChange={v => setForm({ ...form, totalSeats: v })} />
              <NumberInput label="Fee" min={0} value={form.feeAmount || 0} onChange={v => setForm({ ...form, feeAmount: v })} />
              <Button leftSection={<IconPlus size={15} />} onClick={() => action('/api/admin/classes', 'POST', {
                name: form.name, grade: form.grade, shift: form.shift,
                totalSeats: Number(form.totalSeats), feeAmount: Number(form.feeAmount) })}>Create</Button>
            </Group>
          </Paper>
          <Paper withBorder radius="md" p="md">
            <Table striped highlightOnHover>
              <Table.Thead><Table.Tr><Table.Th>Class</Table.Th><Table.Th>Grade</Table.Th><Table.Th>Shift</Table.Th><Table.Th>Seats</Table.Th><Table.Th>Fee</Table.Th><Table.Th>Available</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {classes.map(c => (
                  <Table.Tr key={c.id}>
                    <Table.Td fw={600}>{c.name}</Table.Td><Table.Td>{c.grade}</Table.Td><Table.Td>{c.shift}</Table.Td>
                    <Table.Td>{c.totalSeats}</Table.Td><Table.Td>{money(c.feeAmount)}</Table.Td><Table.Td>{c.availableSeats}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Paper>
        </Tabs.Panel>

        <Tabs.Panel value="applications" pt="md">
          <Paper withBorder radius="md" p="md">
            <Title order={4} mb="sm">All applications ({apps?.total ?? 0})</Title>
            <Table striped highlightOnHover>
              <Table.Thead><Table.Tr><Table.Th>Class</Table.Th><Table.Th>Student</Table.Th><Table.Th>NID</Table.Th><Table.Th>Status</Table.Th><Table.Th>Ref</Table.Th><Table.Th></Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {(apps?.items || []).map(a => (
                  <Table.Tr key={a.holdId}>
                    <Table.Td>{a.className}</Table.Td><Table.Td>{a.studentName}</Table.Td><Table.Td>{a.studentNid}</Table.Td>
                    <Table.Td><Badge color={{ HELD: 'yellow', CONFIRMED: 'green', EXPIRED: 'red', CANCELLED: 'gray' }[a.status]}>{a.status}</Badge></Table.Td>
                    <Table.Td>{a.transactionRef || '-'}</Table.Td>
                    <Table.Td><Button size="xs" variant="light" onClick={async () => {
                      setDetailFor(a.holdId)
                      try { setDetail(await apiFetch(`/api/admin/applications/${a.holdId}/details`)) }
                      catch (e) { setBanner(friendlyMessage(e)) }
                    }}>View</Button></Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Paper>
        </Tabs.Panel>

        <Tabs.Panel value="students" pt="md">
          <Paper withBorder radius="md" p="md">
            <Title order={4} mb="sm">Students ({students?.total ?? 0})</Title>
            <Table striped highlightOnHover>
              <Table.Thead><Table.Tr><Table.Th>Name</Table.Th><Table.Th>NID</Table.Th><Table.Th>Role</Table.Th><Table.Th>Apps</Table.Th></Table.Tr></Table.Thead>
              <Table.Tbody>
                {(students?.items || []).map(s => (
                  <Table.Tr key={s.id}>
                    <Table.Td>{s.fullName}</Table.Td><Table.Td>{s.nid}</Table.Td><Table.Td><Badge variant="light">{s.role}</Badge></Table.Td><Table.Td>{s.totalApplications}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Paper>
        </Tabs.Panel>
      </Tabs>

      <Modal opened={!!detailFor} onClose={() => { setDetailFor(null); setDetail(null) }}
        title="Application details" size="lg" centered>
        {!detail ? <Text c="dimmed">Loading…</Text> : <Stack gap="sm">
          <Group><Badge color="blue">{detail.className}</Badge><Badge>{detail.status}</Badge></Group>
          <Text><b>Student:</b> {detail.studentName} ({detail.studentNid})</Text>
          <Divider label="Admission form" labelPosition="left" />
          <Text><b>Date of birth:</b> {detail.dateOfBirth ? new Date(detail.dateOfBirth).toLocaleDateString() : '-'}</Text>
          <Text><b>Guardian:</b> {detail.guardianName || '-'} · {detail.guardianPhone || '-'}</Text>
          <Text><b>Address:</b> {detail.address || '-'}</Text>
          <Text><b>Previous school:</b> {detail.previousSchool || '-'}</Text>
          <Divider label="Documents" labelPosition="left" />
          {detail.documents.length === 0 ? <Text c="dimmed">No documents.</Text> :
            detail.documents.map(d => <Text key={d.id}><b>{d.documentType}:</b> {d.fileName}</Text>)}
        </Stack>}
      </Modal>
    </Stack>
  )
}
