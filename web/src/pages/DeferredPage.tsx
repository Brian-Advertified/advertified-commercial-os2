import { Navigate } from 'react-router-dom'

export function DeferredPage({ destination }: { destination: 'Tasks' | 'Notifications' }) {
  return (
    <section className="deferred-page" aria-labelledby="deferred-title">
      <p className="eyebrow">Truthful boundary</p>
      <h1 id="deferred-title">{destination} are not available yet</h1>
      <p>
        This area will open when an owning workflow provides real, tenant-safe
        {destination === 'Tasks' ? ' human tasks' : ' notification records'}.
        Advertified does not invent queue entries or counts.
      </p>
      <a className="secondary-button" href="/home">Return home</a>
    </section>
  )
}

export function NotFoundPage() {
  return <Navigate to="/home" replace />
}
