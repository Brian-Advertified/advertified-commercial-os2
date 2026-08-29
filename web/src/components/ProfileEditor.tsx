import { useState, type FormEvent } from 'react'
import { ApiFailure, api, humanMessage } from '../api/client'
import { profileUpdateSchema, type CurrentUser } from '../api/schemas'
import { notifications } from '../notifications/notifications'
import { ProfileForm, type FieldErrors } from './ProfileForm'

type Props = {
  initialUser: CurrentUser
  tenantId: string
  antiforgeryToken: string
  onUpdated: (user: CurrentUser) => void
}

export function ProfileEditor({ initialUser, tenantId, antiforgeryToken, onUpdated }: Props) {
  const [user, setUser] = useState(initialUser)
  const [displayName, setDisplayName] = useState(initialUser.displayName)
  const [phone, setPhone] = useState(initialUser.phone ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = profileUpdateSchema.safeParse({ displayName, phone })
    if (!parsed.success) {
      const fields = parsed.error.flatten().fieldErrors
      setFieldErrors({ displayName: fields.displayName?.[0], phone: fields.phone?.[0] })
      return
    }

    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const updated = await api.updateProfile(
        tenantId,
        parsed.data,
        user.version,
        antiforgeryToken,
      )
      setUser(updated)
      setDisplayName(updated.displayName)
      setPhone(updated.phone ?? '')
      onUpdated(updated)
      notifications.success('Your profile has been updated.')
    } catch (failure) {
      const message = humanMessage(failure)
      setError(message)
      notifications.failure(message)
      if (failure instanceof ApiFailure && failure.code === 'VERSION_CONFLICT') {
        setFieldErrors({ displayName: 'Refresh your profile before saving again.' })
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <ProfileForm
      user={user}
      displayName={displayName}
      phone={phone}
      saving={saving}
      error={error}
      fieldErrors={fieldErrors}
      onDisplayNameChange={setDisplayName}
      onPhoneChange={setPhone}
      onSubmit={save}
    />
  )
}
