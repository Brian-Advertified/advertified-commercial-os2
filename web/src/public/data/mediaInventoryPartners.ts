export interface MediaInventoryPartner {
  name: string;
  assetPath: string;
  width: number;
  height: number;
}

export const mediaInventoryPartners: readonly MediaInventoryPartner[] = [
  { name: 'SABC 1', assetPath: '/assets/media-inventory-partners/sabc-1-mono.webp', width: 320, height: 140 },
  { name: 'SABC 2', assetPath: '/assets/media-inventory-partners/sabc-2-mono.webp', width: 320, height: 140 },
  { name: 'SABC 3', assetPath: '/assets/media-inventory-partners/sabc-3-mono.webp', width: 320, height: 140 },
  { name: 'SABC News', assetPath: '/assets/media-inventory-partners/sabc-news.png', width: 301, height: 161 },
  { name: 'SABC Sport', assetPath: '/assets/media-inventory-partners/sabc-sport.png', width: 285, height: 172 },
  { name: 'Metro FM', assetPath: '/assets/media-inventory-partners/metro-fm-mono.webp', width: 320, height: 140 },
  { name: '5FM', assetPath: '/assets/media-inventory-partners/5fm-mono.webp', width: 320, height: 140 },
  { name: 'RSG', assetPath: '/assets/media-inventory-partners/rsg-mono.webp', width: 320, height: 140 },
  { name: 'SAfm', assetPath: '/assets/media-inventory-partners/safm-mono.webp', width: 320, height: 140 },
  { name: 'Ligwalagwala FM', assetPath: '/assets/media-inventory-partners/ligwalagwala-fm-mono.webp', width: 320, height: 140 },
  { name: 'Ikwekwezi FM', assetPath: '/assets/media-inventory-partners/ikwekwezi-fm-mono.webp', width: 320, height: 140 },
  { name: 'Motsweding FM', assetPath: '/assets/media-inventory-partners/motsweding-fm-mono.webp', width: 320, height: 140 },
  { name: 'Lesedi FM', assetPath: '/assets/media-inventory-partners/lesedi-fm-mono.webp', width: 320, height: 140 },
  { name: 'Umhlobo Wenene FM', assetPath: '/assets/media-inventory-partners/umhlobo-wenene-fm-mono.webp', width: 320, height: 140 },
  { name: 'TruFM', assetPath: '/assets/media-inventory-partners/trufm-mono.webp', width: 320, height: 140 },
  { name: 'XK FM', assetPath: '/assets/media-inventory-partners/xk-fm-mono.webp', width: 320, height: 140 },
  { name: 'Lotus FM', assetPath: '/assets/media-inventory-partners/lotus-fm-mono.webp', width: 320, height: 140 },
  { name: 'Good Hope FM', assetPath: '/assets/media-inventory-partners/good-hope-fm-mono.webp', width: 320, height: 140 },
  { name: 'Thobela FM', assetPath: '/assets/media-inventory-partners/thobela-fm-mono.webp', width: 320, height: 140 },
  { name: 'Phalaphala FM', assetPath: '/assets/media-inventory-partners/phalaphala-fm-mono.webp', width: 320, height: 140 },
  { name: 'Munghana Lonene FM', assetPath: '/assets/media-inventory-partners/munghana-lonene-fm-mono.webp', width: 320, height: 140 },
  { name: 'Channel Africa', assetPath: '/assets/media-inventory-partners/channel-africa-mono.webp', width: 320, height: 140 },
  { name: 'Ukhozi FM', assetPath: '/assets/media-inventory-partners/ukhozi-fm-mono.webp', width: 320, height: 140 },
  { name: 'Radio 2000', assetPath: '/assets/media-inventory-partners/radio-2000-mono.webp', width: 320, height: 140 },
  { name: '702', assetPath: '/assets/media-inventory-partners/702-mono.webp', width: 320, height: 140 },
  { name: '947 (Highveld)', assetPath: '/assets/media-inventory-partners/947-mono.webp', width: 320, height: 140 },
  { name: 'Kaya 959', assetPath: '/assets/media-inventory-partners/kaya-959-mono.webp', width: 320, height: 140 },
  { name: 'Jacaranda FM', assetPath: '/assets/media-inventory-partners/jacaranda-fm-mono.webp', width: 320, height: 140 },
  { name: 'Algoa FM', assetPath: '/assets/media-inventory-partners/algoa-fm-mono.webp', width: 320, height: 140 },
  { name: 'Jozi FM', assetPath: '/assets/media-inventory-partners/jozi-fm-mono.webp', width: 320, height: 140 },
  { name: 'Smile 90.4FM', assetPath: '/assets/media-inventory-partners/smile-90-4fm-mono.webp', width: 320, height: 140 },
  { name: 'Y', assetPath: '/assets/media-inventory-partners/y-mono.webp', width: 320, height: 140 },
  { name: 'IOL', assetPath: '/assets/media-inventory-partners/iol-mono.webp', width: 320, height: 140 },
  { name: 'Sowetan', assetPath: '/assets/media-inventory-partners/sowetan-mono.webp', width: 320, height: 140 },
  { name: 'Daily Dispatch', assetPath: '/assets/media-inventory-partners/daily-dispatch-mono.webp', width: 320, height: 140 },
  { name: 'Business Day TV', assetPath: '/assets/media-inventory-partners/business-day-tv-mono.webp', width: 320, height: 140 },
  { name: 'eMedia Sales', assetPath: '/assets/media-inventory-partners/emedia-sales-mono.webp', width: 320, height: 140 },
  { name: 'DStv', assetPath: '/assets/media-inventory-partners/dstv-mono.webp', width: 320, height: 140 },
  { name: 'Primedia', assetPath: '/assets/media-inventory-partners/primedia-mono.webp', width: 320, height: 140 },
] as const;

