import { useEffect, useState } from 'react'
import type { Service } from '../types'

type ServicePickerProps = {
  providerId: number
  selectedServiceId: number | null
  onServiceSelect: (service: Service) => void
}

type ServiceState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'success'; services: Service[] }

const priceFormatter = new Intl.NumberFormat('en-GB', {
  style: 'currency', currency: 'PLN',
})

function isService(value: unknown): value is Service {
  if (value === null || typeof value !== 'object') return false
  const service = value as Record<string, unknown>
  return Number.isInteger(service.id) && Number.isInteger(service.providerId)
    && typeof service.name === 'string' && Number.isInteger(service.durationMinutes)
    && typeof service.price === 'number' && Number.isFinite(service.price)
}

export function ServicePicker({ providerId, selectedServiceId, onServiceSelect }: ServicePickerProps) {
  const [state, setState] = useState<ServiceState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    async function loadServices() {
      try {
        const response = await fetch(`/api/providers/${providerId}/services`, {
          signal: controller.signal,
        })
        if (!response.ok) throw new Error(`HTTP ${response.status}`)
        const data: unknown = await response.json()
        if (!Array.isArray(data) || !data.every(isService)
          || !data.every((service) => service.providerId === providerId)) {
          throw new Error('Invalid service response')
        }
        if (!controller.signal.aborted) {
          setState({ status: 'success', services: data })
        }
      } catch {
        if (!controller.signal.aborted) setState({ status: 'error' })
      }
    }

    void loadServices()
    return () => controller.abort()
  }, [providerId, attempt])

  function retry() {
    setState({ status: 'loading' })
    setAttempt((value) => value + 1)
  }

  return (
    <section className="provider-section service-section" aria-labelledby="service-heading"
      aria-busy={state.status === 'loading'}>
      <div className="section-heading">
        <span className="step-number" aria-hidden="true">2</span>
        <h2 id="service-heading">Choose a service</h2>
      </div>
      {state.status === 'loading' && <p role="status">Loading services…</p>}
      {state.status === 'error' && (
        <div className="error-message" role="alert">
          <p>Unable to load services.</p>
          <button className="retry-button" type="button" onClick={retry}>Try again</button>
        </div>
      )}
      {state.status === 'success' && state.services.length === 0 && (
        <p role="status">This barber has no services yet.</p>
      )}
      {state.status === 'success' && (
        <div className="service-list">
          {state.services.map((service) => (
            <button className="service-card" type="button" key={service.id}
              aria-pressed={selectedServiceId === service.id}
              onClick={() => onServiceSelect(service)}>
              <span className="service-description">
                <span className="provider-name">{service.name}</span>
                <span className="service-duration">{service.durationMinutes} min</span>
              </span>
              <span className="service-price">{priceFormatter.format(service.price)}</span>
              <span className="provider-action">{selectedServiceId === service.id ? 'Selected' : 'Select'}</span>
            </button>
          ))}
        </div>
      )}
    </section>
  )
}
