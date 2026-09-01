import { expect, test, type Page } from '@playwright/test';
import { installCampaignDeliveryApi } from './support/campaign-delivery-api';
import { deliveryIds } from './support/campaign-delivery-data';

const pdf = { name: 'document.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\n%fixture') };
const png = { name: 'image.png', mimeType: 'image/png', buffer: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10, 1]) };
const csv = { name: 'performance.csv', mimeType: 'text/csv', buffer: Buffer.from('metric,value\nconversions,42\n') };

test.setTimeout(120_000);

test.beforeEach(async ({ page }) => {
  await page.addInitScript((tenantId) => {
    sessionStorage.setItem('advertified.workspace', JSON.stringify({ tenantId }));
  }, deliveryIds.tenant);
  await installCampaignDeliveryApi(page);
});

test('accepted proposal reaches approved client measurement through one campaign workspace', async ({ page }) => {
  await completeFunding(page);
  await completeCreativeReadiness(page);
  await completeDeliveryProof(page);
  await completeMeasurement(page);
  await expect(page.getByText('The report retains all sourced facts and limitations.')).toBeVisible();
});

async function completeFunding(page: Page) {
  const query = new URLSearchParams({
    proposalVersionId: deliveryIds.proposal,
    proposalOptionId: deliveryIds.option,
    amountMinor: '10000000',
    currency: 'ZAR',
  });
  await page.goto(`/funding?${query}`);
  await expect(page.getByRole('heading', {
    name: 'Turn an accepted proposal into accountable funding.',
  })).toBeVisible();
  await page.getByLabel('Purchase order number').fill('PO-DELIVERY-001');
  await page.getByLabel('Signed purchase order').setInputFiles(pdf);
  await page.getByRole('button', { name: 'Submit for review' }).click();
  await page.getByLabel('Reconciliation reason').fill('The signed PO matches the selected option.');
  await page.getByRole('button', { name: 'Approve reconciled PO' }).click();
  await page.getByLabel('Invoice number').fill('INV-DELIVERY-001');
  await page.getByRole('button', { name: 'Issue invoice' }).click();
  await page.getByRole('button', { name: 'Start payment record' }).click();
  await page.getByLabel('Bank reference').fill('BANK-REF-001');
  await page.getByLabel('Receipt evidence').setInputFiles(pdf);
  await page.getByLabel('Reconciliation reason').fill('Receipt reconciled to the invoice.');
  await page.getByRole('button', { name: 'Confirm reconciled payment' }).click();
  await page.getByRole('link', { name: 'Open campaigns' }).click();
  await page.getByRole('link', { name: /Gauteng Growth Campaign/ }).click();
}

async function completeCreativeReadiness(page: Page) {
  await page.getByLabel('Confirmation reason')
    .fill('Every media-plan line has one exact confirmed Booking.');
  await page.getByRole('button', { name: 'Confirm booking coverage' }).click();
  await fillCreativeRequirement(page);
  await page.getByRole('button', { name: 'Request production creative' }).click();
  await page.getByLabel('Exact approved copy').fill('Move from interest to a clear next step.');
  await page.getByLabel('Production file').setInputFiles(png);
  await page.getByRole('button', { name: 'Upload production file' }).click();
  await page.getByLabel('Rights state').selectOption('APPROVED');
  await page.getByLabel('Evidence reference').fill('brand-review:001');
  await page.getByLabel('Review reason')
    .fill('Brand, legal and rights checks passed for this exact file.');
  await page.getByRole('button', { name: 'Approve current version' }).click();
  await approveSupplierCreative(page);
  await page.goto(`/campaigns/${deliveryIds.campaign}`);
  await page.getByLabel('Approval reason')
    .fill('The current file has approved brand and supplier reviews.');
  await page.getByRole('button', { name: 'Approve creative readiness' }).click();
}

