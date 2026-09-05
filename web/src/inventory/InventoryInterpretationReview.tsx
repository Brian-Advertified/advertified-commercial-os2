import { useState } from 'react'
import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryImport } from '../api/inventory-schemas'
import { inventoryAcceptanceCopy as copy } from '../content/inventory-acceptance-copy'

export function InventoryInterpretationReview({ record, tenantId, canReview, busy, run }: {
  record: InventoryImport; tenantId: string; canReview: boolean; busy: boolean
  run: (action: (token: string) => Promise<unknown>, success: string) => Promise<void>
}) {
  const [schema, setSchema] = useState(record.interpretation?.schemaJson ?? '')
  const [reason, setReason] = useState('')
  const interpretation = record.interpretation
  if (!interpretation) return null
  const editable = canReview && record.status === inventoryCodes.importStatus.reviewRequired
  const save = () => run(token => {
    const correctedSchema: unknown = JSON.parse(schema)
    return inventoryApi.reprojectExtraction(tenantId, record, token, reason,
      { reevaluateAcceptance: true, correctedSchema, expectedMappingRevision: interpretation.mappingRevision })
  }, copy.saved)
  return <section className="detail-card">
    <h2>{copy.heading}</h2><p>{copy.explanation}</p>
    <a href={`/api/v1/tenants/${tenantId}/inventory-imports/${record.id}/source`} download>{copy.original}</a>
    <p>{copy.revision}: <code>{interpretation.mappingRevision}</code></p>
    {interpretation.failure && <p role="status">{interpretation.failure}</p>}
    <details open><summary>{copy.structure}</summary><pre>{interpretation.structureJson}</pre></details>
    <details><summary>{copy.mappings}</summary><pre>{interpretation.schemaJson}</pre></details>
    {editable && <>
      <label className="field-group">{copy.correction}<textarea rows={14} value={schema}
        onChange={event => setSchema(event.target.value)} /></label>
      <label className="field-group">{copy.reason}<input maxLength={2000} value={reason}
        onChange={event => setReason(event.target.value)} /></label>
      <button className="secondary-button" disabled={busy || !reason.trim() || !schema.trim()} onClick={() => void save()}>{copy.save}</button>
      <button className="secondary-button" disabled={busy || !reason.trim()} onClick={() => void run(token =>
        inventoryApi.reprojectExtraction(tenantId, record, token, reason,
          { reevaluateAcceptance: true, expectedMappingRevision: interpretation.mappingRevision }), copy.saved)}>{copy.retry}</button>
    </>}
  </section>
}
