export interface PublicMediaOwner {
  id: string;
  name: string;
  logoUrl: string | null;
}

export interface PublicInventoryChannel {
  channel: string;
  count: number;
  owners: PublicMediaOwner[];
}

export interface PublicInventorySummary {
  totalCount: number;
  channels: PublicInventoryChannel[];
}

export async function getPublicInventorySummary(signal?: AbortSignal): Promise<PublicInventorySummary> {
  const response = await fetch('/api/v1/public/inventory-summary', { signal });
  if (!response.ok) throw new Error(`Inventory summary failed with ${response.status}`);
  return response.json() as Promise<PublicInventorySummary>;
}

export function publicInventoryAssetUrl(reference: string | null): string | null {
  if (!reference?.startsWith('/public/')) return null;
  return `/api/v1${reference}`;
}
