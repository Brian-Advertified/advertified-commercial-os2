import { useEffect, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { commercialPolicyApi } from '../api/commercial-policy-client'
import type { CommercialPolicy, CommercialPolicyInput }
  from '../api/commercial-policy-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import { notifications } from '../notifications/notifications'
import { humanizeCode, majorAmountToMinor, minorAmountToInput } from '../presentation/format'

const administratorRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.agencyAdmin,
])

type FormState = {
  markup: string
  managementFee: string
  commission: string
  vatStatus: string
  vatRate: string
  pricesIncludeVat: boolean
  currency: string
  bookingApprovalThreshold: string
  allowSelfApproval: boolean
}

type PolicyUpdate = <K extends keyof FormState>(key: K, value: FormState[K]) => void

const emptyForm: FormState = {
  markup: '', managementFee: '', commission: '', vatStatus: '', vatRate: '',
  pricesIncludeVat: false, currency: '', bookingApprovalThreshold: '',
  allowSelfApproval: false,
}

export function CommercialPolicyPage() {
  const { session } = useSession()
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!administratorRoles.has(selected.roleCode)) return <MessageState
    title="Commercial settings are not available"
    message="Only an agency or platform administrator can manage this workspace policy." />
  if (!session) return <Navigate to="/sign-in" replace />
  return <CommercialPolicyEditor key={selected.tenantId}
    tenantId={selected.tenantId} antiforgeryToken={session.antiforgeryToken} />
}

function CommercialPolicyEditor({ tenantId, antiforgeryToken }: {
  tenantId: string; antiforgeryToken: string
}) {
  const [policy, setPolicy] = useState<CommercialPolicy | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    void commercialPolicyApi.getCurrent(tenantId).then((current) => {
      if (!active) return
      setPolicy(current)
      setForm(current ? toForm(current) : emptyForm)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    }).finally(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [tenantId])

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    let input: CommercialPolicyInput
    try { input = toInput(form) }
    catch (failure) { setError(failure instanceof Error ? failure.message : 'Review the policy.'); return }
    setSaving(true)
    try {
      const saved = await commercialPolicyApi.save(
        tenantId, input, policy?.version ?? 0, antiforgeryToken)
      setPolicy(saved); setForm(toForm(saved))
      notifications.success(`Commercial policy version ${saved.versionNumber} saved.`)
    } catch (failure) {
      const message = humanMessage(failure)
      setError(message); notifications.failure(message)
    } finally { setSaving(false) }
  }

  if (loading) return <LoadingState label="Loading commercial settings" />
  return <section className="operations-page operations-policy-page" aria-labelledby="commercial-policy-title">
    <header className="operations-command-header"><div>
      <p className="eyebrow">Versioned workspace pricing rules</p>
      <h1 id="commercial-policy-title">Commercial policy</h1>
      <p>Set the exact fees, VAT treatment and approval threshold used by commercial calculations.</p>
    </div><span className="operations-state-label">{policy ? 'Configured' : 'Action required'}</span></header>
    <dl className="operations-context-strip operations-context-four">
      <div><dt>Policy version</dt><dd>{policy ? `Version ${policy.versionNumber}` : 'Not configured'}</dd></div>
      <div><dt>Currency</dt><dd>{form.currency || 'Not selected'}</dd></div>
      <div><dt>VAT treatment</dt><dd>{form.vatStatus ? humanizeCode(form.vatStatus, true) : 'Not selected'}</dd></div>
      <div><dt>Booking approval</dt><dd>{form.bookingApprovalThreshold || 'Not set'}</dd></div>
    </dl>
    {!policy && <p className="inline-alert" role="status">
      No policy exists yet. Enter every value below before booking work can proceed.
    </p>}
    <PolicyForm form={form} setForm={setForm} saving={saving} error={error} submit={save} />
  </section>
}

