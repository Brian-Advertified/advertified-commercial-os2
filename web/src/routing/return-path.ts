const allowedReturnPaths = new Set(['/briefs/new'])
const supplierClaimPath = /^\/supplier-claim\/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/iu

export function publicReturnPath(search: string): string | null {
  const value = new URLSearchParams(search).get('returnTo')
  if (!value || value.length > 300 || value.includes('\\') || value.startsWith('//')) return null
  return allowedReturnPaths.has(value) || supplierClaimPath.test(value) ? value : null
}

export function workspaceSelectionPath(returnTo: string | null): string {
  if (returnTo && supplierClaimPath.test(returnTo)) return returnTo
  return returnTo
    ? `/workspaces?returnTo=${encodeURIComponent(returnTo)}`
    : '/workspaces'
}
