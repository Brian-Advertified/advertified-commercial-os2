const moneyFormatters = new Map<string, Intl.NumberFormat>()

export function formatMoney(
  amountMinor: number,
  currency: string,
  maximumFractionDigits = 0,
): string {
  const key = `${currency}:${maximumFractionDigits}`
  let formatter = moneyFormatters.get(key)
  if (!formatter) {
    formatter = new Intl.NumberFormat('en-ZA', {
      style: 'currency',
      currency,
      maximumFractionDigits,
    })
    moneyFormatters.set(key, formatter)
  }
  return formatter.format(amountMinor / 100)
}

export function humanizeCode(value: string, titleCase = false): string {
  const words = value.toLowerCase().replaceAll('_', ' ')
  return titleCase
    ? words.replace(/\b\w/g, letter => letter.toUpperCase())
    : words
}
