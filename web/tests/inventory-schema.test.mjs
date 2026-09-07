import assert from 'node:assert/strict'
import test from 'node:test'
import {
  inventoryCommercialTermsSchema,
  inventorySpatialSchema,
} from '../src/api/inventory-schemas.ts'

test('legacy null commercial and spatial lists normalize to empty lists', () => {
  const commercial = inventoryCommercialTermsSchema.parse({
    vatTreatment: null, rateValidFrom: null, rateValidTo: null,
    productionCostMinor: null, installationCostMinor: null, minimumOrder: null,
    billingDays: null, discountTerms: null, inclusions: null, exclusions: null,
    conditions: null, bookingLeadTimeDays: null, bookingDeadline: null,
    materialDeadline: null, cancellationTerms: null,
  })
  const spatial = inventorySpatialSchema.parse({
    country: null, province: null, municipality: null, locality: null, venue: null,
    road: null, route: null, trafficDirection: null, facingBearingDegrees: null,
    pointsOfInterest: null, coverageGeoJson: null, catchmentGeoJson: null,
    routeGeoJson: null, directionGeoJson: null,
  })

  assert.deepEqual(commercial.inclusions, [])
  assert.deepEqual(commercial.exclusions, [])
  assert.deepEqual(commercial.conditions, [])
  assert.deepEqual(spatial.pointsOfInterest, [])
})
