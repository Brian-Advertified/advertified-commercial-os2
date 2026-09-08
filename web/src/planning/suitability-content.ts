export const suitabilityContent = {
  title: 'Buying evidence and gaps',
  unassessed: 'Placement screening has not been supplied. Review the buying evidence before selection.',
  explanation: 'These checks compare a placement with the supplied requirements. They do not establish the best campaign combination.',
  missingTitle: 'What still needs a planner’s assessment',
  needsEvidence: 'Needs evidence',
  reachCaveat: 'Profile matches are not people reached. Campaign reach, frequency and audience overlap have not been calculated here.',
  uncomputed: [
    { label: 'Message and format suitability', detail: 'Check that the creative, viewing time and placement support the campaign objective.' },
    { label: 'Cost per relevant audience reached', detail: 'Compare the full buy, exposure and target audience on compatible terms. A lower listed rate alone does not show better value.' },
    { label: 'Additional campaign reach', detail: 'Establish what this placement adds beyond the other selected media, including audience duplication.' },
  ],
} as const
