import type { HumanTask } from '../api/schemas';
import { masterDataCodes } from '../generated/master-data-codes';

export function taskTarget(task: HumanTask): string {
  const resources = masterDataCodes.commercialResourceTypes;
  if (task.resourceType === resources.creativeAsset) return `/creative-assets/${task.resourceId}`;
  if (task.resourceType === resources.deliveryProof) return `/delivery-proofs/${task.resourceId}`;
  if (task.resourceType === resources.performanceEvidence) return `/performance-evidence/${task.resourceId}`;
  if (task.resourceType === resources.measurementReport) return `/measurement-reports/${task.resourceId}`;
  if (task.resourceType === resources.campaign) return `/campaigns/${task.resourceId}`;
  if (task.briefId) return `/briefs/${task.briefId}`;
  if (task.opportunityId) return `/opportunities/${task.opportunityId}`;
  return '/tasks';
}
