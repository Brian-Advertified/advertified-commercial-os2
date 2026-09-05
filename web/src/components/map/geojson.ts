export type Geometry = {
  type: 'Point' | 'MultiPoint' | 'LineString' | 'MultiLineString' | 'Polygon' | 'MultiPolygon'
  coordinates: unknown[]
}
export type GeometryBudget = { remainingPoints: number }
const maximumGeometryTextLength = 1_000_000

export function parseGeometry(value: string): Geometry | null {
  if (value.length > maximumGeometryTextLength) return null
  try {
    const parsed: unknown = JSON.parse(value)
    return validateGeometry(isRecord(parsed) && parsed.type === 'Feature' ? parsed.geometry : parsed)
  } catch {
    return null
  }
}

export function validateGeometry(
  value: unknown, budget: GeometryBudget = { remainingPoints: 10_000 },
): Geometry | null {
  if (!isRecord(value)) return null
  const coordinates = value.coordinates
  const point = (item: unknown) => validPoint(item, budget)
  const line = (item: unknown) => validArray(item, 2, point)
  const ring = (item: unknown) => validRing(item, point)
  const polygon = (item: unknown) => validArray(item, 1, ring)
  let valid: boolean
  switch (value.type) {
    case 'Point': valid = point(coordinates); break
    case 'MultiPoint': valid = validArray(coordinates, 1, point); break
    case 'LineString': valid = line(coordinates); break
    case 'MultiLineString': valid = validArray(coordinates, 1, line); break
    case 'Polygon': valid = polygon(coordinates); break
    case 'MultiPolygon': valid = validArray(coordinates, 1, polygon); break
    default: return null
  }
  return valid ? { type: value.type, coordinates: structuredClone(coordinates) as unknown[] } : null
}

function validPoint(value: unknown, budget: GeometryBudget): boolean {
  if (!Array.isArray(value) || value.length !== 2 || budget.remainingPoints <= 0) return false
  const [longitude, latitude] = value
  if (typeof longitude !== 'number' || typeof latitude !== 'number' ||
      !Number.isFinite(longitude) || !Number.isFinite(latitude) ||
      Math.abs(longitude) > 180 || Math.abs(latitude) > 90) return false
  budget.remainingPoints -= 1
  return true
}

function validArray(value: unknown, minimum: number, validate: (item: unknown) => boolean): boolean {
  return Array.isArray(value) && value.length >= minimum && value.length <= 10_000 &&
    value.every(validate)
}

function validRing(value: unknown, point: (item: unknown) => boolean): boolean {
  if (!validArray(value, 4, point)) return false
  const ring = value as number[][]
  return ring[0][0] === ring[ring.length - 1][0] && ring[0][1] === ring[ring.length - 1][1]
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
}
