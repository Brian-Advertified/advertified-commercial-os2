import { request } from './client'
import {
  fundingWorkspaceSchema,
  invoiceSchema,
  paymentIntentSchema,
  purchaseOrderSchema,
  type FundingWorkspace,
  type Invoice,
  type PaymentIntent,
  type PurchaseOrder,
  type PurchaseOrderInput,
} from './funding-schemas'

const root = (tenantId: string) => `/api/v1/tenants/${tenantId}`

export const fundingApi = {
  async getWorkspace(tenantId: string): Promise<FundingWorkspace> {
    return (await request(`${root(tenantId)}/funding`, fundingWorkspaceSchema)).data
  },

  async submitPurchaseOrder(
    tenantId: string,
    input: PurchaseOrderInput,
    document: File,
    token: string,
  ): Promise<PurchaseOrder> {
    const body = new FormData()
    body.set('proposalVersionId', input.proposalVersionId)
    body.set('proposalOptionId', input.proposalOptionId)
    body.set('purchaseOrderNumber', input.purchaseOrderNumber)
    body.set('amountMinor', String(input.amountMinor))
    body.set('currency', input.currency.toUpperCase())
    body.set('document', document)
    return (await request(
      `${root(tenantId)}/purchase-orders`, purchaseOrderSchema,
      { method: 'POST', body },
      { antiforgeryToken: token, idempotencyKey: crypto.randomUUID() },
    )).data
  },

  async approvePurchaseOrder(
    tenantId: string,
    order: PurchaseOrder,
    reconciliationReason: string,
    token: string,
  ): Promise<PurchaseOrder> {
    return (await request(
      `${root(tenantId)}/purchase-orders/${order.id}:approve`, purchaseOrderSchema,
      { method: 'POST', body: JSON.stringify({ reconciliationReason }) },
      {
        antiforgeryToken: token,
        expectedVersion: order.version,
        idempotencyKey: crypto.randomUUID(),
      },
    )).data
  },

  async issueInvoice(
    tenantId: string,
    order: PurchaseOrder,
    invoiceNumber: string,
    token: string,
  ): Promise<Invoice> {
    return (await request(
      `${root(tenantId)}/invoices:issue`, invoiceSchema,
      { method: 'POST', body: JSON.stringify({
        purchaseOrderId: order.id,
        invoiceNumber,
      }) },
      { antiforgeryToken: token, idempotencyKey: crypto.randomUUID() },
    )).data
  },

  async startPayment(
    tenantId: string,
    invoice: Invoice,
    methodCode: string,
    token: string,
  ): Promise<PaymentIntent> {
    return (await request(
      `${root(tenantId)}/payment-intents`, paymentIntentSchema,
      { method: 'POST', body: JSON.stringify({ invoiceId: invoice.id, methodCode }) },
      { antiforgeryToken: token, idempotencyKey: crypto.randomUUID() },
    )).data
  },

  async reconcilePayment(
    tenantId: string,
    payment: PaymentIntent,
    reconciliationReference: string,
    reason: string,
    receipt: File,
    token: string,
  ): Promise<PaymentIntent> {
    const body = new FormData()
    body.set('reconciliationReference', reconciliationReference)
    body.set('reason', reason)
    body.set('receipt', receipt)
    return (await request(
      `${root(tenantId)}/payment-intents/${payment.id}:reconcile`, paymentIntentSchema,
      { method: 'POST', body },
      {
        antiforgeryToken: token,
        expectedVersion: payment.version,
        idempotencyKey: crypto.randomUUID(),
      },
    )).data
  },
}
