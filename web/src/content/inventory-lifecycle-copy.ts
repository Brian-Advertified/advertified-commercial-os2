export const inventoryLifecycleCopy = {
  heading: 'Supplier inventory replacement',
  title: 'Current release and proposed replacement',
  explanation: 'Current inventory changes only after successful publication. Publication rechecks source evidence, authority and current versions.',
  unavailable: 'Supplier release information could not be loaded. The current inventory state has not been confirmed.',
  loading: 'Loading supplier release information…',
  unresolved: 'Resolve the permanent supplier identity before reviewing its current release.',
  currentProducts: 'current products in the supplier’s persisted release',
  approvedCandidates: 'approved source candidates; these are not yet published products',
  impacts: 'Pending-work and proposal impacts have not been calculated in this view. Publication records the actual cutover and required proposal reviews.',
} as const
