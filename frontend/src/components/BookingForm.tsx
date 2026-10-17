import type { FormEvent } from 'react'

type BookingFormProps = {
  customerName: string
  customerEmail: string
  submitting: boolean
  onNameChange: (name: string) => void
  onEmailChange: (email: string) => void
  onSubmit: () => void
}

export function BookingForm({ customerName, customerEmail, submitting,
  onNameChange, onEmailChange, onSubmit }: BookingFormProps) {
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit()
  }

  return (
    <section className="provider-section service-section" aria-labelledby="customer-heading">
      <div className="section-heading">
        <span className="step-number" aria-hidden="true">4</span>
        <h2 id="customer-heading">Your details</h2>
      </div>
      <form onSubmit={handleSubmit}>
        <fieldset className="customer-fields" disabled={submitting}>
          <label htmlFor="customer-name">Full name</label>
          <input id="customer-name" autoComplete="name" required minLength={2} maxLength={100}
            value={customerName} onChange={(event) => onNameChange(event.target.value)} />
          <label htmlFor="customer-email">Email</label>
          <input id="customer-email" type="email" autoComplete="email" required maxLength={254}
            value={customerEmail} onChange={(event) => onEmailChange(event.target.value)} />
          <button className="retry-button" type="submit">
            {submitting ? 'Saving…' : 'Book appointment'}
          </button>
        </fieldset>
      </form>
    </section>
  )
}
