import { useEffect, useState } from 'react'
import type { AvailableSlot } from '../types'

type SlotPickerProps = {
  serviceId: number
  date: string
  selectedSlot: AvailableSlot | null
  onDateChange: (date: string) => void
  onSlotSelect: (slot: AvailableSlot) => void
}

type SlotState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'success'; slots: AvailableSlot[] }

const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/Warsaw', hour: '2-digit', minute: '2-digit',
})

function getTodayInWarsaw() {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Europe/Warsaw', year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(new Date())
  const value = (type: string) => parts.find((part) => part.type === type)?.value
  return `${value('year')}-${value('month')}-${value('day')}`
}

function isSlot(value: unknown): value is AvailableSlot {
  if (value === null || typeof value !== 'object') return false
  const slot = value as Record<string, unknown>
  return typeof slot.startTime === 'string' && typeof slot.endTime === 'string'
    && Number.isFinite(Date.parse(slot.startTime))
    && Number.isFinite(Date.parse(slot.endTime))
    && Date.parse(slot.startTime) < Date.parse(slot.endTime)
}

export function SlotPicker({ serviceId, date, selectedSlot, onDateChange, onSlotSelect }: SlotPickerProps) {
  const [state, setState] = useState<SlotState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!date) return
    const controller = new AbortController()

    async function loadSlots() {
      try {
        const response = await fetch(
          `/api/services/${serviceId}/available-slots?date=${encodeURIComponent(date)}`,
          { signal: controller.signal },
        )
        if (!response.ok) throw new Error(`HTTP ${response.status}`)
        const data: unknown = await response.json()
        if (!Array.isArray(data) || !data.every(isSlot)) throw new Error('Invalid slot response')
        if (!controller.signal.aborted) setState({ status: 'success', slots: data })
      } catch {
        if (!controller.signal.aborted) setState({ status: 'error' })
      }
    }

    void loadSlots()
    return () => controller.abort()
  }, [serviceId, date, attempt])

  function retry() {
    setState({ status: 'loading' })
    setAttempt((value) => value + 1)
  }

  return (
    <section className="provider-section service-section" aria-labelledby="slot-heading"
      aria-busy={Boolean(date) && state.status === 'loading'}>
      <div className="section-heading">
        <span className="step-number" aria-hidden="true">3</span>
        <h2 id="slot-heading">Choose a date and time</h2>
      </div>
      <label className="date-label" htmlFor="booking-date">Appointment date</label>
      <input className="date-input" id="booking-date" type="date" value={date}
        min={getTodayInWarsaw()} onChange={(event) => onDateChange(event.target.value)} />
      <p className="timezone-note">All times are shown in the salon time zone (Europe/Warsaw).</p>
      {!date && <p role="status">Choose a date to see available times.</p>}
      {date && state.status === 'loading' && <p role="status">Loading available times…</p>}
      {date && state.status === 'error' && (
        <div className="error-message" role="alert">
          <p>Unable to load available times.</p>
          <button className="retry-button" type="button" onClick={retry}>Try again</button>
        </div>
      )}
      {date && state.status === 'success' && state.slots.length === 0 && (
        <p role="status">No appointments are available on this day. Choose another date.</p>
      )}
      {date && state.status === 'success' && (
        <div className="slot-grid">
          {state.slots.map((slot) => (
            <button className="slot-button" type="button" key={slot.startTime}
              aria-pressed={selectedSlot?.startTime === slot.startTime}
              aria-label={`${timeFormatter.format(new Date(slot.startTime))}–${timeFormatter.format(new Date(slot.endTime))}`}
              onClick={() => onSlotSelect(slot)}>
              {timeFormatter.format(new Date(slot.startTime))}
            </button>
          ))}
        </div>
      )}
      {selectedSlot && (
        <p className="selection-status" role="status">
          Selected appointment: {date}, {timeFormatter.format(new Date(selectedSlot.startTime))}–{timeFormatter.format(new Date(selectedSlot.endTime))}
        </p>
      )}
    </section>
  )
}
