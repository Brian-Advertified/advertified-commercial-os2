import { useMemo, useRef, useState, type FormEvent } from 'react';
import '../../styles/public-forms.css';
import { publicEnquiryGateway, type PublicEnquiryKind, type PublicEnquirySubmission } from '../api/publicEnquiry';

type FieldErrors = Partial<Record<keyof PublicEnquirySubmission, string>>;
type FormState = 'idle' | 'pending' | 'success' | 'error';

const emptyValues = { name: '', email: '', organisation: '', message: '' };

export function PublicContactForm({ kind }: { kind: PublicEnquiryKind }) {
  const [values, setValues] = useState(emptyValues);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [state, setState] = useState<FormState>('idle');
  const [statusMessage, setStatusMessage] = useState(publicEnquiryGateway.unavailableMessage ?? '');
  const controller = useRef<AbortController | null>(null);
  const campaignEnquiry = kind === 'campaign-enquiry';
  const title = campaignEnquiry ? 'Prepare your campaign conversation' : 'Prepare your enquiry';
  const messageLabel = campaignEnquiry ? 'Business challenge and desired outcome' : 'How can Advertified help?';
  const disabled = !publicEnquiryGateway.available || state === 'pending';
  const validation = useMemo(() => validate(values), [values]);

  const update = (field: keyof typeof values, value: string) => {
    const next = { ...values, [field]: value };
    setValues(next);
    if (errors[field]) setErrors(validate(next));
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const nextErrors = validate(values);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length || !publicEnquiryGateway.available) return;
    controller.current?.abort();
    controller.current = new AbortController();
    setState('pending');
    const result = await publicEnquiryGateway.submit({ kind, ...values }, controller.current.signal);
    if (result.status === 'accepted') {
      setState('success');
      setStatusMessage(result.message);
    } else if (result.status === 'validation_failed') {
      setState('error');
      setErrors(result.fieldErrors);
      setStatusMessage('Review the highlighted fields and try again.');
    } else {
      setState('error');
      setStatusMessage(result.message);
    }
  };

  return (
    <section className="public-form-shell" aria-labelledby={`${kind}-form-title`}>
      <h2 className="sr-only" id={`${kind}-form-title`}>{title}</h2>
      <form className="contact-form public-contact-form" onSubmit={(event) => void submit(event)} noValidate>
        <FormField label="Name" name="name" value={values.name} error={errors.name} onChange={(value) => update('name', value)} onBlur={() => setErrors(validation)} />
        <FormField label="Email address" name="email" type="email" value={values.email} error={errors.email} onChange={(value) => update('email', value)} onBlur={() => setErrors(validation)} />
        <FormField label="Organisation" name="organisation" value={values.organisation} error={errors.organisation} onChange={(value) => update('organisation', value)} onBlur={() => setErrors(validation)} />
        <label className="full public-form-wide" htmlFor={`${kind}-message`}>
          <span>{messageLabel}</span>
          <textarea id={`${kind}-message`} name="message" value={values.message} onChange={(event) => update('message', event.target.value)} onBlur={() => setErrors(validation)} aria-invalid={Boolean(errors.message)} aria-describedby={errors.message ? `${kind}-message-error` : undefined} />
          {errors.message && <small id={`${kind}-message-error`} className="public-field-error">{errors.message}</small>}
        </label>
        {statusMessage && <div className="public-form-status public-form-wide" role="status" aria-live="polite">{statusMessage}</div>}
        <button className="btn primary large full public-form-wide" type="submit" disabled={disabled}>
          {state === 'pending' ? 'Preparing email…' : publicEnquiryGateway.available ? 'Email Advertified' : 'Online enquiries unavailable'}
        </button>
      </form>
    </section>
  );
}

function FormField({ label, name, type = 'text', value, error, onChange, onBlur }: { label: string; name: string; type?: string; value: string; error?: string; onChange: (value: string) => void; onBlur: () => void }) {
  const id = `public-${name}`;
  return <label htmlFor={id}><span>{label}</span><input id={id} name={name} type={type} value={value} onChange={(event) => onChange(event.target.value)} onBlur={onBlur} aria-invalid={Boolean(error)} aria-describedby={error ? `${id}-error` : undefined} />{error && <small id={`${id}-error`} className="public-field-error">{error}</small>}</label>;
}

function validate(values: typeof emptyValues): FieldErrors {
  const errors: FieldErrors = {};
  if (values.name.trim().length < 2) errors.name = 'Enter your name.';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/u.test(values.email.trim())) errors.email = 'Enter a valid email address.';
  if (values.organisation.trim().length < 2) errors.organisation = 'Enter your organisation.';
  if (values.message.trim().length < 20) errors.message = 'Provide at least 20 characters of context.';
  return errors;
}
