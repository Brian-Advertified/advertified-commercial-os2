import assert from 'node:assert/strict'
import test from 'node:test'
import { bookingInvestment } from '../src/home/dashboard-metrics.ts'

const line = (channel, amount, currency = 'ZAR', status = 'CONFIRMED') =>
  ({ channel, clientPriceMinor: amount, currency, status })

test('dashboard media value uses confirmed lines without combining currencies or truncating channels', () => {
  const channels = ['OOH', 'DOOH', 'RADIO', 'TV', 'PRINT', 'DIGITAL', 'SOCIAL']
  const bookings = channels.map(channel => line(channel, 100))
  bookings.push(line('OOH', 9000, 'USD'), line('TV', 8000, 'GBP'))
  for (const status of ['DRAFT', 'PENDING_SUPPLIER', 'CANCELLED', 'APPROVED', 'ACTIVE']) {
    bookings.push(line('OOH', 5000, 'ZAR', status))
  }
  const view = bookingInvestment(bookings, 'ZAR')
  assert.equal(view.totalMinor, 700)
  assert.equal(view.channels.length, channels.length)
  assert.equal(view.channels.reduce((sum, [, amount]) => sum + amount, 0), view.totalMinor)
  assert.deepEqual(view.otherCurrencies, ['GBP', 'USD'])
  assert.equal(view.confirmedCount, 9)
})

test('missing, invalid and overflowing prices are not presented as zero or a complete partial total', () => {
  for (const price of [null, -1, Number.NaN, Number.MAX_SAFE_INTEGER]) {
    const view = bookingInvestment([line('OOH', 100), line('RADIO', price)], 'ZAR')
    assert.equal(view.totalMinor, null)
    assert.deepEqual(view.channels, [])
  }
  const foreign = bookingInvestment([line('OOH', 100), line('RADIO', null, 'USD')], 'ZAR')
  assert.equal(foreign.totalMinor, 100)
  assert.deepEqual(foreign.otherCurrencies, ['USD'])
})

test('an empty confirmed currency set and a real zero price remain distinguishable from unknown prices', () => {
  assert.equal(bookingInvestment([], 'ZAR').totalMinor, 0)
  assert.equal(bookingInvestment([line('OOH', 0)], 'ZAR').totalMinor, 0)
  assert.equal(bookingInvestment([line('OOH', null)], 'ZAR').totalMinor, null)
})
