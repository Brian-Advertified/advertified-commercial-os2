import type { FormEvent } from 'react'
import type { CurrentUser } from '../api/schemas'

export type FieldErrors = { displayName?: string; phone?: string }

type Props = {
  user: CurrentUser
  displayName: string
  phone: string
  saving: boolean
  error: string | null
  fieldErrors: FieldErrors
  onDisplayNameChange: (value: string) => void
  onPhoneChange: (value: string) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => Promise<void>
}

export function ProfileForm(props: Props) {
  return (
    <div className="profile-layout">
      <aside className="profile-summary">
        <span className="profile-avatar" aria-hidden="true">{props.user.displayName.charAt(0)}</span>
        <h2>{props.user.displayName}</h2>
        <p>{props.user.email}</p>
        <span className="status-chip">{props.user.mfaEnabled ? 'MFA enabled' : 'Local identity'}</span>
      </aside>
      <form className="profile-form" onSubmit={(event) => void props.onSubmit(event)} noValidate>
        <div className="field-group">
          <label htmlFor="display-name">Display name</label>
          <input
            id="display-name"
            value={props.displayName}
            onChange={(event) => props.onDisplayNameChange(event.target.value)}
            aria-invalid={Boolean(props.fieldErrors.displayName)}
            aria-describedby={props.fieldErrors.displayName ? 'display-name-error' : undefined}
          />
          {props.fieldErrors.displayName && <p className="field-error" id="display-name-error">{props.fieldErrors.displayName}</p>}
        </div>
        <div className="field-group">
          <label htmlFor="email">Email</label>
          <input id="email" value={props.user.email} disabled aria-describedby="email-note" />
          <p className="field-note" id="email-note">Identity email changes are not available from this profile.</p>
        </div>
        <div className="field-group">
          <label htmlFor="phone">Phone</label>
          <input
            id="phone"
            value={props.phone}
            onChange={(event) => props.onPhoneChange(event.target.value)}
            aria-invalid={Boolean(props.fieldErrors.phone)}
            aria-describedby={props.fieldErrors.phone ? 'phone-error' : undefined}
          />
          {props.fieldErrors.phone && <p className="field-error" id="phone-error">{props.fieldErrors.phone}</p>}
        </div>
        {props.error && <div className="inline-alert" role="alert">{props.error}</div>}
        <div className="form-actions">
          <button className="primary-button" type="submit" disabled={props.saving}>
            {props.saving ? 'Saving…' : 'Save profile'}
          </button>
        </div>
      </form>
    </div>
  )
}
