import { z } from 'zod'

export const supplierClaimInvitationSchema = z.object({
  id: z.uuid(), supplierId: z.uuid(), supplierName: z.string(), invitedEmail: z.email(),
  role: z.string(), status: z.string(), expiresAtUtc: z.string(),
  registrationToken: z.string().nullable(), createdBy: z.uuid(), createdAtUtc: z.string(),
  acceptedUserId: z.uuid().nullable(), acceptedAtUtc: z.string().nullable(), version: z.number().int(),
})

export const inventorySupplierLifecycleSchema = z.object({
  id: z.uuid(), name: z.string(), claimStatus: z.string(), currentReleaseId: z.uuid().nullable(),
  currentProductCount: z.number().int().nonnegative(), expiredProductCount: z.number().int().nonnegative(),
  releases: z.array(z.object({
    id: z.uuid(), supplierId: z.uuid(), sourceImportId: z.uuid().nullable(), versionNumber: z.number().int(),
    replacementMode: z.string(), status: z.string(), supersedesReleaseId: z.uuid().nullable(),
    effectiveAtUtc: z.string(), supersededAtUtc: z.string().nullable(),
    productCount: z.number().int().nonnegative(), version: z.number().int(),
  })),
  invitations: z.array(supplierClaimInvitationSchema), version: z.number().int(),
})

export const proposalInventoryImpactSchema = z.object({
  id: z.uuid(), proposalVersionId: z.uuid(), proposalOptionId: z.uuid(), mediaPlanLineId: z.uuid(),
  inventoryTenantId: z.uuid(), supplierId: z.uuid(), oldReleaseId: z.uuid(), replacementReleaseId: z.uuid(),
  oldProductId: z.uuid(), oldProductVersionId: z.uuid(), oldRateId: z.uuid(), oldAvailabilityId: z.uuid().nullable(),
  replacementProductId: z.uuid().nullable(), replacementProductVersionId: z.uuid().nullable(),
  replacementRateId: z.uuid().nullable(), replacementAvailabilityId: z.uuid().nullable(),
  impactType: z.string(), status: z.string(), comparisonJson: z.string(),
  resolvedBy: z.uuid().nullable(), resolvedAtUtc: z.string().nullable(), version: z.number().int(),
})

export type SupplierClaimInvitation = z.infer<typeof supplierClaimInvitationSchema>
export type InventorySupplierLifecycle = z.infer<typeof inventorySupplierLifecycleSchema>
export type ProposalInventoryImpact = z.infer<typeof proposalInventoryImpactSchema>
