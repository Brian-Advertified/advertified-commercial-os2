import { useMemo, useState, type FormEvent } from 'react'
import type {
  InboundMailbox,
  InboundMailboxInput,
} from '../api/email-automation-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { operationalCopy } from '../content/operational-copy'

const providerOptions = [
  { value: masterDataCodes.emailProviders.resend, label: 'Resend' },
  ...(import.meta.env.DEV
    ? [{ value: masterDataCodes.emailProviders.deterministic,
      label: operationalCopy.sandboxMailbox }]
    : []),
]

type SetupValues = {
  address: string
  provider: string
  allowedDomains: string
  autoSendEnabled: boolean
}

export function MailboxSetupForm({ current, ownerUserId, busy, onSubmit, onCancel }: {
  current: InboundMailbox | null
  ownerUserId: string
  busy: boolean
  onSubmit: (configuration: InboundMailboxInput) => Promise<void>
  onCancel?: () => void
}) {
  const [values, setValues] = useState<SetupValues>(() => ({
    address: current?.address ?? '',
    provider: current?.provider ?? providerOptions[0].value,
    allowedDomains: current?.allowedSenderDomains.join(', ') ?? '',
    autoSendEnabled: current?.autoSendEnabled ?? false,
  }))
  const domains = useMemo(() => parseDomains(values.allowedDomains), [values.allowedDomains])
  const valid = values.address.includes('@') && domains.length > 0

  function update(patch: Partial<SetupValues>) {
    setValues(existing => ({ ...existing, ...patch }))
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!valid) return
    await onSubmit({
      address: values.address.trim().toLowerCase(),
      provider: values.provider,
      ownerUserId: current?.ownerUserId ?? ownerUserId,
      defaultClientAccountId: current?.defaultClientAccountId ?? null,
      autoSendEnabled: values.autoSendEnabled,
      allowedSenderDomains: domains,
    })
  }

  return <form className="ooh-mailbox-setup" onSubmit={(event) => void submit(event)}>
    <SetupHeading editing={Boolean(current)} />
    <div className="ooh-setup-grid">
      <label className="field-group">Mailbox address<input type="email" required
        value={values.address} placeholder="proposals@advertified.com"
        onChange={(event) => update({ address: event.target.value })} /></label>
      <label className="field-group">Email provider<select value={values.provider}
        onChange={(event) => update({ provider: event.target.value })}>
        {providerOptions.map((option) => <option key={option.value} value={option.value}>
          {option.label}
        </option>)}</select></label>
      <label className="field-group ooh-domain-field">Allowed sender domains<input required
        value={values.allowedDomains} placeholder="client.co.za, agency.co.za"
        onChange={(event) => update({ allowedDomains: event.target.value })} />
        <small>Only requests from these domains can enter automatic proposal preparation.</small>
      </label>
    </div>
    <AutoSendControl enabled={values.autoSendEnabled}
      update={(autoSendEnabled) => update({ autoSendEnabled })} />
    <div className="ooh-setup-actions">
      {onCancel && <button className="text-action" type="button" onClick={onCancel}>Cancel</button>}
      <button className="primary-button" type="submit" disabled={busy || !valid}>
        {busy ? 'Saving…' : current ? 'Save mailbox' : 'Connect mailbox'}
      </button>
    </div>
  </form>
}

function SetupHeading({ editing }: { editing: boolean }) {
  return <div className="ooh-setup-heading"><div><p className="eyebrow">
    {editing ? 'Mailbox settings' : 'One-time setup'}</p>
    <h2>{editing ? 'Update the proposal mailbox' : 'Connect the proposal mailbox'}</h2>
    <p>Complete requests use the same Brief, STP, planning, inventory and proposal flow. The client is read from each Brief, so no client record is required beforehand.</p></div>
    <span className="ooh-mode-badge">OOH / DOOH only</span></div>
}

function AutoSendControl({ enabled, update }: {
  enabled: boolean
  update: (value: boolean) => void
}) {
  return <label className="ooh-auto-send-control">
    <input type="checkbox" checked={enabled}
      onChange={(event) => update(event.target.checked)} />
    <span><strong>Send complete proposals automatically</strong>
      <small>Unclear, multi-channel, attached or commercially unready requests are held and never sent.</small></span>
  </label>
}

function parseDomains(value: string) {
  return value.split(',').map(item => item.trim().toLowerCase().replace(/^@/, ''))
    .filter((item, index, all) => item.includes('.') && all.indexOf(item) === index)
}
