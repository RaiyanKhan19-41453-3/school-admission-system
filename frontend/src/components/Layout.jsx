import { AppShell, Burger, Group, NavLink, Text, Badge, Button, Box, useMantineColorScheme, ActionIcon } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconSchool, IconLogout, IconMoon, IconSun } from '@tabler/icons-react'
import { Link, NavLink as RouterNavLink, useNavigate } from 'react-router-dom'
import { getToken, setToken, apiFetch } from '../api.js'

export default function Layout({ profile, setProfile, children }) {
  const [opened, { toggle }] = useDisclosure()
  const navigate = useNavigate()
  const { colorScheme, toggleColorScheme } = useMantineColorScheme()
  const token = getToken()

  async function logout() {
    setToken(null); setProfile(null); navigate('/login')
  }

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{ width: 230, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group>
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />
            <IconSchool size={26} color="var(--mantine-color-blue-6)" />
            <Text fw={700} size="lg">School Admission</Text>
          </Group>
          <Group>
            {token && <Badge color={profile?.role === 'Admin' ? 'grape' : 'blue'} variant="light">{profile?.role}</Badge>}
            {token && <Text size="sm" c="dimmed" visibleFrom="sm">{profile?.fullName}</Text>}
            <ActionIcon variant="subtle" onClick={toggleColorScheme} aria-label="Toggle color scheme">
              {colorScheme === 'dark' ? <IconSun size={18} /> : <IconMoon size={18} />}
            </ActionIcon>
            {token && <Button size="xs" variant="light" leftSection={<IconLogout size={14} />} onClick={logout}>Sign out</Button>}
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="md">
        <Text size="xs" c="dimmed" tt="uppercase" fw={700} mb="sm">Menu</Text>
        {token && profile?.role === 'Parent' && (
          <NavLink component={RouterNavLink} to="/parent" label="Classes & Applications" activeColor="blue" />
        )}
        {token && profile?.role === 'Admin' && (
          <NavLink component={RouterNavLink} to="/admin" label="Admin Dashboard" activeColor="blue" />
        )}
        {!token && <>
          <NavLink component={RouterNavLink} to="/login" label="Sign in" />
          <NavLink component={RouterNavLink} to="/register" label="Register" />
        </>}
      </AppShell.Navbar>

      <AppShell.Main>{children}</AppShell.Main>
    </AppShell>
  )
}
