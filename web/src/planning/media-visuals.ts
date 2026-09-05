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

const toneColors: Record<MediaTone, string> = {
  ooh: '#6038f5',
  dooh: '#7a45ff',
  radio: '#2089ff',
  tv: '#175cd3',
  print: '#8794a8',
  digital: '#22bdd0',
  social: '#ec6ba8',
  influencer: '#c44fdc',
  experiential: '#f5a524',
  podcast: '#7f56d9',
  retail: '#12b76a',
  transit: '#0ba5ec',
  mall: '#f79009',
  email: '#6172f3',
  mobile: '#2e90fa',
}

export function mediaVisual(channel: string) {
  const definition = masterDataDefinitions.channels.find((item) => item.code === channel)
  const tone = tones[channel as ChannelCode] ?? 'digital'
  return {
    label: definition?.displayLabel ?? channel,
    tone,
    color: toneColors[tone],
  }
}
