import { CheckCircle2 } from 'lucide-react';
import { useState, type FormEvent } from 'react';

import { masterDataCodes } from '../../generated/master-data-codes';
import type { RegistrationType } from '../data/registrationTypes';
import { submitPublicIntake } from '../api/publicIntake';

const typeCodes: Record<RegistrationType, string> = {
  advertiser: masterDataCodes.publicIntakeTypes.advertiser,
  agency: masterDataCodes.publicIntakeTypes.agency,
  'media-owner': masterDataCodes.publicIntakeTypes.mediaOwner,
  creator: masterDataCodes.publicIntakeTypes.creator,
};

const emptyValues = {
  name: '', email: '', phone: '', organisation: '', website: '', relationship: '', message: '',
};

type Values = typeof emptyValues;
type Errors = Partial<Record<keyof Values, string>>;
type FormState = 'idle' | 'pending' | 'success' | 'error';

export function GovernedOnboardingForm({ type, organisationLabel, relationshipLabel }: {
  type: RegistrationType;
  organisationLabel: string;
  relationshipLabel: string;
}) {
  const [values, setValues] = useState(emptyValues);
  const [errors, setErrors] = useState<Errors>({});
  const [state, setState] = useState<FormState>('idle');
  const [status, setStatus] = useState('');

  const update = (field: keyof Values, value: string) => {
    const next = { ...values, [field]: value };
    setValues(next);
    if (errors[field]) setErrors(validate(next));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const nextErrors = validate(values);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) return;
    setState('pending');
    setStatus('');
    try {
      await submitPublicIntake({
        typeCode: typeCodes[type],
        name: values.name,
        email: values.email,
        phone: values.phone || null,
        organisation: values.organisation,
        website: values.website || null,
        relationship: values.relationship || null,
        message: values.message || null,
      });
      setState('success');
      setStatus('Your registration request has been received. Advertified will verify the organisation and requested access before issuing workspace access.');
    } catch {
      setState('error');
      setStatus('Your registration request could not be submitted. Please try again.');
    }
  };

  return <OnboardingFormView type={type} organisationLabel={organisationLabel}
    relationshipLabel={relationshipLabel} values={values} errors={errors} state={state}
    status={status} update={update} submit={submit} />;
}

function OnboardingFormView(props: {
  type: RegistrationType; organisationLabel: string; relationshipLabel: string;
  values: Values; errors: Errors; state: FormState; status: string;
  update: (field: keyof Values, value: string) => void;
  submit: (event: FormEvent) => Promise<void>;
}) {
  if (props.state === 'success') return <article className="registration-details__form">
    <CheckCircle2 aria-hidden="true" /><span className="eyebrow">REQUEST RECEIVED</span>
    <h2>Registration is under review.</h2><p>{props.status}</p>
  </article>;
  const { values, errors, update } = props;
  return <article className="registration-details__form">
    <header className="registration-details__heading">
      <span className="eyebrow">GOVERNED ONBOARDING</span>
      <h2>Request the right Advertified access.</h2>
      <p>Submitting this form creates a review request only. No account, membership or campaign access is created automatically.</p>
    </header>
    <form className="contact-form public-contact-form" onSubmit={(event) => void props.submit(event)} noValidate>
      <Field label="Your name" name="name" value={values.name} error={errors.name} onChange={(value) => update('name', value)} />
      <Field label="Business email" name="email" type="email" value={values.email} error={errors.email} onChange={(value) => update('email', value)} />
      <Field label="Mobile number" name="phone" value={values.phone} error={errors.phone} onChange={(value) => update('phone', value)} />
      <Field label={props.organisationLabel} name="organisation" value={values.organisation} error={errors.organisation} onChange={(value) => update('organisation', value)} />
      <Field label="Website or public profile" name="website" type="url" value={values.website} error={errors.website} onChange={(value) => update('website', value)} />
      <label className="full public-form-wide" htmlFor={`registration-${props.type}-relationship`}>
        <span>{props.relationshipLabel}</span>
        <textarea id={`registration-${props.type}-relationship`} value={values.relationship} onChange={(event) => update('relationship', event.target.value)} aria-invalid={Boolean(errors.relationship)} />
        {errors.relationship && <small className="public-field-error">{errors.relationship}</small>}
      </label>
      <label className="full public-form-wide" htmlFor={`registration-${props.type}-message`}>
        <span>Anything else we should know?</span>
        <textarea id={`registration-${props.type}-message`} value={values.message} onChange={(event) => update('message', event.target.value)} aria-invalid={Boolean(errors.message)} />
        {errors.message && <small className="public-field-error">{errors.message}</small>}
      </label>
      {props.status && <div className="public-form-status public-form-wide" role="status">{props.status}</div>}
      <button className="btn primary large full public-form-wide" type="submit" disabled={props.state === 'pending'}>
        {props.state === 'pending' ? 'Submitting request…' : 'Submit registration request'}
      </button>
    </form>
  </article>;
}

function Field({ label, name, type = 'text', value, error, onChange }: {
  label: string; name: string; type?: string; value: string; error?: string;
  onChange: (value: string) => void;
}) {
  const id = `registration-${name}`;
  return <label htmlFor={id}><span>{label}</span><input id={id} name={name} type={type} value={value} onChange={(event) => onChange(event.target.value)} aria-invalid={Boolean(error)} />{error && <small className="public-field-error">{error}</small>}</label>;
}

function validate(values: Values): Errors {
  const errors: Errors = {};
  if (values.name.trim().length < 2) errors.name = 'Enter your name.';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/u.test(values.email.trim())) errors.email = 'Enter a valid business email.';
  if (values.phone.trim().length > 50) errors.phone = 'Use a shorter phone number.';
  if (values.organisation.trim().length < 2) errors.organisation = 'Enter the organisation or trading name.';
  if (values.website.trim() && !/^https?:\/\/[^\s]+$/iu.test(values.website.trim())) errors.website = 'Use a complete http or https address.';
  if (values.relationship.trim().length < 10) errors.relationship = 'Describe your relationship or requested access.';
  if (values.relationship.length > 1000) errors.relationship = 'Keep this under 1,000 characters.';
  if (values.message.length > 4000) errors.message = 'Keep this under 4,000 characters.';
  return errors;
}
