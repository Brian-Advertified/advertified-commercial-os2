import { findMediaInventoryPartner } from '../public/data/mediaInventoryPartners.ts'

type InventoryIdentity = {
  name: string
  supplierName: string
}

const identityLogos: ReadonlyArray<readonly [RegExp, string]> = [
  [/\bMETRO FM\b/iu, 'Metro FM'],
  [/\b5FM\b/iu, '5FM'],
  [/\bRSG\b/iu, 'RSG'],
  [/\bSAFM\b/iu, 'SAfm'],
  [/\bLIGWALAGWALA FM\b/iu, 'Ligwalagwala FM'],
  [/\bIKWEKWEZI FM\b/iu, 'Ikwekwezi FM'],
  [/\bMOTSWEDING FM\b/iu, 'Motsweding FM'],
  [/\bLESEDI FM\b/iu, 'Lesedi FM'],
  [/\bUMHLOBO WENENE FM\b/iu, 'Umhlobo Wenene FM'],
  [/\bTRUFM\b/iu, 'TruFM'],
  [/\bXK FM\b/iu, 'XK FM'],
  [/\bLOTUS FM\b/iu, 'Lotus FM'],
  [/\bGOOD HOPE FM\b/iu, 'Good Hope FM'],
  [/\bTHOBELA FM\b/iu, 'Thobela FM'],
  [/\bPHALAPHALA FM\b/iu, 'Phalaphala FM'],
  [/\bMUNGHANA LONENE FM\b/iu, 'Munghana Lonene FM'],
  [/\bCHANNEL AFRICA\b/iu, 'Channel Africa'],
  [/\bUKHOZI FM\b/iu, 'Ukhozi FM'],
  [/\bRADIO 2000\b/iu, 'Radio 2000'],
  [/\bKAYA (?:959|FM)\b/iu, 'Kaya 959'],
  [/\b947\b/iu, '947 (Highveld)'],
  [/\b702\b/iu, '702'],
  [/\bSABC NEWS\b/iu, 'SABC News'],
  [/\bSABC SPORT\b/iu, 'SABC Sport'],
  [/\bSABC ?1\b/iu, 'SABC 1'],
  [/\bSABC ?2\b/iu, 'SABC 2'],
  [/\bSABC ?3\b/iu, 'SABC 3'],
  [/\bJACARANDA FM\b/iu, 'Jacaranda FM'],
  [/\bALGOA FM\b/iu, 'Algoa FM'],
  [/\bJOZI FM\b/iu, 'Jozi FM'],
  [/\bSMILE 90[.]4 ?FM\b/iu, 'Smile 90.4FM'],
  [/\bY(?:FM| RADIO)?\b/iu, 'Y'],
]

const supplierLogos: Readonly<Record<string, string>> = {
  'Algoa FM': 'Algoa FM',
  'Jozi FM': 'Jozi FM',
  'Kaya 959': 'Kaya 959',
  'Primedia Broadcasting': 'Primedia',
  'Smile 90.4FM': 'Smile 90.4FM',
  'eMedia': 'eMedia Sales',
  'DStv Media Sales': 'DStv',
}

export function inventoryArtwork(item: InventoryIdentity) {
  const identity = `${item.name} ${item.supplierName}`
  const brand = identityLogos.find(([pattern]) => pattern.test(identity))?.[1]
    ?? supplierLogos[item.supplierName]
  return brand ? findMediaInventoryPartner(brand)?.assetPath : undefined
}

export function inventoryIdentityLabel(item: InventoryIdentity) {
  const identity = `${item.name} ${item.supplierName}`
  return identityLogos.find(([pattern]) => pattern.test(identity))?.[1]
    ?? item.name.split(/\s+—\s+/u)[0]
    ?? item.supplierName
}
