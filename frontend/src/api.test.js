import { describe, it, expect } from 'vitest'
import { friendlyMessage } from './api.js'

describe('friendlyMessage', () => {
  it('maps seat errors to plain language', () => {
    expect(friendlyMessage({ code: 'NO_SEATS', message: 'x' })).toContain('No seats')
  })
  it('falls back to message', () => {
    expect(friendlyMessage({ message: 'Hello' })).toBe('Hello')
  })
})
