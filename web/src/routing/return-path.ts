const allowedReturnPaths = new Set(['/briefs/new'])

export function publicReturnPath(search: string): string | null {
  const value = new URLSearchParams(search).get('returnTo')
  return value && allowedReturnPaths.has(value) ? value : null
}

export function workspaceSelectionPath(returnTo: string | null): string {
  return returnTo
    ? `/workspaces?returnTo=${encodeURIComponent(returnTo)}`
    : '/workspaces'
}