function PolicyForm({ form, setForm, saving, error, submit }: {
  form: FormState; setForm: (value: FormState) => void; saving: boolean
  error: string | null; submit: (event: FormEvent<HTMLFormElement>) => Promise<void>
}) {
  const registered = form.vatStatus === masterDataCodes.vatStatuses.registered
  const update: PolicyUpdate = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm({ ...form, [key]: value })
  return <form className="operations-policy-form"
    onSubmit={(event) => void submit(event)} noValidate>
    <div className="operations-policy-grid">
      <PricingFields form={form} update={update} />
      <TaxFields form={form} setForm={setForm} update={update} registered={registered} />
      <ApprovalFields form={form} update={update} />
    </div>
    {error && <div className="inline-alert" role="alert">{error}</div>}
    <footer className="operations-form-footer"><p>Saving creates the next immutable policy version.</p>
      <button className="primary-button" type="submit" disabled={saving}>
        {saving ? 'Saving…' : 'Save policy version'}</button></footer>
  </form>
}

function PricingFields({ form, update }: { form: FormState; update: PolicyUpdate }) {
  return <section className="operations-form-section">
    <header><p className="eyebrow">Pricing</p><h2 id="pricing-rules-title">Fees and commission</h2>
      <p>Percentages used in governed commercial calculations.</p></header>
    <div className="operations-field-grid">
      <PercentField id="markup" label="Markup" value={form.markup}
        update={(value) => update('markup', value)} />
      <PercentField id="management-fee" label="Management fee" value={form.managementFee}
        update={(value) => update('managementFee', value)} />
      <PercentField id="commission" label="Agency commission" value={form.commission}
        update={(value) => update('commission', value)} />
    </div>
  </section>
}

function TaxFields({ form, setForm, update, registered }: {
  form: FormState; setForm: (value: FormState) => void; update: PolicyUpdate; registered: boolean
}) {
  function updateVatStatus(value: string) {
    const isRegistered = value === masterDataCodes.vatStatuses.registered
    setForm({ ...form, vatStatus: value, vatRate: isRegistered ? form.vatRate : '0',
      pricesIncludeVat: isRegistered ? form.pricesIncludeVat : false })
  }
  return <section className="operations-form-section">
    <header><p className="eyebrow">Tax</p><h2 id="tax-rules-title">VAT treatment</h2>
      <p>Workspace tax status and supplied-price interpretation.</p></header>
    <div className="operations-field-grid">
      <SelectField id="vat-status" label="VAT treatment" value={form.vatStatus}
        options={masterDataDefinitions.vatStatuses} update={updateVatStatus} />
      <PercentField id="vat-rate" label="VAT rate" value={form.vatRate}
        disabled={!registered} update={(value) => update('vatRate', value)} />
      <label className="checkbox-field operations-checkbox-field">
        <input type="checkbox" checked={form.pricesIncludeVat} disabled={!registered}
          onChange={(event) => update('pricesIncludeVat', event.target.checked)} />
        Supplied prices already include VAT
      </label>
    </div>
  </section>
}

function ApprovalFields({ form, update }: { form: FormState; update: PolicyUpdate }) {
  return <section className="operations-form-section">
    <header><p className="eyebrow">Governance</p><h2 id="approval-rules-title">Booking approval</h2>
      <p>Currency and exact value at which a named approval is required.</p></header>
    <div className="operations-field-grid operations-field-grid-wide">
      <SelectField id="currency" label="Policy currency" value={form.currency}
        options={masterDataDefinitions.currencies} update={(value) => update('currency', value)} />
      <div className="field-group"><label htmlFor="booking-threshold">
        Booking approval threshold ({form.currency || 'currency'})</label>
        <input id="booking-threshold" inputMode="decimal" placeholder="e.g. 50000.00"
          value={form.bookingApprovalThreshold}
          onChange={(event) => update('bookingApprovalThreshold', event.target.value)} />
        <p className="field-note">Bookings at or above this amount require the governed approval step.</p>
      </div>
      <label className="checkbox-field operations-checkbox-field">
        <input type="checkbox" checked={form.allowSelfApproval}
          onChange={(event) => update('allowSelfApproval', event.target.checked)} />
        Allow authorised creators to approve their own Briefs and proposals
      </label>
      <p className="field-note">Leave this off when a different named approver must review commercial work.</p>
    </div>
  </section>
}

