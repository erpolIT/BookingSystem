import { useEffect, useState } from 'react'
import type { Booking } from '../types'
import { mutateApi } from '../api'
import { formatBookingPrice } from '../formatBookingPrice'

type Props = { onCancelled: (id: number) => void; disabled: boolean }
type State = { status: 'loading' } | { status: 'error' } | { status: 'success'; bookings: Booking[] }
const formatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/Warsaw', dateStyle: 'medium', timeStyle: 'short',
})

export function MyBookings({ onCancelled, disabled }: Props) {
  const [state, setState] = useState<State>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [cancellingId, setCancellingId] = useState<number | null>(null)
  const [message, setMessage] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const response = await fetch('/api/bookings', { signal: controller.signal, cache: 'no-store' })
        if (!response.ok) throw new Error('Could not load bookings')
        const data = await response.json()
        if (!Array.isArray(data)) throw new Error('Invalid booking list')
        if (!controller.signal.aborted) setState({ status: 'success', bookings: data })
      } catch { if (!controller.signal.aborted) setState({ status: 'error' }) }
    }
    void load()
    return () => controller.abort()
  }, [attempt])

  async function cancel(id: number) {
    if (cancellingId !== null || disabled) return
    setCancellingId(id)
    setMessage('')
    try {
      const response = await mutateApi(`/api/bookings/${id}/cancel`)
      if (!response.ok) throw new Error('Cancellation failed')
      onCancelled(id)
    } catch { setMessage('Unable to confirm cancellation. Check your session and try again.') }
    finally { setCancellingId(null) }
  }

  return (
    <section className="provider-section my-bookings" aria-labelledby="my-bookings-heading">
      <h2 id="my-bookings-heading">My bookings</h2>
      {state.status === 'loading' && <p role="status">Loading bookings…</p>}
      {state.status === 'error' && <div role="alert"><p>Unable to load your bookings.</p>
        <button className="retry-button" type="button" onClick={() => {
          setState({ status: 'loading' }); setAttempt((value) => value + 1)
        }}>Try again</button></div>}
      {message && <p role="alert">{message}</p>}
      {state.status === 'success' && state.bookings.length === 0 && <p>You have no bookings yet.</p>}
      {state.status === 'success' && state.bookings.map((booking) => (
        <article className="booking-row" key={booking.id}>
          <div><strong>Appointment #{booking.id}</strong>
            <p>{formatter.format(new Date(booking.startTime))} — Warsaw time</p>
            <p>{formatBookingPrice(booking.priceAtBooking)}</p>
            <p>{booking.status === 1 ? 'Cancelled' : 'Confirmed'}</p></div>
          {booking.status === 0 && <button className="retry-button" type="button" disabled={disabled || cancellingId !== null}
            onClick={() => void cancel(booking.id)}>{cancellingId === booking.id ? 'Cancelling…' : `Cancel appointment #${booking.id}`}</button>}
        </article>
      ))}
    </section>
  )
}
