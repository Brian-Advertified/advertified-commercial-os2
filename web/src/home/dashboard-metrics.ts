import type { Booking } from '../api/booking-schemas'
import { masterDataCodes } from '../generated/master-data-codes.ts'

type InvestmentLine = Pick<Booking, 'status' | 'currency' | 'channel' | 'clientPriceMinor'>

export function isConfirmedBooking(status: string) {
  return status === masterDataCodes.lifecycleStatuses.confirmed
}

export function bookingInvestment(bookings: readonly InvestmentLine[], currency: string) {
  const confirmed = bookings.filter(item => isConfirmedBooking(item.status))
  const currencyLines = confirmed.filter(item => item.currency === currency)
  const missingAmounts = currencyLines.filter(item => item.clientPriceMinor === null ||
    !Number.isSafeInteger(item.clientPriceMinor) || item.clientPriceMinor < 0).length
  const otherCurrencies = [...new Set(confirmed.filter(item => item.currency !== currency)
    .map(item => item.currency))].sort()
  const totals = new Map<string, number>()
  if (missingAmounts === 0) {
    for (const item of currencyLines) {
      totals.set(item.channel, (totals.get(item.channel) ?? 0) + (item.clientPriceMinor as number))
    }
  }
  const sum = [...totals.values()].reduce((total, amount) => total + amount, 0)
  const totalMinor = missingAmounts === 0 && Number.isSafeInteger(sum) ? sum : null
  const channels = totalMinor === null ? [] : [...totals.entries()].sort((left, right) => right[1] - left[1])
  return { totalMinor, channels, missingAmounts, otherCurrencies, confirmedCount: confirmed.length }
}
