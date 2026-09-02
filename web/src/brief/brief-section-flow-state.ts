import { useEffect, useState } from 'react'
import { masterDataCodes } from '../generated/master-data-codes'

export const briefSections = [
  ['overview', 'Overview'],
  ['objectives', 'Objectives'],
  [masterDataCodes.agentTypes.audience, 'Audience'],
  ['geography', 'Geography'],
  ['timing', 'Timing'],
  ['budget', 'Budget'],
  ['media', 'Media Preferences'],
  ['constraints', 'Constraints'],
  [masterDataCodes.agentTypes.measurement, 'Measurement'],
  ['attachments', 'Attachments'],
  ['review', 'Review & Submit'],
] as const

export type BriefSectionId = typeof briefSections[number][0]
export type BriefSectionStatus = 'complete' | 'attention'
export type BriefSectionState = {
  id: BriefSectionId
  label: string
  status: BriefSectionStatus
}

export function useBriefSectionFlow() {
  const [activeId, setActiveId] = useState<BriefSectionId>(() => sectionFromHash())

  useEffect(() => {
    const sync = () => setActiveId(sectionFromHash())
    window.addEventListener('hashchange', sync)
    return () => window.removeEventListener('hashchange', sync)
  }, [])

  function goTo(id: BriefSectionId) {
    setActiveId(id)
    window.history.pushState(null, '', `#brief-${id}`)
    window.requestAnimationFrame(() => {
      const section = document.getElementById(`brief-${id}`)
      section?.scrollIntoView({ behavior: 'smooth', block: 'start' })
      section?.focus({ preventScroll: true })
    })
  }

  return { activeId, goTo }
}

function sectionFromHash(): BriefSectionId {
  const candidate = window.location.hash.replace(/^#brief-/, '')
  return briefSections.some(([id]) => id === candidate)
    ? candidate as BriefSectionId
    : briefSections[0][0]
}
