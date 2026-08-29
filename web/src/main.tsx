import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { SessionProvider } from './auth/SessionContext.tsx'
import { NotificationHost } from './notifications/NotificationHost.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SessionProvider>
      <App />
      <NotificationHost />
    </SessionProvider>
  </StrictMode>,
)
