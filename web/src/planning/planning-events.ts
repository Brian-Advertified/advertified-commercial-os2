export const planningChangedEvent = 'advertified:planning-changed'

export function announcePlanningChanged() {
  window.dispatchEvent(new Event(planningChangedEvent))
}
