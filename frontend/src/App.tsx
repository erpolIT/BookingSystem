import { useCallback, useEffect, useRef, useState } from 'react'
import './App.css'
import { ServicePicker } from './components/ServicePicker'
import type { AuthUser, AvailableSlot, Booking, BookingPayload, Service } from './types'
import { SlotPicker } from './components/SlotPicker'
import { BookingForm } from './components/BookingForm'
import { AuthPanel } from './components/AuthPanel'
import { MyBookings } from './components/MyBookings'
import { AdminPanel } from './components/AdminPanel'
import { mutateApi } from './api'
import { formatBookingPrice } from './formatBookingPrice'

type Provider = { id: number; name: string }
type ProviderState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'success'; providers: Provider[] }

type PendingBooking = { key: string; payload: BookingPayload }
const pendingStorageKey = 'booking-system-pending'

function readPendingBooking(userId: string): PendingBooking | null {
  try {
    const data = JSON.parse(sessionStorage.getItem(`${pendingStorageKey}:${userId}`) ?? 'null')
    if (data && typeof data.key === 'string' && data.payload
      && Number.isInteger(data.payload.serviceId)
      && typeof data.payload.startTime === 'string'
      && typeof data.payload.customerName === 'string'
      && typeof data.payload.customerEmail === 'string') return data
  } catch { /* No pending request when storage is unavailable or invalid. */ }
  return null
}

const bookingDateFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/Warsaw', dateStyle: 'medium', timeStyle: 'short',
})

