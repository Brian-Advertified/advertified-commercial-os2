import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'

type ChannelCode = typeof masterDataCodes.channels[keyof typeof masterDataCodes.channels]
type MediaTone = 'ooh' | 'dooh' | 'radio' | 'tv' | 'print' | 'digital' | 'social' |
  'influencer' | 'experiential' | 'podcast' | 'retail' | 'transit' | 'mall' | 'email' | 'mobile'

const tones: Record<ChannelCode, MediaTone> = {
  [masterDataCodes.channels.ooh]: 'ooh',
  [masterDataCodes.channels.dooh]: 'dooh',
  [masterDataCodes.channels.radio]: 'radio',
  [masterDataCodes.channels.tv]: 'tv',
  [masterDataCodes.channels.print]: 'print',
  [masterDataCodes.channels.digital]: 'digital',
  [masterDataCodes.channels.social]: 'social',
  [masterDataCodes.channels.influencer]: 'influencer',
  [masterDataCodes.channels.experiential]: 'experiential',
  [masterDataCodes.channels.podcast]: 'podcast',
  [masterDataCodes.channels.retail]: 'retail',
  [masterDataCodes.channels.transit]: 'transit',
  [masterDataCodes.channels.mall]: 'mall',
  [masterDataCodes.channels.email]: 'email',
  [masterDataCodes.channels.mobile]: 'mobile',
}

export function mediaVisual(channel: string) {
  const definition = masterDataDefinitions.channels.find((item) => item.code === channel)
  return {
    label: definition?.displayLabel ?? channel,
    tone: tones[channel as ChannelCode] ?? 'digital',
  }
}
