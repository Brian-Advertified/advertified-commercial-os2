import toastr from 'toastr'
import 'toastr/build/toastr.min.css'
import './notifications.css'

toastr.options = {
  closeButton: true,
  newestOnTop: true,
  progressBar: true,
  positionClass: 'toast-bottom-right',
  preventDuplicates: true,
  timeOut: 4500,
  extendedTimeOut: 1500,
  tapToDismiss: true,
  escapeHtml: true,
}

function replaceVisibleNotification(show: () => void) {
  toastr.clear()
  show()
}

export const notifications = {
  success(message: string) {
    replaceVisibleNotification(() => { toastr.success(message) })
  },
  information(message: string) {
    replaceVisibleNotification(() => { toastr.info(message) })
  },
  warning(message: string) {
    replaceVisibleNotification(() => { toastr.warning(message) })
  },
  failure(message: string) {
    replaceVisibleNotification(() => { toastr.error(message) })
  },
}
