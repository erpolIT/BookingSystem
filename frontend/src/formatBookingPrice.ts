const priceFormatter = new Intl.NumberFormat('en-GB', {
  style: 'currency', currency: 'PLN',
})

export function formatBookingPrice(price: number | null | undefined): string {
  return price == null ? 'Price not recorded' : priceFormatter.format(price)
}
