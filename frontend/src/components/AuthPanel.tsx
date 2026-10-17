import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { AuthUser } from '../types'
import { mutateApi } from '../api'

type AuthPanelProps = {
  user: AuthUser | null
  disabled: boolean
  onUserChange: (user: AuthUser | null) => void
}

export function AuthPanel({ user, disabled, onUserChange }: AuthPanelProps) {
  const [loading, setLoading] = useState(true)
  const [attempt, setAttempt] = useState(0)
  const [registering, setRegistering] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const inFlight = useRef(false)

  useEffect(() => {
    const controller = new AbortController()
    async function loadUser() {
      try {
        const response = await fetch('/api/auth/me', { signal: controller.signal, cache: 'no-store' })
        if (!response.ok) throw new Error('Session lookup failed')
        const data = await response.json()
        if (!controller.signal.aborted) {
          onUserChange(data.user)
          setLoading(false)
          setMessage('')
        }
      } catch {
        if (!controller.signal.aborted) {
          setLoading(false)
          setMessage('Unable to check your session. Please try again.')
        }
      }
    }
    void loadUser()
    return () => controller.abort()
  }, [attempt, onUserChange])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (inFlight.current || disabled) return
    inFlight.current = true
    setBusy(true)
    setMessage('')
    try {
      const response = await mutateApi(`/api/auth/${registering ? 'register' : 'login'}`, {
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: email.trim(), password }),
      })
      const data = await response.json()
      if (!response.ok) {
        setMessage(data.message ?? (Object.values(data.errors ?? {}).flat().join(' ') || 'Check your sign-in details.'))
        return
      }
      setPassword('')
      if (registering) {
        setRegistering(false)
        setMessage('Account created. You can now sign in.')
      } else {
        onUserChange(data.user)
      }
    } catch {
      setMessage(registering
        ? 'Unable to confirm account creation. Try signing in with the email address you provided.'
        : 'Unable to confirm sign-in. Check your session again.')
    } finally { inFlight.current = false; setBusy(false) }
  }

  async function logout() {
    if (inFlight.current || disabled) return
    inFlight.current = true
    setBusy(true)
    try {
      const response = await mutateApi('/api/auth/logout')
      if (!response.ok && response.status !== 401) throw new Error('Logout failed')
      onUserChange(null)
      setPassword('')
      setMessage('')
    } catch { setMessage('Unable to confirm sign-out. Please try again.') }
    finally { inFlight.current = false; setBusy(false) }
  }

  return (
    <section className="provider-section auth-panel" aria-labelledby="auth-heading">
      <h2 id="auth-heading">{user ? 'Your account' : registering ? 'Create account' : 'Sign in'}</h2>
      {loading && <p role="status">Checking your session…</p>}
      {message && <p className="booking-message" role="status">{message}</p>}
      {!loading && user && (
        <div className="account-actions">
          <p>Signed in as {user.email}</p>
          <button className="retry-button" type="button" disabled={busy || disabled} onClick={() => void logout()}>
            {busy ? 'Signing out…' : 'Sign out'}
          </button>
        </div>
      )}
      {!loading && !user && (
        <>
          <p className="auth-description">Sign in to book and manage your appointments.</p>
          <form onSubmit={(event) => void submit(event)}>
            <fieldset className="customer-fields" disabled={busy || disabled}>
              <label htmlFor="account-email">Account email</label>
              <input id="account-email" type="email" autoComplete="username" required maxLength={254}
                value={email} onChange={(event) => setEmail(event.target.value)} />
              <label htmlFor="account-password">Password</label>
              <input id="account-password" type="password" autoComplete={registering ? 'new-password' : 'current-password'}
                required minLength={10} maxLength={128} value={password} onChange={(event) => setPassword(event.target.value)} />
              {registering && <p>At least 10 characters, including an uppercase letter, a lowercase letter, a number and a special character.</p>}
              <button className="retry-button" type="submit">{busy ? 'Please wait…' : registering ? 'Create account' : 'Sign in'}</button>
              <button className="text-button" type="button" onClick={() => { setRegistering(!registering); setMessage('') }}>
                {registering ? 'I already have an account' : 'Need an account? Register'}
              </button>
            </fieldset>
          </form>
        </>
      )}
      {message && <button className="text-button" type="button" disabled={busy || disabled} onClick={() => {
        setLoading(true); setAttempt((value) => value + 1)
      }}>Check session again</button>}
    </section>
  )
}
