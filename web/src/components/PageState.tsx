export function LoadingState({ label = 'Loading your workspace' }: { label?: string }) {
  return (
    <div className="page-state" role="status" aria-live="polite">
      <span className="loading-mark" aria-hidden="true" />
      <p>{label}…</p>
    </div>
  )
}

export function MessageState({
  title,
  message,
  action,
}: {
  title: string
  message: string
  action?: React.ReactNode
}) {
  return (
    <section className="message-state" aria-labelledby="message-state-title">
      <span className="message-mark" aria-hidden="true">A</span>
      <h1 id="message-state-title">{title}</h1>
      <p>{message}</p>
      {action}
    </section>
  )
}
