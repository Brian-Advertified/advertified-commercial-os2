export function decisionReportFixture(productId: string, productVersionId: string, briefVersionId: string, shortlistVersionId: string) {
  return {
    supplierSafe: false, hasMore: false, nextCursor: null,
    items: [{
      eventId: 'da000000-0000-0000-0000-000000000001',
      previousEventId: 'da000000-0000-0000-0000-000000000002',
      productId, productVersionId, previousProductVersionId: productVersionId,
      productName: 'Synthetic previously selected billboard', isSelected: false,
      wasSelected: true, presentInCurrentShortlist: true,
      decidedAtUtc: '2026-09-08T10:00:00Z', actorId: 'da000000-0000-0000-0000-000000000003',
      reason: 'Changed the anchor to improve verified local coverage.', briefVersionId, shortlistVersionId,
    }],
  }
}
