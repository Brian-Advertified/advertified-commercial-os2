import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes.ts'

// Presentation wording only: persisted channel and product classifications remain unchanged.
export const mediaContent = {
  outdoor: 'Outdoor advertising',
  digitalScreens: 'Digital screens',
  outdoorPlacement: 'Outdoor placement',
  digitalScreen: 'Digital screen',
  outdoorOnly: 'Outdoor advertising and digital screens only',
} as const

export function mediaLabel(code: string): string | undefined {
  if (code === masterDataCodes.channels.ooh) return mediaContent.outdoor
  if (code === masterDataCodes.channels.dooh) return mediaContent.digitalScreens
  if (code === masterDataCodes.campaignModes.oohOnly) return mediaContent.outdoorOnly
  if (code === masterDataCodes.inventoryProductTypes.oohSite) return mediaContent.outdoorPlacement
  if (code === masterDataCodes.inventoryProductTypes.doohScreen) return mediaContent.digitalScreen
  return undefined
}

export function channelLabel(code: string): string {
  return mediaLabel(code) ?? masterDataDefinitions.channels.find(item => item.code === code)?.displayLabel ?? code
}

// Render explanatory copy clearly without rewriting retained narrative or supplier evidence.
export function clientMediaCopy(value: string): string {
  return value.replace(/\b(?:DOOH|OOH)\b/g, code => channelLabel(code))
}
