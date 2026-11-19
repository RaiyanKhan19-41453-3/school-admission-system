import { useEffect, useState } from 'react'
import {
  Modal, TextInput, Button, Group, Stack, Text, Paper, Badge, Select,
  Alert, FileInput, ActionIcon, Loader, Divider,
} from '@mantine/core'
import { IconUpload, IconDownload, IconTrash, IconAlertCircle, IconCheck } from '@tabler/icons-react'
import { apiFetch, friendlyMessage, API_BASE, getToken } from '../api.js'

const DOC_TYPES = ['BirthCertificate', 'Photo', 'Transcript', 'GuardianNID', 'Other']

export default function ApplicationFormModal({ holdId, opened, onClose, onSaved }) {
  const [details, setDetails] = useState(null)
  const [form, setForm] = useState({ dateOfBirth: '', guardianName: '', guardianPhone: '', address: '', previousSchool: '' })
  const [docType, setDocType] = useState('BirthCertificate')
  const [file, setFile] = useState(null)
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')
  const [notice, setNotice] = useState('')

  const load = async () => {
    try { const d = await apiFetch(`/api/applications/${holdId}/details`); setDetails(d)
      setForm({ dateOfBirth: d.dateOfBirth ? d.dateOfBirth.slice(0, 10) : '',
        guardianName: d.guardianName || '', guardianPhone: d.guardianPhone || '',
        address: d.address || '', previousSchool: d.previousSchool || '' }) }
    catch (e) { setErr(friendlyMessage(e)) }
  }
  useEffect(() => { if (opened) { setErr(''); setNotice(''); load() } }, [opened, holdId])

  async function submitDetails() {
    setBusy(true); setErr('')
    try {
      const r = await apiFetch(`/api/applications/${holdId}/details`, { method: 'PUT', body: {
        dateOfBirth: form.dateOfBirth ? new Date(form.dateOfBirth + 'T00:00:00Z').toISOString() : new Date().toISOString(),
        guardianName: form.guardianName, guardianPhone: form.guardianPhone,
        address: form.address, previousSchool: form.previousSchool } })
      setDetails(r); setNotice('Details saved.'); onSaved?.()
    } catch (e) { setErr(friendlyMessage(e)) } finally { setBusy(false) }
  }

  async function upload() {
    if (!file) { setErr('Choose a file first.'); return }
    setBusy(true); setErr('')
    try {
      const fd = new FormData()
      fd.append('documentType', docType)
      fd.append('file', file)
      const res = await fetch(`${API_BASE}/api/applications/${holdId}/documents`, {
        method: 'POST', headers: { 'Authorization': 'Bearer ' + getToken() }, body: fd })
      if (!res.ok) { const b = await res.json().catch(() => ({})); throw new Error(b.message || 'Upload failed') }
      setFile(null); setNotice('Document uploaded.'); await load(); onSaved?.()
    } catch (e) { setErr(friendlyMessage(e)) } finally { setBusy(false) }
  }

  async function remove(docId) {
    setBusy(true)
    try { await apiFetch(`/api/applications/documents/${docId}`, { method: 'DELETE' }); setNotice('Document removed.'); await load() }
    catch (e) { setErr(friendlyMessage(e)) } finally { setBusy(false) }
  }

  function downloadUrl(docId) {
    return `${API_BASE}/api/applications/documents/${docId}/download`
  }

  return (
    <Modal opened={opened} onClose={onClose} title="Admission application" size="lg" centered>
      <Stack gap="md">
        {err && <Alert color="red" icon={<IconAlertCircle size={16} />}>{err}</Alert>}
        {notice && <Alert color="green" icon={<IconCheck size={16} />}>{notice}</Alert>}
        {!details ? <Loader /> : <>
          <Group>
            <Badge color="blue">{details.className}</Badge>
            <Badge color={{ HELD: 'yellow', CONFIRMED: 'green', EXPIRED: 'red', CANCELLED: 'gray' }[details.status]}>{details.status}</Badge>
            {details.detailsSubmitted && <Badge color="teal" variant="light">Details submitted</Badge>}
          </Group>

          <Divider label="Student details" labelPosition="left" />
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <TextInput type="date" label="Date of birth" value={form.dateOfBirth}
              onChange={e => setForm({ ...form, dateOfBirth: e.target.value })} />
            <TextInput label="Guardian name" value={form.guardianName} onChange={e => setForm({ ...form, guardianName: e.target.value })} />
            <TextInput label="Guardian phone" value={form.guardianPhone} onChange={e => setForm({ ...form, guardianPhone: e.target.value })} />
            <TextInput label="Previous school" value={form.previousSchool} onChange={e => setForm({ ...form, previousSchool: e.target.value })} />
            <TextInput label="Address" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} style={{ gridColumn: '1 / -1' }} />
          </div>
          <Button loading={busy} onClick={submitDetails}>Save details</Button>

          <Divider label="Documents" labelPosition="left" />
          {details.documents.length === 0 && <Text size="sm" c="dimmed">No documents uploaded yet.</Text>}
          {details.documents.map(d => (
            <Paper key={d.id} p="xs" withBorder>
              <Group justify="space-between">
                <Group><Text size="sm" fw={600}>{d.documentType}</Text><Text size="xs" c="dimmed">{d.fileName} · {(d.fileSize / 1024).toFixed(1)} KB</Text></Group>
                <Group>
                  <ActionIcon variant="light" component="a" href={downloadUrl(d.id)} target="_blank" download>
                    <IconDownload size={16} />
                  </ActionIcon>
                  <ActionIcon variant="light" color="red" loading={busy} onClick={() => remove(d.id)}>
                    <IconTrash size={16} />
                  </ActionIcon>
                </Group>
              </Group>
            </Paper>
          ))}
          <Group align="flex-end">
            <Select label="Document type" data={DOC_TYPES} value={docType} onChange={setDocType} allowDeselect={false} />
            <FileInput label="Choose file" placeholder="PDF, image, etc." value={file} onChange={setFile} accept="application/pdf,image/*,.doc,.docx" />
            <Button leftSection={<IconUpload size={15} />} loading={busy} onClick={upload}>Upload</Button>
          </Group>
        </>}
      </Stack>
    </Modal>
  )
}
