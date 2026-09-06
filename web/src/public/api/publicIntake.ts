import { z } from 'zod';

export interface PublicIntakeSubmission {
  typeCode: string;
  name: string;
  email: string;
  phone?: string | null;
  organisation: string;
  website?: string | null;
  relationship?: string | null;
  message?: string | null;
}

const publicIntakeViewSchema = z.object({
  id: z.uuid(),
  typeCode: z.string(),
  status: z.string(),
  version: z.number().int().positive(),
});

export type PublicIntakeAccepted = z.infer<typeof publicIntakeViewSchema>;

export async function submitPublicIntake(
  submission: PublicIntakeSubmission,
  signal?: AbortSignal,
): Promise<PublicIntakeAccepted> {
  const response = await fetch('/api/v1/public-intake', {
    method: 'POST',
    credentials: 'same-origin',
    signal,
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(submission),
  });
  const payload: unknown = await response.json().catch(() => null);
  if (!response.ok) throw new Error('PUBLIC_INTAKE_REJECTED');
  const parsed = publicIntakeViewSchema.safeParse(payload);
  if (!parsed.success) throw new Error('PUBLIC_INTAKE_INVALID_RESPONSE');
  return parsed.data;
}
