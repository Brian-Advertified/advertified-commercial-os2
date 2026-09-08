import { useEffect, useMemo, useState } from 'react'
import { humanMessage } from '../api/client'
import { proposalApi } from '../api/proposal-client'
import type { Proposal, ProposalBrandAsset } from '../api/proposal-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'

type Props = {
  tenantId: string
  proposal: Proposal
  token: string
  busy: boolean
  onConfigure: (input: {
    agencyBrandAssetId: string | null
    clientBrandAssetId: string | null
    primaryColour: string | null
    secondaryColour: string | null
  }) => Promise<void>
  onApproveUnbranded: (reason: string) => Promise<void>
}

export function ProposalBrandingPanel(props: Props) {
  const [assets, setAssets] = useState<ProposalBrandAsset[]>([])
  const [error, setError] = useState<string | null>(null)
  const [assetBusy, setAssetBusy] = useState(false)
  const draft = props.proposal.status === masterDataCodes.lifecycleStatuses.draft
  useEffect(() => {
    let active = true
    void proposalApi.listBrandAssets(props.tenantId, props.proposal.id)
      .then(items => { if (active) setAssets(items) })
      .catch(failure => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [props.tenantId, props.proposal.id])
  const agencyAssets = useMemo(() => assets.filter(item => item.clientAccountId === null), [assets])
  const clientAssets = useMemo(() => assets.filter(item => item.clientAccountId !== null), [assets])

  async function assetAction(action: () => Promise<ProposalBrandAsset>) {
    setAssetBusy(true); setError(null)
    try {
      const changed = await action()
      setAssets(current => [changed, ...current.filter(item => item.id !== changed.id)])
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setAssetBusy(false) }
  }

  return <section className="proposal-section" id="proposal-branding"
    aria-labelledby="proposal-branding-title">
    <div className="proposal-section-heading"><div><p className="eyebrow">Governed branding</p>
      <h2 id="proposal-branding-title">Agency and client identity</h2></div>
      <span className="status-chip">{humanizeCode(props.proposal.branding.status, true)}</span>
    </div>
    <p>Prepared by <strong>{props.proposal.branding.agencyName}</strong> for{' '}
      <strong>{props.proposal.branding.clientBrandName}</strong>. Uploaded assets retain their
      source, uploader, content hash and approval decision.</p>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {draft ? <BrandingEditor {...props} agencyAssets={agencyAssets} clientAssets={clientAssets}
      assetBusy={assetBusy} onAssetAction={assetAction} /> :
      <BrandingSummary proposal={props.proposal} />}
  </section>
}

function BrandingSummary({ proposal }: { proposal: Proposal }) {
  if (proposal.branding.status === 'UNBRANDED_AUTHORISED') {
    return <div className="inline-alert" role="status">
      Unbranded delivery was explicitly authorised: {proposal.branding.unbrandedApprovalReason}
    </div>
  }
  return <dl className="proposal-record-metrics">
    <div><dt>Agency asset</dt><dd>{proposal.branding.agencyAsset?.label ?? 'Outstanding'}</dd></div>
    <div><dt>Client asset</dt><dd>{proposal.branding.clientAsset?.label ?? 'Outstanding'}</dd></div>
  </dl>
}

type EditorProps = Props & {
  agencyAssets: ProposalBrandAsset[]
  clientAssets: ProposalBrandAsset[]
  assetBusy: boolean
  onAssetAction: (action: () => Promise<ProposalBrandAsset>) => Promise<void>
}

function BrandingEditor(props: EditorProps) {
  const [agencyId, setAgencyId] = useState(props.proposal.branding.agencyAsset?.id ?? '')
  const [clientId, setClientId] = useState(props.proposal.branding.clientAsset?.id ?? '')
  const [primary, setPrimary] = useState(props.proposal.branding.primaryColour ?? '#5C2EF2')
  const [secondary, setSecondary] = useState(props.proposal.branding.secondaryColour ?? '')
  const [reason, setReason] = useState('')
  return <div className="proposal-branding-editor">
    <div className="proposal-options-grid">
      <BrandAssetControl title="Agency logo" clientAsset={false} assets={props.agencyAssets}
        selected={agencyId} setSelected={setAgencyId} {...props} />
      <BrandAssetControl title="Client logo" clientAsset assets={props.clientAssets}
        selected={clientId} setSelected={setClientId} {...props} />
    </div>
    <div className="proposal-share-control">
      <label>Primary colour <input type="text" value={primary} pattern="#[0-9A-Fa-f]{6}"
        onChange={event => setPrimary(event.target.value)} placeholder="#5C2EF2" /></label>
      <label>Secondary colour <input type="text" value={secondary} pattern="#[0-9A-Fa-f]{6}"
        onChange={event => setSecondary(event.target.value)} placeholder="Optional" /></label>
      <button type="button" className="primary-button" disabled={props.busy || props.assetBusy}
        onClick={() => void props.onConfigure({
          agencyBrandAssetId: agencyId || null,
          clientBrandAssetId: clientId || null,
          primaryColour: primary || null,
          secondaryColour: secondary || null,
        })}>Save proposal branding</button>
    </div>
    <div className="proposal-share-control">
      <label>Reason to proceed without approved logos
        <textarea value={reason} maxLength={1000} onChange={event => setReason(event.target.value)}
          placeholder="Explain why this proposal may be delivered unbranded." /></label>
      <button type="button" className="secondary-button"
        disabled={props.busy || props.assetBusy || !reason.trim()}
        onClick={() => void props.onApproveUnbranded(reason.trim())}>
        Authorise unbranded proposal</button>
    </div>
  </div>
}

function BrandAssetControl(props: EditorProps & {
  title: string
  clientAsset: boolean
  assets: ProposalBrandAsset[]
  selected: string
  setSelected: (value: string) => void
}) {
  const [label, setLabel] = useState('')
  const [source, setSource] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const selected = props.assets.find(item => item.id === props.selected)
  return <article className="proposal-option-card"><h3>{props.title}</h3>
    <label>Approved or pending asset
      <select value={props.selected} onChange={event => props.setSelected(event.target.value)}>
        <option value="">No asset selected</option>
        {props.assets.map(asset => <option key={asset.id} value={asset.id}>
          {asset.label} · {asset.approvedAtUtc ? 'approved' : 'pending approval'}
        </option>)}
      </select></label>
    {selected && !selected.approvedAtUtc && <button type="button" className="secondary-button"
      disabled={props.assetBusy} onClick={() => void props.onAssetAction(() =>
        proposalApi.approveBrandAsset(props.tenantId, selected, props.token))}>
      Approve rights and use</button>}
    <label>Asset label <input value={label} maxLength={200}
      onChange={event => setLabel(event.target.value)} /></label>
    <label>Source and rights reference <input value={source} maxLength={1000}
      onChange={event => setSource(event.target.value)} /></label>
    <label>JPEG logo, up to 2 MB <input type="file" accept="image/jpeg"
      onChange={event => setFile(event.target.files?.[0] ?? null)} /></label>
    <button type="button" className="secondary-button"
      disabled={props.assetBusy || !file || !label.trim() || !source.trim()}
      onClick={() => file && void props.onAssetAction(() => proposalApi.uploadBrandAsset(
        props.tenantId, props.proposal.id,
        { clientAsset: props.clientAsset, label: label.trim(),
          sourceReference: source.trim(), document: file }, props.token))}>
      Upload governed asset</button>
  </article>
}
