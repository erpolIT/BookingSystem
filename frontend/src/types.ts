export type Service = {
    id: number
    providerId: number
    name: string
    durationMinutes: number
    price: number
}
export type AvailableSlot = {
  startTime: string
  endTime: string
}

export type Booking = {
  id: number
  serviceId: number
  providerId: number
  startTime: string
  endTime: string
  status: number
  customerName: string
  customerEmail: string
}

export type BookingPayload = {
  serviceId: number
  startTime: string
  customerName: string
  customerEmail: string
}

export type AuthUser = { id: string; email: string; isAdministrator: boolean }
export type Provider = { id: number; name: string }
export type WorkingHours = { dayOfWeek: number; startTime: string; endTime: string }
export type AdminBooking = Booking & { providerName: string; serviceName: string }
