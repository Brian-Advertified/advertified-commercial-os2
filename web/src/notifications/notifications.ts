import { toast } from 'react-toastify'

function replaceVisibleNotification(show: () => void) {
  toast.clearWaitingQueue()
  toast.dismiss()
  show()
}

export const notifications = {
  success(message: string) {
    replaceVisibleNotification(() => toast.success(message))
  },
  information(message: string) {
    replaceVisibleNotification(() => toast.info(message))
  },
  warning(message: string) {
    replaceVisibleNotification(() => toast.warning(message))
  },
  failure(message: string) {
    replaceVisibleNotification(() => toast.error(message))
  },
}
