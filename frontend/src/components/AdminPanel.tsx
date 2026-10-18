import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { mutateApi } from '../api'
import type { AdminBooking, Provider, Service, WorkingHours } from '../types'

const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const dateFormatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Europe/Warsaw', year: 'numeric', month: '2-digit', day: '2-digit',
})
const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/Warsaw', hour: '2-digit', minute: '2-digit',
})

async function readApi<T>(url: string, signal: AbortSignal): Promise<T> {
  const response = await fetch(url, { signal, cache: 'no-store' })
  if (!response.ok) throw new Error(response.status === 401 || response.status === 403
    ? 'Administrator access required. Sign in again to refresh your permissions.'
    : 'Unable to load data. Please try again.')
  return response.json() as Promise<T>
}

async function save(url: string, method: string, body?: unknown) {
  const response = await mutateApi(url, {
    method, headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    const data = await response.json().catch(() => ({}))
    throw new Error(response.status === 401 || response.status === 403
      ? 'Administrator access required. Sign in again.'
      : Object.values(data.errors ?? {}).flat().join(' ') || data.message || 'Unable to save changes.')
  }
}

export function AdminPanel({ onChanged }: { onChanged: () => void }) {
  const [providers, setProviders] = useState<Provider[]>([])
  const [providerId, setProviderId] = useState('')
  const [name, setName] = useState('')
  const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(true)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const inFlight = useRef(false)

  useEffect(() => {
    const controller = new AbortController()
    void readApi<Provider[]>('/api/providers', controller.signal).then((data) => {
      if (!controller.signal.aborted) { setProviders(data); setLoading(false) }
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) { setMessage(String(error)); setLoading(false) }
    })
    return () => controller.abort()
  }, [revision])

  function changed() { setRevision((value) => value + 1); onChanged() }

  async function addProvider(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (inFlight.current) return
    inFlight.current = true; setBusy(true); setMessage('')
    try {
      await save('/api/providers', 'POST', { name: name.trim() })
      setName(''); setMessage('Barber added.'); changed()
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Result unknown. Refresh the list before trying again.') }
    finally { inFlight.current = false; setBusy(false) }
  }

  return <section className="provider-section admin-panel" aria-labelledby="admin-heading">
    <h2 id="admin-heading">Administrator</h2>
    <p className="timezone-note">Manage barbers, services and appointments. All schedules use Warsaw time.</p>
    {message && <p role="status" className="booking-message">{message}</p>}
    {loading && <p role="status">Loading barbers…</p>}
    <button className="text-button" type="button" disabled={busy} onClick={changed}>Refresh administration data</button>
    <form onSubmit={(event) => void addProvider(event)}>
      <fieldset className="customer-fields" disabled={busy || loading}>
        <legend>Add a barber</legend>
        <label htmlFor="new-provider-name">Name</label>
        <input id="new-provider-name" required minLength={2} maxLength={100} value={name} onChange={(event) => setName(event.target.value)} />
        <button className="retry-button" type="submit">{busy ? 'Saving…' : 'Add barber'}</button>
      </fieldset>
    </form>
    <label className="date-label" htmlFor="admin-provider">Manage a barber</label>
    <select id="admin-provider" disabled={busy || loading} value={providerId} onChange={(event) => setProviderId(event.target.value)}>
      <option value="">Choose a barber</option>
      {providers.map((provider) => <option key={provider.id} value={provider.id}>{provider.name}</option>)}
    </select>
    {providerId && <ProviderEditor key={`${providerId}-${revision}`} providerId={Number(providerId)} onChanged={changed} />}
    <DailyBookings revision={revision} providers={providers} onChanged={changed} />
  </section>
}

function ProviderEditor({ providerId, onChanged }: { providerId: number; onChanged: () => void }) {
  const [data, setData] = useState<{ services: Service[]; hours: WorkingHours[] } | null>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const inFlight = useRef(false)
  const [name, setName] = useState('')
  const [duration, setDuration] = useState('30')
  const [price, setPrice] = useState('60')
  const [day, setDay] = useState('1')
  const [start, setStart] = useState('09:00')
  const [end, setEnd] = useState('17:00')

  useEffect(() => {
    const controller = new AbortController()
    void Promise.all([
      readApi<Service[]>(`/api/providers/${providerId}/services`, controller.signal),
      readApi<WorkingHours[]>(`/api/providers/${providerId}/working-hours`, controller.signal),
    ]).then(([services, hours]) => {
      if (!controller.signal.aborted) setData({ services, hours })
    }).catch((error: unknown) => { if (!controller.signal.aborted) setMessage(String(error)) })
    return () => controller.abort()
  }, [providerId])

  async function update(url: string, method: string, body?: unknown) {
    if (inFlight.current) return
    inFlight.current = true; setBusy(true); setMessage('')
    try { await save(url, method, body); onChanged() }
    catch (error) { setMessage(error instanceof Error ? error.message : 'Result unknown. Refresh before trying again.') }
    finally { inFlight.current = false; setBusy(false) }
  }

  return <div className="admin-editor">
    {message && <p className="booking-message" role="alert">{message}</p>}
    {!data && !message && <p role="status">Loading services and hours…</p>}
    {data && <>
      <h3>Services</h3>
      {data.services.length === 0 && <p>No services yet.</p>}
      <ul>{data.services.map((service) => <li key={service.id}>{service.name} · {service.durationMinutes} min · {service.price.toFixed(2)} PLN</li>)}</ul>
      <form onSubmit={(event) => {
        event.preventDefault()
        void update('/api/services', 'POST', { providerId, name: name.trim(), durationMinutes: Number(duration), price: Number(price) })
      }}>
        <fieldset className="customer-fields" disabled={busy}>
          <legend>Add a service</legend>
          <label htmlFor="admin-service-name">Service name</label>
          <input id="admin-service-name" required minLength={2} maxLength={100} value={name} onChange={(event) => setName(event.target.value)} />
          <label htmlFor="admin-duration">Duration (minutes)</label>
          <input id="admin-duration" type="number" required min={1} max={480} step={1} value={duration} onChange={(event) => setDuration(event.target.value)} />
          <label htmlFor="admin-price">Price (PLN)</label>
          <input id="admin-price" type="number" required min="0.01" max="10000" step="0.01" value={price} onChange={(event) => setPrice(event.target.value)} />
          <button className="retry-button" type="submit">{busy ? 'Saving…' : 'Add service'}</button>
        </fieldset>
      </form>
      <h3>Weekly working hours</h3>
      <p className="timezone-note">Days without hours are closed. Changes do not cancel existing appointments.</p>
      {data.hours.length === 0 && <p>No working hours configured.</p>}
      <ul>{data.hours.map((hours) => <li key={hours.dayOfWeek}>
        {days[hours.dayOfWeek]}: {hours.startTime.slice(0, 5)}–{hours.endTime.slice(0, 5)}{' '}
        <button className="text-button" type="button" disabled={busy} onClick={() => {
          if (window.confirm(`Close ${days[hours.dayOfWeek]}? Existing bookings will remain.`))
            void update(`/api/providers/${providerId}/working-hours/${hours.dayOfWeek}`, 'DELETE')
        }}>Close day</button>
      </li>)}</ul>
      <form onSubmit={(event) => {
        event.preventDefault()
        if (start >= end) { setMessage('Closing time must be later than opening time.'); return }
        void update(`/api/providers/${providerId}/working-hours/${day}`, 'PUT', { startTime: start, endTime: end })
      }}>
        <fieldset className="customer-fields" disabled={busy}>
          <legend>Set hours for a weekday</legend>
          <label htmlFor="admin-weekday">Day</label>
          <select id="admin-weekday" value={day} onChange={(event) => setDay(event.target.value)}>
            {days.map((label, index) => <option key={label} value={index}>{label}</option>)}
          </select>
          <label htmlFor="admin-opening">Opening time</label>
          <input id="admin-opening" type="time" required value={start} onChange={(event) => setStart(event.target.value)} />
          <label htmlFor="admin-closing">Closing time</label>
          <input id="admin-closing" type="time" required value={end} onChange={(event) => setEnd(event.target.value)} />
          <button className="retry-button" type="submit">{busy ? 'Saving…' : 'Save working hours'}</button>
        </fieldset>
      </form>
    </>}
  </div>
}

function DailyBookings({ providers, revision, onChanged }: { providers: Provider[]; revision: number; onChanged: () => void }) {
  const [date, setDate] = useState(() => dateFormatter.format(new Date()))
  const [providerId, setProviderId] = useState('')
  return <div className="admin-editor">
    <h3>Daily appointments</h3>
    <label className="date-label" htmlFor="admin-booking-date">Date (Warsaw)</label>
    <input className="date-input" id="admin-booking-date" type="date" required value={date} onChange={(event) => setDate(event.target.value)} />
    <label className="date-label" htmlFor="admin-booking-provider">Barber</label>
    <select id="admin-booking-provider" value={providerId} onChange={(event) => setProviderId(event.target.value)}>
      <option value="">All barbers</option>
      {providers.map((provider) => <option key={provider.id} value={provider.id}>{provider.name}</option>)}
    </select>
    {date && <BookingList key={`${date}-${providerId}-${revision}`} date={date} providerId={providerId} onChanged={onChanged} />}
  </div>
}

function BookingList({ date, providerId, onChanged }: { date: string; providerId: string; onChanged: () => void }) {
  const [data, setData] = useState<{ bookings: AdminBooking[]; truncated: boolean } | null>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const inFlight = useRef(false)
  useEffect(() => {
    const controller = new AbortController()
    const params = new URLSearchParams({ date })
    if (providerId) params.set('providerId', providerId)
    void readApi<{ bookings: AdminBooking[]; truncated: boolean }>(`/api/admin/bookings?${params}`, controller.signal)
      .then((result) => { if (!controller.signal.aborted) setData(result) })
      .catch((error: unknown) => { if (!controller.signal.aborted) setMessage(String(error)) })
    return () => controller.abort()
  }, [date, providerId])

  async function cancel(booking: AdminBooking) {
    if (inFlight.current || !window.confirm(`Cancel booking #${booking.id} for ${booking.customerName}?`)) return
    inFlight.current = true; setBusy(true); setMessage('')
    try {
      await save(`/api/admin/bookings/${booking.id}/cancel`, 'POST')
      setData((value) => value && ({ ...value, bookings: value.bookings.map((item) => item.id === booking.id ? { ...item, status: 1 } : item) }))
      onChanged()
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Unable to confirm cancellation. Refresh to check its status.') }
    finally { inFlight.current = false; setBusy(false) }
  }

  return <div aria-busy={!data && !message}>
    {message && <p className="booking-message" role="alert">{message}</p>}
    {!data && !message && <p role="status">Loading appointments…</p>}
    {data?.truncated && <p role="status">Showing the first 500 appointments. Filter by barber to narrow the list.</p>}
    {data?.bookings.length === 0 && <p className="timezone-note">No appointments for this date.</p>}
    {data?.bookings.map((booking) => <article className="booking-row" key={booking.id}>
      <div>
        <strong>#{booking.id} · {timeFormatter.format(new Date(booking.startTime))}–{timeFormatter.format(new Date(booking.endTime))}</strong>
        <p>{booking.providerName} · {booking.serviceName}</p>
        <p>{booking.customerName} · {booking.customerEmail}</p>
        <p>{booking.status === 0 ? 'Confirmed' : 'Cancelled'}</p>
      </div>
      {booking.status === 0 && <button type="button" className="retry-button" disabled={busy} onClick={() => void cancel(booking)}>Cancel appointment</button>}
    </article>)}
  </div>
}
