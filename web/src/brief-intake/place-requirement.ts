import { masterDataCodes } from '../generated/master-data-codes.ts'

export type PlaceInput = {
  name: string
  latitude: string
  longitude: string
  radius: string
  source: string
}

export function placeRequirement(input: PlaceInput) {
  const latitude = Number(input.latitude)
  const longitude = Number(input.longitude)
  const radius = Number(input.radius)
  if (!Object.values(input).every(value => value.trim().length > 0)) return null
  if (![latitude, longitude, radius].every(Number.isFinite)) return null
  if (Math.abs(latitude) > 90 || Math.abs(longitude) > 180 || radius <= 0) return null
  return {
    type: masterDataCodes.spatialRequirementTypes.pointRadius,
    priority: masterDataCodes.spatialRequirementPriorities.required,
    label: input.name.trim(),
    geoJson: JSON.stringify({ type: 'Point', coordinates: [longitude, latitude] }),
    radiusMetres: radius,
    coverageThreshold: 0.5,
    sourceLocator: input.source.trim(),
    isVerified: false,
  }
}
