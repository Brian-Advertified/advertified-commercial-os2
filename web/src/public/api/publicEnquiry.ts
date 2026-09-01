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

export const publicEnquiryGateway: PublicEnquiryGateway = {
  available: false,
  unavailableMessage: 'Online enquiries are not connected yet. Email ad@advertified.com to contact Advertified.',
  async submit() {
    return {
      status: 'unavailable',
      message: 'Online enquiries are not connected yet. Email ad@advertified.com to contact Advertified.',
    };
  },
};
