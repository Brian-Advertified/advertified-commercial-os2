import { useState, type FormEvent } from 'react'
import { campaignApi } from '../api/campaign-client'
import type { Campaign, CreativeRequirement } from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import { masterDataDefinitions } from '../generated/master-data-codes'
import { formatDateTime, formatMiB, humanizeCode } from '../presentation/format'
import type { CampaignActionRunner } from './campaign-types'

type CreativeReview = NonNullable<
  NonNullable<CreativeRequirement['asset']>['currentVersion']['brandReview']
>

type Props = {
  tenantId: string
  token: string
  campaign: Campaign
  requirement: CreativeRequirement
  busy: boolean
  canUpload: boolean
  canBrandReview: boolean
  run: CampaignActionRunner
}

export function CreativeAssetCard(props: Props) {
  const asset = props.requirement.asset
  return <article className="creative-requirement-card">
    <CreativeRequirementHeader requirement={props.requirement} />
    <CreativeSpecification requirement={props.requirement} />
    {!asset && props.canUpload && <CreativeUploadForm {...props} />}
    {!asset && !props.canUpload && <WaitingCopy />}
    {asset && <>
      <CreativeVersionSummary requirement={props.requirement} />
      <CreativeReviewSummary requirement={props.requirement} />
      {props.canUpload && <CreativeReplacementForm {...props} />}
      {props.canBrandReview && !asset.currentVersion.brandReview &&
        <BrandReviewForm {...props} />}
    </>}
  </article>
}

function CreativeRequirementHeader({ requirement }: { requirement: CreativeRequirement }) {
  return <header><span><Icon name="proposal" /></span><div><small>{humanizeCode(requirement.channel, true)}</small>
    <h3>{requirement.formatCode}</h3><p>{requirement.instructions}</p></div>
    <span className={`status-chip ${requirement.asset ? 'status-positive' : 'status-warning'}`}>
      {requirement.asset ? 'Asset supplied' : 'Asset required'}</span></header>
}

function CreativeSpecification({ requirement }: { requirement: CreativeRequirement }) {
  return <dl className="creative-specification"><div><dt>Dimensions</dt><dd>{requirement.width} × {requirement.height}</dd></div>
    <div><dt>File type</dt><dd>{requirement.requiredMediaType}</dd></div>
    <div><dt>Maximum size</dt><dd>{formatMiB(requirement.maximumBytes)}</dd></div>
    <div><dt>Flight</dt><dd>{requirement.flightStart} – {requirement.flightEnd}</dd></div></dl>
}

function CreativeVersionSummary({ requirement }: { requirement: CreativeRequirement }) {
  const version = requirement.asset!.currentVersion
  return <section className="creative-current-version"><div><span>Current production file</span>
    <strong>{version.fileName}</strong><small>Version {version.versionNumber} · {formatMiB(version.sizeBytes)} · uploaded {formatDateTime(version.createdAtUtc)}</small></div>
    <div><span>Approved copy</span><p>{version.approvedCopy}</p></div></section>
}

function CreativeReviewSummary({ requirement }: { requirement: CreativeRequirement }) {
  const version = requirement.asset!.currentVersion
  return <div className="creative-review-grid">
    <ReviewState label="Brand, legal and rights" review={version.brandReview} />
    <ReviewState label="Supplier technical review" review={version.supplierReview} />
  </div>
}

function ReviewState({ label, review }: {
  label: string
  review: CreativeReview | null
}) {
  return <div><span>{label}</span><strong>{review ? humanizeCode(review.decision, true) : 'Awaiting review'}</strong>
    <small>{review?.reason ?? 'The exact current file version must be reviewed.'}</small></div>
}

function CreativeUploadForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const file = values.get('file')
    const copy = String(values.get('approvedCopy') ?? '').trim()
    const message = fileError(file, props.requirement)
    if (!copy || message) {
      setError(message ?? 'Enter the exact approved copy for this format.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.createCreativeAsset(
        props.tenantId, props.campaign, props.requirement.id, copy, file as File, props.token),
      'The production file was retained as the current creative version.',
    )
  }
  return <form className="creative-action-form" onSubmit={submit}>
    <h4>Supply the production file</h4>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <label className="field-group">Exact approved copy
      <textarea name="approvedCopy" required maxLength={5000} rows={3} /></label>
    <label className="field-group">Production file
      <input name="file" type="file" required accept={props.requirement.requiredMediaType} /></label>
    <button className="primary-button" disabled={props.busy}>Upload production file</button>
  </form>
}

function CreativeReplacementForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const file = values.get('replacementFile')
    const copy = String(values.get('replacementCopy') ?? '').trim()
    const message = fileError(file, props.requirement)
    if (!copy || message) {
      setError(message ?? 'Enter the approved copy for the replacement version.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.uploadCreativeVersion(
        props.tenantId, props.campaign.id, props.requirement.asset!, copy,
        file as File, props.token),
      'A new creative version was retained and previous reviews were invalidated.',
    )
  }
  return <details className="creative-replacement"><summary>Replace the current file</summary>
    <form className="creative-action-form" onSubmit={submit}>
      {error && <p className="inline-alert" role="alert">{error}</p>}
      <label className="field-group">Exact approved copy
        <textarea name="replacementCopy" required maxLength={5000} rows={3}
          defaultValue={props.requirement.asset!.currentVersion.approvedCopy} /></label>
      <label className="field-group">Replacement file
        <input name="replacementFile" type="file" required
          accept={props.requirement.requiredMediaType} /></label>
      <button className="secondary-button" disabled={props.busy}>Create replacement version</button>
    </form>
  </details>
}

function BrandReviewForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const submitter = (event.nativeEvent as SubmitEvent).submitter
    const approved = submitter instanceof HTMLButtonElement && submitter.value === 'approve'
    const rightsStatus = String(values.get('rightsStatus') ?? '')
    const evidenceReference = String(values.get('evidenceReference') ?? '').trim()
    const reason = String(values.get('reason') ?? '').trim()
    if (!rightsStatus || !evidenceReference || !reason) {
      setError('Record the rights state, evidence reference and review reason.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.reviewCreativeBrand(
        props.tenantId, props.campaign.id, props.requirement.asset!, approved,
        rightsStatus, evidenceReference, reason, props.token),
      approved ? 'Brand, legal and rights review was approved.' : 'The creative version was rejected for correction.',
    )
  }
  return <form className="creative-action-form" onSubmit={submit}>
    <h4>Brand, legal and rights review</h4>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div className="creative-review-form-grid"><label className="field-group">Rights state
      <select name="rightsStatus" required defaultValue="">
        <option value="" disabled>Choose rights state</option>
        {masterDataDefinitions.assetRightsStatuses.map(item =>
          <option value={item.code} key={item.code}>{item.displayLabel}</option>)}
      </select></label>
      <label className="field-group">Evidence reference
        <input name="evidenceReference" required maxLength={1000} /></label></div>
    <label className="field-group">Review reason
      <textarea name="reason" required maxLength={1000} rows={3} /></label>
    <div className="creative-review-actions"><button className="secondary-button" name="decision"
      value="reject" disabled={props.busy}>Reject current version</button>
      <button className="primary-button" name="decision" value="approve"
        disabled={props.busy}>Approve current version</button></div>
  </form>
}

function WaitingCopy() {
  return <div className="creative-waiting"><Icon name="tasks" /><span>
    <strong>Production file not supplied</strong><small>An authorised campaign operator must upload the exact booked-format artwork.</small></span></div>
}

function fileError(value: FormDataEntryValue | null, requirement: CreativeRequirement) {
  if (!(value instanceof File) || value.size === 0) return 'Choose the production file.'
  if (value.size > requirement.maximumBytes) return `The file must not exceed ${formatMiB(requirement.maximumBytes)}.`
  if (value.type !== requirement.requiredMediaType) return `Choose a ${requirement.requiredMediaType} file.`
  return null
}
