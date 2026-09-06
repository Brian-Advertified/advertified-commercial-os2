import { masterDataCodes } from '../../generated/master-data-codes';
import { submitPublicIntake } from './publicIntake';

export type PublicEnquiryKind = 'general-enquiry' | 'campaign-enquiry';

export interface PublicEnquirySubmission {
  kind: PublicEnquiryKind;
  name: string;
  email: string;
  organisation: string;
  message: string;
}

export type PublicEnquiryResult =
  | { status: 'accepted'; message: string }
  | { status: 'validation_failed'; fieldErrors: Partial<Record<keyof PublicEnquirySubmission, string>> }
  | { status: 'failed'; message: string }
  | { status: 'unavailable'; message: string };

export interface PublicEnquiryGateway {
  readonly available: boolean;
  readonly unavailableMessage: string | null;
  submit: (submission: PublicEnquirySubmission, signal: AbortSignal) => Promise<PublicEnquiryResult>;
}

const typeCodes: Record<PublicEnquiryKind, string> = {
  'general-enquiry': masterDataCodes.publicIntakeTypes.generalEnquiry,
  'campaign-enquiry': masterDataCodes.publicIntakeTypes.campaignEnquiry,
};

export const publicEnquiryGateway: PublicEnquiryGateway = {
  available: true,
  unavailableMessage: null,
  async submit(submission, signal) {
    try {
      await submitPublicIntake({
        typeCode: typeCodes[submission.kind],
        name: submission.name,
        email: submission.email,
        organisation: submission.organisation,
        message: submission.message,
      }, signal);
      return {
        status: 'accepted',
        message: 'Thank you. Advertified has received your enquiry and will review it.',
      };
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        return { status: 'failed', message: 'The request was cancelled.' };
      }
      return { status: 'failed', message: 'Advertified could not connect. Please try again.' };
    }
  },
};