function PercentField({ id, label, value, update, disabled = false }: {
  id: string; label: string; value: string; update: (value: string) => void; disabled?: boolean
}) {
  return <div className="field-group"><label htmlFor={id}>{label} (%)</label>
    <input id={id} inputMode="decimal" placeholder="e.g. 15.00" value={value}
      disabled={disabled} onChange={(event) => update(event.target.value)} /></div>
}

function SelectField({ id, label, value, options, update }: {
  id: string; label: string; value: string
  options: ReadonlyArray<{ code: string; displayLabel: string }>
  update: (value: string) => void
}) {
  return <div className="field-group"><label htmlFor={id}>{label}</label>
    <select id={id} value={value} onChange={(event) => update(event.target.value)}>
      <option value="">Select one</option>
      {options.map((option) => <option value={option.code} key={option.code}>
        {option.displayLabel}</option>)}
    </select></div>
}

function toInput(form: FormState): CommercialPolicyInput {
  if (!form.vatStatus || !form.currency) throw new Error('Select the VAT treatment and currency.')
  return {
    markupBasisPoints: percentToBasisPoints(form.markup, 100_000, 'markup'),
    managementFeeBasisPoints: percentToBasisPoints(form.managementFee, 100_000, 'management fee'),
    commissionBasisPoints: percentToBasisPoints(form.commission, 10_000, 'commission'),
    vatStatus: form.vatStatus,
    vatRateBasisPoints: percentToBasisPoints(form.vatRate, 10_000, 'VAT rate'),
    pricesIncludeVat: form.pricesIncludeVat,
    currency: form.currency,
    bookingApprovalThresholdMinor: moneyToMinor(form.bookingApprovalThreshold, form.currency),
    allowSelfApproval: form.allowSelfApproval,
  }
}

function percentToBasisPoints(value: string, maximum: number, label: string) {
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(value.trim())
  if (!match) throw new Error(`Enter ${label} as a positive percentage with at most two decimals.`)
  const result = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'))
  if (!Number.isSafeInteger(result) || result > maximum) throw new Error(`The ${label} is outside the allowed range.`)
  return result
}

function moneyToMinor(value: string, currency: string) {
  const normalized = value.trim()
  if (!/^\d+(?:\.\d+)?$/.test(normalized)) {
    throw new Error('Enter a non-negative booking threshold.')
  }
  const amount = Number(normalized)
  const result = majorAmountToMinor(amount, currency)
  if (!Number.isSafeInteger(result)) throw new Error('The booking threshold is too large.')
  if (Number(minorAmountToInput(result, currency)) !== amount) {
    throw new Error('The booking threshold uses more precision than the selected currency supports.')
  }
  return result
}

function toForm(policy: CommercialPolicy): FormState {
  return {
    markup: basisPointsToPercent(policy.markupBasisPoints),
    managementFee: basisPointsToPercent(policy.managementFeeBasisPoints),
    commission: basisPointsToPercent(policy.commissionBasisPoints),
    vatStatus: policy.vatStatus,
    vatRate: basisPointsToPercent(policy.vatRateBasisPoints),
    pricesIncludeVat: policy.pricesIncludeVat,
    currency: policy.currency,
    bookingApprovalThreshold: minorAmountToInput(
      policy.bookingApprovalThresholdMinor, policy.currency),
    allowSelfApproval: policy.allowSelfApproval,
  }
}

function basisPointsToPercent(value: number) { return (value / 100).toFixed(2) }
