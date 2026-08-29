import { ToastContainer } from 'react-toastify'
import 'react-toastify/dist/ReactToastify.css'

export function NotificationHost() {
  return (
    <ToastContainer
      position="bottom-right"
      autoClose={4500}
      newestOnTop
      closeOnClick
      pauseOnFocusLoss
      pauseOnHover
      theme="light"
      aria-label="Notifications"
    />
  )
}
