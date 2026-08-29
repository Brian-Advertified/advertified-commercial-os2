import { toast } from 'react-toastify'

export const notifications = {
  success(message: string) { toast.success(message) },
  information(message: string) { toast.info(message) },
  warning(message: string) { toast.warning(message) },
  failure(message: string) { toast.error(message) },
}
