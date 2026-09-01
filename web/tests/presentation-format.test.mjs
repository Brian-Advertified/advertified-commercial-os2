import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatMoney,
  majorAmountToMinor,
  minorAmountToInput,
} from '../src/presentation/format.ts'

test('money conversion follows each currency minor-unit exponent', () => {
  assert.equal(majorAmountToMinor(1234, 'JPY', 'en-US'), 1234)
  assert.equal(majorAmountToMinor(12.34, 'USD', 'en-US'), 1234)
  assert.equal(majorAmountToMinor(12.345, 'KWD', 'en-US'), 12345)
  assert.equal(minorAmountToInput(12345, 'KWD', 'en-US'), '12.345')
})

test('money display uses the requested locale without a fixed market assumption', () => {
  assert.equal(formatMoney(123456, 'USD', undefined, 'en-US'), '$1,234.56')
  assert.match(formatMoney(123456, 'JPY', undefined, 'en-US'), /123,456/u)
  assert.match(formatMoney(123456, 'KWD', undefined, 'en-US'), /123\.456/u)
})