export const campaignSocialPlatforms: readonly MediaInventoryPartner[] = [
  { name: 'Facebook', assetPath: '/assets/media-inventory-partners/facebook-mono.webp', width: 320, height: 140 },
  { name: 'Instagram', assetPath: '/assets/media-inventory-partners/instagram-mono.webp', width: 320, height: 140 },
  { name: 'YouTube', assetPath: '/assets/media-inventory-partners/youtube-mono.webp', width: 320, height: 140 },
  { name: 'LinkedIn', assetPath: '/assets/media-inventory-partners/linkedin-mono.webp', width: 320, height: 140 },
  { name: 'TikTok', assetPath: '/assets/media-inventory-partners/tiktok-mono.webp', width: 320, height: 140 },
  { name: 'X', assetPath: '/assets/media-inventory-partners/x-mono.webp', width: 320, height: 140 },
  { name: 'WhatsApp', assetPath: '/assets/media-inventory-partners/whatsapp-mono.webp', width: 320, height: 140 },
] as const;

const partnerAliases = new Map<string, string>([
  ['947', '947highveld'],
  ['highveld', '947highveld'],
  ['highveldstereo', '947highveld'],
  ['radio702', '702'],
  ['metrofmsa', 'metrofm'],
  ['kayafm', 'kaya959'],
  ['fivefm', '5fm'],
  ['s3', 'sabc3'],
  ['sabc1television', 'sabc1'],
  ['sabc2television', 'sabc2'],
  ['sabc3television', 'sabc3'],
  ['sabc3openup', 'sabc3'],
  ['jacarandafmregional', 'jacarandafm'],
]);

function normalizedPartnerName(name: string) {
  const normalized = name.toLowerCase().replace(/[^a-z0-9]/gu, '');
  return partnerAliases.get(normalized) ?? normalized;
}

export function findMediaInventoryPartner(name: string): MediaInventoryPartner | undefined {
  const normalized = normalizedPartnerName(name);
  return [...mediaInventoryPartners, ...campaignSocialPlatforms].find((partner) => {
    const candidate = normalizedPartnerName(partner.name);
    return candidate === normalized
      || (candidate.length > 3 && normalized.includes(candidate))
      || (normalized.length > 3 && candidate.includes(normalized));
  });
}
