import { useEffect, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { commercialPolicyApi } from '../api/commercial-policy-client'
import type { CommercialPolicy, CommercialPolicyInput }
  from '../api/commercial-policy-schemas'
import { humanMessage } from '../api/client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import { notifications } from '../notifications/notifications'

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
}

const emptyForm: FormState = {
  markup: '', managementFee: '', commission: '', vatStatus: '', vatRate: '',
  pricesIncludeVat: false, currency: '', bookingApprovalThreshold: '',
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
  return <section aria-labelledby="commercial-policy-title">
    <header className="page-heading page-heading-split"><div>
      <p className="eyebrow">Versioned workspace pricing rules</p>
      <h1 id="commercial-policy-title">Commercial policy</h1>
      <p>Set the exact fees, VAT treatment and approval threshold used by commercial calculations.</p>
    </div><span className="status-chip">{policy ? `Version ${policy.versionNumber}` : 'Not configured'}</span></header>
    {!policy && <p className="inline-alert" role="status">
      No policy exists yet. Enter every value below before booking work can proceed.
    </p>}
    <PolicyForm form={form} setForm={setForm} saving={saving} error={error}
      submit={save} />
  </section>
}

function PolicyForm({ form, setForm, saving, error, submit }: {
  form: FormState
  setForm: (value: FormState) => void
  saving: boolean
  error: string | null
  submit: (event: FormEvent<HTMLFormElement>) => Promise<void>
}) {
  const registered = form.vatStatus === masterDataCodes.vatStatuses.registered
  const update = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm({ ...form, [key]: value })
  return <form className="profile-form commercial-policy-form"
    onSubmit={(event) => void submit(event)} noValidate>
    <div className="commercial-policy-grid">
      <PercentField id="markup" label="Markup" value={form.markup}
        update={(value) => update('markup', value)} />
      <PercentField id="management-fee" label="Management fee"
        value={form.managementFee} update={(value) => update('managementFee', value)} />
      <PercentField id="commission" label="Agency commission"
        value={form.commission} update={(value) => update('commission', value)} />
      <SelectField id="vat-status" label="VAT treatment" value={form.vatStatus}
        options={masterDataDefinitions.vatStatuses} update={(value) => setForm({ ...form,
          vatStatus: value, vatRate: value === masterDataCodes.vatStatuses.registered
            ? form.vatRate : '0', pricesIncludeVat: value === masterDataCodes.vatStatuses.registered
              ? form.pricesIncludeVat : false })} />
      <PercentField id="vat-rate" label="VAT rate" value={form.vatRate}
        disabled={!registered} update={(value) => update('vatRate', value)} />
      <SelectField id="currency" label="Policy currency" value={form.currency}
        options={masterDataDefinitions.currencies} update={(value) => update('currency', value)} />
      <div className="field-group field-wide"><label htmlFor="booking-threshold">
        Booking approval threshold ({form.currency || 'currency'})</label>
        <input id="booking-threshold" inputMode="decimal" placeholder="e.g. 50000.00"
          value={form.bookingApprovalThreshold}
          onChange={(event) => update('bookingApprovalThreshold', event.target.value)} />
        <p className="field-note">Bookings at or above this exact amount require the governed approval step.</p>
      </div>
      <label className="checkbox-field field-wide">
        <input type="checkbox" checked={form.pricesIncludeVat} disabled={!registered}
          onChange={(event) => update('pricesIncludeVat', event.target.checked)} />
        Supplied prices already include VAT
      </label>
    </div>
    {error && <div className="inline-alert" role="alert">{error}</div>}
    <div className="form-actions"><button className="primary-button" type="submit"
      disabled={saving}>{saving ? 'Saving…' : 'Save policy version'}</button></div>
  </form>
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
    bookingApprovalThresholdMinor: moneyToMinor(form.bookingApprovalThreshold),
  }
}

function percentToBasisPoints(value: string, maximum: number, label: string) {
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(value.trim())
  if (!match) throw new Error(`Enter ${label} as a positive percentage with at most two decimals.`)
  const result = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'))
  if (!Number.isSafeInteger(result) || result > maximum) throw new Error(`The ${label} is outside the allowed range.`)
  return result
}

function moneyToMinor(value: string) {
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(value.trim())
  if (!match) throw new Error('Enter the booking threshold with at most two decimal places.')
  const result = Number(match[1]) * 100 + Number((match[2] ?? '').padEnd(2, '0'))
  if (!Number.isSafeInteger(result)) throw new Error('The booking threshold is too large.')
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
    bookingApprovalThreshold: minorToMoney(policy.bookingApprovalThresholdMinor),
  }
}

function basisPointsToPercent(value: number) { return (value / 100).toFixed(2) }
function minorToMoney(value: number) { return `${Math.trunc(value / 100)}.${String(value % 100).padStart(2, '0')}` }
