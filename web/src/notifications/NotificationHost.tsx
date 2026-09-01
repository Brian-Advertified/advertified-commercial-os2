import { ToastContainer } from 'react-toastify'
import 'react-toastify/dist/ReactToastify.css'
import './notifications.css'

export function NotificationHost() {
  return (
    <ToastContainer
      position="bottom-right"
      autoClose={4500}
      limit={1}
      newestOnTop
      closeOnClick
      pauseOnFocusLoss={false}
      pauseOnHover
      theme="dark"
      className="advertified-notifications"
      aria-label="Notifications"
    />
  )
}
