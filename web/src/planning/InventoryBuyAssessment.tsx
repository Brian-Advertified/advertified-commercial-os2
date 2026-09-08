import type { BuyAssessment } from '../api/buy-assessment-schema'
import { formatMoney } from '../presentation/format'
import { buyAssessmentContent as copy } from './buy-assessment-content'
import './buy-assessment.css'

export function InventoryBuyAssessment({ assessment }: { assessment: BuyAssessment }) {
  const money = (value: number | null) => value !== null && assessment.currency
    ? formatMoney(value, assessment.currency) : copy.unknown
  const digital = assessment.digitalExposure
  return <section aria-label={copy.title} className="buy-assessment">
    <h4>{copy.title}</h4>
    <p><strong>{money(assessment.campaignSupplierCostMinor)}</strong> — {copy.supplierCost}</p>
    <small>{copy.costCaveat}</small>
    <h4>{copy.baseline}</h4>
    <p>{assessment.isTargetAudience ? copy.target : copy.nonTarget}</p>
    <dl className="buy-assessment-facts">
      <Fact label={copy.reach} value={assessment.reach} />
      <Fact label={copy.impressions} value={assessment.impressions} />
      <Fact label={copy.frequency} value={assessment.averageFrequency} />
      <Fact label={copy.cpm} value={money(assessment.costPerThousandImpressionsMinor)} />
      <Fact label={copy.cpp} value={money(assessment.costPerPersonReachedMinor)} />
    </dl>
    <small>{copy.ratioCaveat}</small>
    {digital && <>
      <h4>{copy.digital}</h4>
      <dl className="buy-assessment-facts">
        <Fact label={copy.creative} value={digital.spotLengthSeconds} />
        <Fact label={copy.slot} value={digital.slotLengthSeconds} />
        <Fact label={copy.loop} value={digital.loopLengthSeconds} />
        <Fact label={copy.plays} value={digital.playsPerLoop} />
        <Fact label={copy.share} value={digital.loopSharePercent} />
      </dl><small>{copy.loopCaveat}</small>
    </>}
    <dl className="buy-assessment-facts">
      <Fact label={copy.source} value={assessment.measurementSource} />
      <Fact label={copy.period} value={assessment.measurementPeriod} />
      <Fact label={copy.universe} value={assessment.universe} />
      <Fact label={copy.methodology} value={assessment.methodology} />
    </dl>
  </section>
}

function Fact({ label, value }: { label: string; value: number | string | null }) {
  return <div><dt>{label}</dt><dd>{typeof value === 'number'
    ? value.toLocaleString(undefined, { maximumFractionDigits: 2 }) : value ?? copy.unknown}</dd></div>
}
