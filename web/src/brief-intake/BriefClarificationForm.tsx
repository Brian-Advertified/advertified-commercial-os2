import type { FormEvent } from 'react'
import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import { Icon } from '../components/Icon'
import { humanizeCode } from '../presentation/format'
import { BriefUnderstandingSummary } from './BriefUnderstandingSummary'
import { campaignModeLabel } from './brief-intake-presentation'

export function BriefClarificationForm({ understanding, busy, onSubmit, onEdit }: {
  understanding: SuppliedBriefUnderstanding
  busy: boolean
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onEdit: () => void
}) {
  return <div className="brief-clarification-workbench">
    <form className="brief-clarification-panel" onSubmit={onSubmit}>
      <header className="brief-review-heading"><div><p className="eyebrow">Focused clarification</p>
        <h2>Confirm only what could not be established</h2>
        <p>The rest of the Brief is already structured. Your answers are applied to the original request and reviewed again before planning.</p></div>
        <button className="text-action" type="button" onClick={onEdit}>Edit original Brief</button></header>
      <div className="brief-question-stack">{understanding.questions.map(question => {
        const evidence = understanding.evidence.find(item => item.fieldPath === question.fieldPath)
        return <fieldset className="brief-question-card" key={question.fieldPath}>
          <legend><span>{humanizeCode(question.fieldPath.replaceAll('.', '_'), true)}</span>
            {question.isBlocking && <em>Required to continue</em>}</legend>
          <h3>{question.question}</h3>
          {evidence?.excerpt && <blockquote><Icon name="evidence" />
            <span><small>Relevant source text</small>{evidence.excerpt}</span></blockquote>}
          {question.options.length > 0
            ? <div className="clarification-options">{question.options.map(option =>
                <label key={option}><input type="radio" name={question.fieldPath}
                  value={option} required /><span><strong>{optionLabel(question.fieldPath, option)}</strong>
                    <small>Select this only when it reflects the client requirement.</small></span></label>)}</div>
            : <label className="field-group">Your answer
                <input name={question.fieldPath} required maxLength={4000}
                  placeholder="Enter the confirmed information" />
              </label>}
        </fieldset>
      })}</div>
      <div className="brief-review-actions"><span><Icon name="shield" /> Only these answers will change the structured Brief.</span>
        <button className="primary-button" type="submit" disabled={busy}>
          {busy ? 'Applying the answers…' : 'Review the completed Brief'}
          {!busy && <Icon name="arrow" />}
        </button></div>
    </form>
    <aside aria-label="Current Brief understanding">
      <BriefUnderstandingSummary understanding={understanding} compact />
    </aside>
  </div>
}

function optionLabel(fieldPath: string, option: string) {
  return fieldPath === 'campaignMode'
    ? campaignModeLabel(option)
    : humanizeCode(option, true)
}
