export type CampaignActionRunner = (
  action: () => Promise<unknown>,
  successMessage: string,
) => Promise<void>
