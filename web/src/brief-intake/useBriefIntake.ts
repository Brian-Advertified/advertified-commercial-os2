import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { briefApi, type CreateBriefVersion } from '../api/brief-client'
import type { BriefClarification, SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import { masterDataCodes } from '../generated/master-data-codes'
import type { BriefSpatialDraft } from './BriefSpatialEditor'

const CampaignModeField = 'campaignMode'

export type BriefIntakeContext = {
  tenantId: string
  userId: string
  token: string
}

type SourceDraft = { title: string; content: string }
type BriefPreparationKeys = {
  brief: string
  version: string
  submit: string
  approve: string
  campaignMode: string
  sourceLocator: string
}
type IntakeModel = {
  source: SourceDraft
  clarifications: BriefClarification[]
  understanding: SuppliedBriefUnderstanding | null
  preparationKeys: BriefPreparationKeys | null
  spatialRequirements: BriefSpatialDraft[]
  busy: boolean
  error: string | null
}

type UpdateModel = React.Dispatch<React.SetStateAction<IntakeModel>>
type Navigate = ReturnType<typeof useNavigate>
type Understand = (source: SourceDraft, clarifications: BriefClarification[]) => Promise<void>

const initialModel: IntakeModel = {
  source: { title: '', content: '' },
  clarifications: [],
  understanding: null,
  preparationKeys: null,
  spatialRequirements: [],
  busy: false,
  error: null,
}

export function useBriefIntake(context: BriefIntakeContext) {
  const [model, setModel] = useState<IntakeModel>(initialModel)
  const navigate = useNavigate()
  const understand = useUnderstandBrief(context, setModel)
  return {
    ...model,
    submitSource: submitSource(understand, setModel),
    submitClarifications: submitClarifications(understand, model),
    approveReview: approveReview(context, model, setModel, navigate),
    retryPlanning: retryPlanning(context, model, setModel, navigate),
    editSource: editSource(setModel),
    correctMode: correctMode(setModel),
    setSpatialRequirements: (values: BriefSpatialDraft[]) =>
      setModel(current => ({ ...current, spatialRequirements: values })),
  }
}

function useUnderstandBrief(
  context: BriefIntakeContext,
  setModel: UpdateModel,
): Understand {
  return async (source, clarifications) => {
    setModel(current => ({ ...current, busy: true, error: null }))
    try {
      const result = await briefApi.understand(context.tenantId, {
        sourceTitle: source.title,
        sourceContent: source.content,
        clarifications,
      }, context.token)
      if (result.requiresHumanClarification && result.questions.length === 0) {
        throw new Error('The Brief needs clarification, but no question was provided.')
      }
      setModel(current => ({
        ...current,
        source,
        clarifications,
        understanding: result,
        preparationKeys: result.requiresHumanClarification ? null : createPreparationKeys(),
        busy: false,
      }))
    } catch (failure) {
      setModel(current => ({ ...current, error: humanMessage(failure), busy: false }))
    }
  }
}

function approveReview(
  context: BriefIntakeContext,
  model: IntakeModel,
  setModel: UpdateModel,
  navigate: Navigate,
) {
  return async () => {
    if (!model.understanding || model.understanding.requiresHumanClarification ||
        !model.preparationKeys) return
    if (!spatialRequirementsReady(model.spatialRequirements)) {
      setModel(current => ({ ...current,
        error: 'Complete and verify each exact geography before continuing.' }))
      return
    }
    setModel(current => ({ ...current, busy: true, error: null }))
    await preparePlanning(
      context,
      model.source,
      model.understanding,
      model.clarifications,
      model.preparationKeys,
      model.spatialRequirements,
      setModel,
      navigate,
    )
  }
}

async function preparePlanning(
  context: BriefIntakeContext,
  source: SourceDraft,
  understanding: SuppliedBriefUnderstanding,
  clarifications: BriefClarification[],
  keys: BriefPreparationKeys,
  spatialRequirements: BriefSpatialDraft[],
  setModel: UpdateModel,
  navigate: Navigate,
) {
  try {
    const id = await createCampaign(
      context, source, understanding, clarifications, keys, spatialRequirements)
    navigate(`/stp/${id}`)
  } catch (failure) {
    setModel(current => ({ ...current, error: humanMessage(failure), busy: false }))
  }
}

function retryPlanning(
  context: BriefIntakeContext,
  model: IntakeModel,
  setModel: UpdateModel,
  navigate: Navigate,
) {
  return () => {
    if (!model.understanding || model.understanding.requiresHumanClarification ||
        !model.preparationKeys) return
    setModel(current => ({ ...current, busy: true, error: null }))
    void preparePlanning(
      context,
      model.source,
      model.understanding,
      model.clarifications,
      model.preparationKeys,
      model.spatialRequirements,
      setModel,
      navigate,
    )
  }
}

function submitSource(understand: Understand, setModel: UpdateModel) {
  return (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const source = {
      title: requiredField(values, 'sourceTitle'),
      content: requiredField(values, 'sourceContent'),
    }
    setModel(current => ({
      ...current,
      source,
      clarifications: [],
      preparationKeys: null,
    }))
    void understand(source, [])
  }
}

function submitClarifications(understand: Understand, model: IntakeModel) {
  return (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!model.understanding) return
    const values = new FormData(event.currentTarget)
    const answers = model.understanding.questions.map(question => ({
      fieldPath: question.fieldPath,
      value: requiredField(values, question.fieldPath),
    }))
    const merged = new Map(model.clarifications.map(item => [item.fieldPath, item]))
    answers.forEach(answer => merged.set(answer.fieldPath, answer))
    void understand(model.source, [...merged.values()])
  }
}

