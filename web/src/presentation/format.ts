const moneyFormatters = new Map<string, Intl.NumberFormat>()
const currencyFactors = new Map<string, number>()
const numberFormatters = new Map<string, Intl.NumberFormat>()
const dateFormatters = new Map<string, Intl.DateTimeFormat>()

export function formatMoney(
  amountMinor: number,
  currency: string,
  maximumFractionDigits?: number,
  locale?: string,
): string {
  const fractionDigits = maximumFractionDigits ?? currencyFractionDigits(currency, locale)
  const key = `${locale ?? 'browser'}:${currency}:${fractionDigits}`
  let formatter = moneyFormatters.get(key)
  if (!formatter) {
    formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      maximumFractionDigits: fractionDigits,
    })
    moneyFormatters.set(key, formatter)
  }
  return formatter.format(amountMinor / currencyMinorFactor(currency, locale))
}

export function majorAmountToMinor(
  amount: number,
  currency: string,
  locale?: string,
): number {
  if (!Number.isFinite(amount) || amount < 0) {
    throw new Error('Enter a valid non-negative amount.')
  }
  return Math.round(amount * currencyMinorFactor(currency, locale))
}

export function minorAmountToInput(
  amountMinor: number,
  currency: string,
  locale?: string,
): string {
  return (amountMinor / currencyMinorFactor(currency, locale)).toFixed(
    currencyFractionDigits(currency, locale),
  )
}

export function formatNumber(
  value: number,
  maximumFractionDigits = 0,
  locale?: string,
): string {
  const key = `${locale ?? 'browser'}:${maximumFractionDigits}`
  let formatter = numberFormatters.get(key)
  if (!formatter) {
    formatter = new Intl.NumberFormat(locale, { maximumFractionDigits })
    numberFormatters.set(key, formatter)
  }
  return formatter.format(value)
}

export function formatMiB(bytes: number, locale?: string): string {
  return `${formatNumber(bytes / (1024 * 1024), 1, locale)} MiB`
}

export function formatDate(
  value: string | Date,
  locale?: string,
): string {
  return dateFormatter(locale, false).format(asDate(value))
}

export function formatDateTime(
  value: string | Date,
  locale?: string,
): string {
  return dateFormatter(locale, true).format(asDate(value))
}

export function humanizeCode(value: string, titleCase = false): string {
  const words = value.trim().toLowerCase().replaceAll('_', ' ')
  return titleCase
    ? words.replace(/\b\w/g, letter => letter.toUpperCase())
    : words
}

function currencyMinorFactor(currency: string, locale?: string) {
  const key = `${locale ?? 'browser'}:${currency}`
  const cached = currencyFactors.get(key)
  if (cached !== undefined) return cached
  const factor = 10 ** currencyFractionDigits(currency, locale)
  currencyFactors.set(key, factor)
  return factor
}

function currencyFractionDigits(currency: string, locale?: string) {
  const digits = new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
  }).resolvedOptions().maximumFractionDigits
  if (digits === undefined) {
    throw new Error('Currency fraction digits are unavailable.')
  }
  return digits
}

function dateFormatter(locale: string | undefined, withTime: boolean) {
  const key = `${locale ?? 'browser'}:${withTime ? 'datetime' : 'date'}`
  let formatter = dateFormatters.get(key)
  if (!formatter) {
    formatter = new Intl.DateTimeFormat(locale, withTime ? {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    } : {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    })
    dateFormatters.set(key, formatter)
  }
  return formatter
}

function asDate(value: string | Date) {
  const result = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(result.getTime())) {
    throw new Error('A valid date is required for display.')
  }
  return result
}