async function fillCreativeRequirement(page: Page) {
  await page.getByLabel('Format code').fill('DIGITAL_1920X1080');
  await page.getByLabel('Width').fill('1920');
  await page.getByLabel('Height').fill('1080');
  await page.getByLabel('Required file type').selectOption('image/png');
  await page.getByLabel('Maximum size (MiB)').fill('5');
  await page.getByLabel('Supplier instructions')
    .fill('Supply a 1920 by 1080 PNG with approved campaign copy.');
  await page.getByLabel('Why production is being requested')
    .fill('Create the exact booked format for supplier review.');
}

async function approveSupplierCreative(page: Page) {
  const title = 'Review the exact production file for your booked format.';
  await page.goto(`/creative-assets/${deliveryIds.asset}`);
  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await page.getByLabel('Technical evidence reference').fill('supplier-review:001');
  await page.getByLabel('Decision reason')
    .fill('The file meets the confirmed format and delivery specification.');
  await page.getByRole('button', { name: 'Approve technical delivery' }).click();
  await expect(page.getByRole('region', { name: title })
    .getByText('Approved', { exact: true })).toBeVisible();
}

async function completeDeliveryProof(page: Page) {
  await page.getByLabel('Launch reason')
    .fill('Funding, Bookings and current creative are ready for delivery.');
  await page.getByRole('button', { name: 'Start campaign' }).click();
  await page.getByLabel('Completion reason').fill('The booked delivery window has closed.');
  await page.getByLabel('Supplier proof request')
    .fill('Submit evidence for the exact confirmed Booking.');
  await page.getByRole('button', { name: 'Complete and request proof' }).click();
  await page.goto('/delivery-proof-requests');
  await page.getByRole('link', { name: 'Submit delivery proof' }).click();
  await page.getByLabel('Proof type').selectOption('PHOTO');
  await page.getByLabel('Captured at').fill('2026-08-31T09:00');
  await page.getByLabel('Location description').fill('N1 digital billboard, Johannesburg');
  await page.getByLabel('Source reference').fill('supplier-camera:001');
  await page.getByLabel('Submission reason')
    .fill('Proof captured inside the exact confirmed delivery window.');
  await page.getByLabel('Evidence file').setInputFiles(png);
  await page.getByRole('button', { name: 'Submit delivery proof' }).click();
  await page.getByLabel('Review reason').fill('Proof matches the exact Booking and flight.');
  await page.getByRole('button', { name: 'Approve proof' }).click();
}

async function completeMeasurement(page: Page) {
  await page.goto(`/campaigns/${deliveryIds.campaign}`);
  await page.getByLabel('Source reference').fill('platform-export:campaign-001');
  await page.getByLabel('Captured at').fill('2026-08-31T09:30');
  await page.getByLabel('Evidence quality').selectOption('VERIFIED');
  await page.getByLabel('Assigned reviewer').selectOption(deliveryIds.reviewer);
  await page.getByLabel('Methodology')
    .fill('Count verified platform responses during the booked flight.');
  await page.getByLabel('Limitations — one per line')
    .fill('The source does not establish causal attribution.');
  await page.getByRole('combobox', { name: 'Metric' }).selectOption('CONVERSIONS');
  await page.getByLabel('Value', { exact: true }).fill('42');
  await page.getByRole('combobox', { name: 'Unit' }).selectOption('COUNT');
  await page.getByLabel('Metric source locator')
    .fill('platform-export:campaign-001:conversions');
  await page.getByLabel('Evidence file').setInputFiles(csv);
  await page.getByRole('button', { name: 'Submit evidence for review' }).click();
  await page.getByLabel('Evidence review reason')
    .fill('The sourced metric and stated limitation are complete.');
  await page.getByRole('button', { name: 'Approve evidence' }).click();
  await page.getByLabel('Assigned report reviewer').selectOption(deliveryIds.reviewer);
  await page.getByRole('button', { name: 'Generate sourced report' }).click();
  await page.getByLabel('Report review reason')
    .fill('The report retains every approved fact and limitation.');
  await page.getByRole('button', { name: 'Approve client report' }).click();
}
