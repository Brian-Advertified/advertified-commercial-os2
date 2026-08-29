import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import './pages.css'
import { useSession } from './auth/session-state'
import { WorkspaceProvider } from './auth/WorkspaceContext'
import { AppShell } from './components/AppShell'
import { LoadingState } from './components/PageState'
import { DeferredPage, NotFoundPage } from './pages/DeferredPage'
import { HomePage } from './pages/HomePage'
import { ProfilePage } from './pages/ProfilePage'
import { SignInPage } from './pages/SignInPage'
import { WorkspacesPage } from './pages/WorkspacesPage'

function AuthenticatedApplication() {
  const { session, loading } = useSession()
  if (loading && !session) return <LoadingState />
  if (!session?.authenticated) return <Navigate to="/sign-in" replace />
  return <WorkspaceProvider><AppShell /></WorkspaceProvider>
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/sign-in" element={<SignInPage />} />
        <Route element={<AuthenticatedApplication />}>
          <Route index element={<Navigate to="/home" replace />} />
          <Route path="/workspaces" element={<WorkspacesPage />} />
          <Route path="/home" element={<HomePage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/tasks" element={<DeferredPage destination="Tasks" />} />
          <Route path="/notifications" element={<DeferredPage destination="Notifications" />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