function editSource(setModel: UpdateModel) {
  return () => setModel(current => ({
    ...current,
    understanding: null,
    clarifications: [],
    preparationKeys: null,
    error: null,
  }))
}

function correctMode(setModel: UpdateModel) {
  return () => setModel(current => current.understanding ? ({
    ...current,
    understanding: {
      ...current.understanding,
      requiresHumanClarification: true,
      questions: [{
        fieldPath: CampaignModeField,
        question: 'Should this use only out-of-home media or a full campaign?',
        isBlocking: true,
        options: [
          masterDataCodes.campaignModes.oohOnly,
          masterDataCodes.campaignModes.fullCampaign,
        ],
      }],
    },
    preparationKeys: null,
    error: null,
  }) : current)
}

async function createCampaign(
  context: BriefIntakeContext,
  source: SourceDraft,
  understanding: SuppliedBriefUnderstanding,
  clarifications: BriefClarification[],
  keys: BriefPreparationKeys,
  spatialRequirements: BriefSpatialDraft[],
) {
  if (!understanding.clientName || !understanding.campaignMode) {
    throw new Error('The client and campaign media scope must be clear before planning starts.')
  }
  const brief = await briefApi.create(context.tenantId, {
    clientId: null,
    clientName: understanding.clientName,
    title: understanding.title,
    ownerUserId: context.userId,
    sourceLocator: keys.sourceLocator,
    sourceTitle: source.title,
    sourceContent: source.content,
    sourceType: masterDataCodes.briefSourceTypes.suppliedText,
  }, context.token, keys.brief)
  const draft = await briefApi.createVersion(
    context.tenantId,
    brief.id,
    draftPayload(brief.id, understanding, spatialRequirements),
    context.token,
    keys.version,
  )
  const submitted = await briefApi.submit(
    context.tenantId, draft, context.token, null, keys.submit)
  const approved = await briefApi.approve(
    context.tenantId, submitted, context.token, keys.approve)
  const modeClarified = clarifications.some(item => item.fieldPath === CampaignModeField)
  const modeEvidence = understanding.evidence.find(item => item.fieldPath === CampaignModeField)
  const suppliedModeFact = modeEvidence?.kind === masterDataCodes.evidenceClassifications.fact &&
    modeEvidence.sourceLocator === 'supplied:brief'
  await planningApi.selectCampaignMode(
    context.tenantId, approved.id, understanding.campaignMode, context.token, {
      source: modeClarified
        ? masterDataCodes.campaignModeDecisionSources.humanClarification
        : suppliedModeFact
          ? masterDataCodes.campaignModeDecisionSources.suppliedBriefEvidence
          : masterDataCodes.campaignModeDecisionSources.agent,
      confidence: understanding.campaignModeConfidence,
      reason: understanding.campaignModeRationale,
    }, keys.campaignMode)
  return approved.id
}

function createPreparationKeys(): BriefPreparationKeys {
  return {
    brief: crypto.randomUUID(),
    version: crypto.randomUUID(),
    submit: crypto.randomUUID(),
    approve: crypto.randomUUID(),
    campaignMode: crypto.randomUUID(),
    sourceLocator: `supplied:web:${crypto.randomUUID()}`,
  }
}

function draftPayload(
  briefId: string,
  result: SuppliedBriefUnderstanding,
  spatialRequirements: BriefSpatialDraft[],
): CreateBriefVersion {
  const draft = result.draft
  return {
    briefId, baseVersionId: null, businessProblem: draft.businessProblem,
    objective: draft.objective, audiences: draft.audiences,
    geographies: draft.geographies, timing: draft.timing,
    budgetMinor: draft.budgetMinor, budgetUnknown: draft.budgetUnknown,
    currency: draft.currency, vatStatus: draft.vatStatus, feesMinor: draft.feesMinor,
    constraints: draft.constraints, measurement: draft.measurement, facts: draft.facts,
    unknowns: draft.unknowns, assumptions: draft.assumptions,
    conflicts: draft.conflicts, evidenceItemIds: [], spatialRequirements,
  }
}

function requiredField(values: FormData, name: string): string {
  const result = String(values.get(name) ?? '').trim()
  if (!result) throw new Error('Complete the requested information before continuing.')
  return result
}

function spatialRequirementsReady(values: BriefSpatialDraft[]): boolean {
  return values.every(spatialRequirementReady)
}

function spatialRequirementReady(value: BriefSpatialDraft): boolean {
  if (!basicSpatialFieldsReady(value) || !spatialTypeFieldsReady(value)) return false
  try {
    const parsed = JSON.parse(value.geoJson) as { type?: unknown; coordinates?: unknown }
    return typeof parsed.type === 'string' && Array.isArray(parsed.coordinates)
  } catch {
    return false
  }
}

function basicSpatialFieldsReady(value: BriefSpatialDraft) {
  return Boolean(value.label.trim() && value.geoJson.trim() && value.isVerified &&
    value.coverageThreshold && value.coverageThreshold > 0 && value.coverageThreshold <= 1)
}

function spatialTypeFieldsReady(value: BriefSpatialDraft) {
  if (value.type === masterDataCodes.spatialRequirementTypes.pointRadius) {
    return Boolean(value.radiusMetres && value.radiusMetres > 0)
  }
  if (value.type === masterDataCodes.spatialRequirementTypes.adminBoundary) {
    return Boolean(value.boundarySource?.trim() && value.boundaryVersion?.trim())
  }
  return true
}