function App() {
  const [state, setState] = useState<ProviderState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [selectedProviderId, setSelectedProviderId] = useState<number | null>(null)
  const [selectedService, setSelectedService] =
      useState<Service | null>(null)
  
  const [selectedDate, setSelectedDate] = useState('')
  const [selectedSlot, setSelectedSlot] = useState<AvailableSlot | null>(null)

  const [customerName, setCustomerName] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const [pendingBooking, setPendingBooking] = useState<PendingBooking | null>(null)
  const [user, setUser] = useState<AuthUser | null>(null)
  const [bookingsRevision, setBookingsRevision] = useState(0)
  const activeUserId = useRef<string | null>(null)
  const pendingRef = useRef(pendingBooking)
  const requestInFlight = useRef(false)
  const [submitting, setSubmitting] = useState(false)
  const [bookingMessage, setBookingMessage] = useState('')
  const [confirmedBooking, setConfirmedBooking] = useState<Booking | null>(null)
  const [cancelling, setCancelling] = useState(false)
  const [slotsRevision, setSlotsRevision] = useState(0)

  const handleUserChange = useCallback((nextUser: AuthUser | null) => {
    if (activeUserId.current !== (nextUser?.id ?? null)) {
      const pending = nextUser ? readPendingBooking(nextUser.id) : null
      pendingRef.current = pending
      setPendingBooking(pending)
      setConfirmedBooking(null)
      setSelectedSlot(null)
      setCustomerName('')
      setCustomerEmail(nextUser?.email ?? '')
      setBookingMessage('')
      activeUserId.current = nextUser?.id ?? null
    }
    setUser(nextUser)
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    async function loadProviders() {
      try {
        const response = await fetch('/api/providers', { signal: controller.signal })
        if (!response.ok) throw new Error(`HTTP ${response.status}`)
        const data: unknown = await response.json()
        if (!Array.isArray(data) || !data.every((item) =>
          item !== null && typeof item === 'object' &&
          Number.isInteger(item.id) && typeof item.name === 'string')) {
          throw new Error('Invalid provider response')
        }
        if (!controller.signal.aborted) {
          setState({ status: 'success', providers: data as Provider[] })
        }
      } catch {
        if (!controller.signal.aborted) {
          setState({ status: 'error', message: 'Unable to load barbers. Please try again shortly.' })
        }
      }
    }

    void loadProviders()
    return () => controller.abort()
  }, [attempt])

  function retry() {
    setState({ status: 'loading' })
    setAttempt((value) => value + 1)
  }
  function handleProviderSelect(providerId: number) {
    if (providerId === selectedProviderId) return
    setSelectedProviderId(providerId)
    setSelectedService(null)
    setSelectedSlot(null)
  }

  function handleServiceSelect(service: Service) {
    if (service.id === selectedService?.id) return
    setSelectedService(service)
    setSelectedSlot(null)
  }

  function handleSlotSelect(slot: AvailableSlot) {
    setSelectedSlot(slot)
    setBookingMessage('')
  }

  function handleDateChange(date: string) {
    if (date === selectedDate) return
    setSelectedDate(date)
    setSelectedSlot(null)
  }

  function clearPending() {
    pendingRef.current = null
    setPendingBooking(null)
    try { sessionStorage.removeItem(`${pendingStorageKey}:${user!.id}`) } catch { /* In-memory result remains authoritative. */ }
  }

  async function submitBooking() {
    if (requestInFlight.current || !user) return
    let pending = pendingRef.current
    if (!pending) {
      if (!selectedService || !selectedSlot) return
      if (customerName.trim().length < 2) {
        setBookingMessage('Enter a full name with at least two characters.')
        return
      }
      pending = {
        key: crypto.randomUUID(),
        payload: { serviceId: selectedService.id, startTime: selectedSlot.startTime,
          customerName: customerName.trim(), customerEmail: customerEmail.trim() },
      }
      try {
        // Persist before sending so reloading can recover the same attempt.
        sessionStorage.setItem(`${pendingStorageKey}:${user.id}`, JSON.stringify(pending))
      } catch {
        setBookingMessage('Unable to save this booking attempt in your browser. Enable session storage and try again.')
        return
      }
      pendingRef.current = pending
      setPendingBooking(pending)
    }
    requestInFlight.current = true
    setSubmitting(true)
    setBookingMessage('')
    try {
      const response = await mutateApi('/api/bookings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Idempotency-Key': pending.key },
        body: JSON.stringify(pending.payload),
        signal: AbortSignal.timeout(30000),
      })
      if (response.status === 401 || response.status === 403) {
        handleUserChange(null)
        setBookingMessage('Your session has expired or access was denied. Sign in to the same account again; your booking attempt has been saved.')
        return
      }
      const data = await response.json()
      if (response.ok) {
        if (!Number.isInteger(data.id) || data.serviceId !== pending.payload.serviceId
          || typeof data.startTime !== 'string' || typeof data.endTime !== 'string') {
          throw new Error('Invalid booking response')
        }
        clearPending()
        setConfirmedBooking(data as Booking)
        setBookingsRevision((value) => value + 1)
        setSelectedSlot(null)
        setSlotsRevision((value) => value + 1)
        setBookingMessage('')
      } else if (response.status === 409 && data.code === 'slot_unavailable') {
        clearPending()
        setSelectedSlot(null)
        setSlotsRevision((value) => value + 1)
        setBookingMessage('This time slot is no longer available. Choose another time — your details have been saved.')
      } else if (response.status === 400 || response.status === 404) {
        clearPending()
        setBookingMessage('Check your details and selected service. The booking was not created.')
      } else {
        // Keep the exact payload and key on server errors or uncertain outcomes.
        throw new Error('Booking result could not be confirmed')
      }
    } catch {
      setBookingMessage('Unable to confirm the result. Your booking may have been saved. Retry the same attempt to check its status.')
    } finally {
      requestInFlight.current = false
      setSubmitting(false)
    }
  }

  async function cancelBooking() {
    if (!confirmedBooking || cancelling) return
    setCancelling(true)
    try {
      const response = await mutateApi(`/api/bookings/${confirmedBooking.id}/cancel`, {
        method: 'POST', signal: AbortSignal.timeout(30000),
      })
      if (!response.ok) throw new Error('Cancellation failed')
      setConfirmedBooking({ ...confirmedBooking, status: 1 })
      setBookingsRevision((value) => value + 1)
      setSlotsRevision((value) => value + 1)
      setBookingMessage('')
    } catch {
      setBookingMessage('Unable to confirm cancellation. You can retry cancelling the same booking.')
    } finally { setCancelling(false) }
  }

  function handleBookingCancelled(id: number) {
    setConfirmedBooking((booking) => booking?.id === id ? { ...booking, status: 1 } : booking)
    setBookingsRevision((value) => value + 1)
    setSlotsRevision((value) => value + 1)
  }

  const selectedProvider = state.status === 'success'
    ? state.providers.find((provider) => provider.id === selectedProviderId)
    : undefined

  return (
    <main className="booking-page">
      <header className="page-header">
        <p className="brand">BookingSystem</p>
        <h1>Book an appointment</h1>
        <p>Choose a barber to book your appointment.</p>
      </header>

      <AuthPanel user={user} disabled={submitting || cancelling} onUserChange={handleUserChange} />
      {user?.isAdministrator && <AdminPanel key={user.id} onChanged={() => {
        setSelectedService(null)
        setSelectedSlot(null)
        setConfirmedBooking(null)
        setAttempt((value) => value + 1)
        setSlotsRevision((value) => value + 1)
        setBookingsRevision((value) => value + 1)
      }} />}
      {user && <MyBookings key={`${user.id}-${bookingsRevision}`}
        disabled={submitting || cancelling} onCancelled={handleBookingCancelled} />}
      {confirmedBooking && (
        <section className="provider-section confirmation" aria-labelledby="confirmation-heading">
          <h2 id="confirmation-heading">{confirmedBooking.status === 1 ? 'Booking cancelled' : 'Booking confirmed'}</h2>
          <p>Booking reference: {confirmedBooking.id}</p>
          <p>{formatBookingPrice(confirmedBooking.priceAtBooking)}</p>
          <p>{bookingDateFormatter.format(new Date(confirmedBooking.startTime))} — Warsaw time</p>
          {confirmedBooking.status === 0 && (
            <button className="retry-button" type="button" disabled={cancelling} onClick={() => void cancelBooking()}>
              {cancelling ? 'Cancelling…' : 'Cancel booking'}
            </button>
          )}
        </section>
      )}
      {bookingMessage && <p className="booking-message" role="alert">{bookingMessage}</p>}
      {user && pendingBooking && (
        <section className="provider-section service-section" aria-label="Unconfirmed booking attempt">
          <p>Booking attempt: {bookingDateFormatter.format(new Date(pendingBooking.payload.startTime))} — Warsaw time.</p>
          <p>{pendingBooking.payload.customerName}, {pendingBooking.payload.customerEmail}</p>
          <button className="retry-button" type="button" disabled={submitting} onClick={() => void submitBooking()}>
            {submitting ? 'Saving…' : 'Retry the same attempt'}
          </button>
        </section>
      )}
      <fieldset className="booking-controls" disabled={submitting || pendingBooking !== null}>
      <section className="provider-section" aria-labelledby="provider-heading" aria-busy={state.status === 'loading'}>
        <div className="section-heading">
          <span className="step-number" aria-hidden="true">1</span>
          <h2 id="provider-heading">Choose a barber</h2>
        </div>

        {state.status === 'loading' && <p role="status">Loading barbers…</p>}
        {state.status === 'error' && (
          <div className="error-message" role="alert">
            <p>{state.message}</p>
            <button className="retry-button" type="button" onClick={retry}>Try again</button>
          </div>
        )}
        {state.status === 'success' && state.providers.length === 0 && (
          <p>No barbers are currently available.</p>
        )}
        {state.status === 'success' && (
          <div className="provider-grid">
            {state.providers.map((provider) => (
              <button
                className="provider-card"
                key={provider.id}
                type="button"
                aria-pressed={selectedProviderId === provider.id}
                onClick={() => handleProviderSelect(provider.id)}
              >
                <span className="provider-avatar" aria-hidden="true">{provider.name.charAt(0).toUpperCase()}</span>
                <span className="provider-name">{provider.name}</span>
                <span className="provider-action">{selectedProviderId === provider.id ? 'Selected' : 'Select'}</span>
              </button>
            ))}
          </div>
        )}
        <p className="selection-status" role="status">
          {selectedProvider ? `Selected barber: ${selectedProvider.name}` : ''}
        </p>
      </section>
      {selectedProviderId !== null && (
        <ServicePicker
          key={`${selectedProviderId}-${attempt}`}
          providerId={selectedProviderId}
          selectedServiceId={selectedService?.id ?? null}
          onServiceSelect={handleServiceSelect}
        />
      )}
      {selectedService && (
        <p className="selection-status" role="status">Selected service: {selectedService.name}</p>
      )}
      {selectedService && (
        <SlotPicker
          key={`${selectedService.id}-${selectedDate}-${slotsRevision}`}
          serviceId={selectedService.id}
          date={selectedDate}
          selectedSlot={selectedSlot}
          onDateChange={handleDateChange}
          onSlotSelect={handleSlotSelect}
        />
      )}
      {user && selectedService && selectedSlot && (
        <BookingForm customerName={customerName} customerEmail={customerEmail}
          submitting={submitting} onNameChange={setCustomerName} onEmailChange={setCustomerEmail}
          onSubmit={() => void submitBooking()} />
      )}
      {!user && selectedSlot && <p className="booking-message">Sign in above to book your selected time slot.</p>}
      </fieldset>
    </main>
  )
}

export default App
