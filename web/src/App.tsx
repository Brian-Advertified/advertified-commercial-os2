import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import './pages.css'
import './planning.css'
import './proposal.css'
import './email-automation.css'
import './marketplace.css'
import { useSession } from './auth/session-state'
import { WorkspaceProvider } from './auth/WorkspaceContext'
import { AppShell } from './components/AppShell'
import { LoadingState } from './components/PageState'
import { DeferredPage, NotFoundPage } from './pages/DeferredPage'
import { HomePage } from './pages/HomePage'
import { OpportunitiesPage } from './pages/OpportunitiesPage'
import { OpportunityDetailPage } from './pages/OpportunityDetailPage'
import { ProfilePage } from './pages/ProfilePage'
import { SignInPage } from './pages/SignInPage'
import { WorkspacesPage } from './pages/WorkspacesPage'
import { TasksPage } from './pages/TasksPage'
import { StrategyPage } from './pages/StrategyPage'
import { RunPage } from './pages/RunPage'
import { NewBriefPage } from './pages/NewBriefPage'
import { BriefPage } from './pages/BriefPage'
import { InventoryPage } from './pages/InventoryPage'
import { InventoryImportPage } from './pages/InventoryImportPage'
import { InventoryProductPage } from './pages/InventoryProductPage'

const PlanningPage = lazy(() => import('./pages/PlanningPage')
  .then(module => ({ default: module.PlanningPage })))
const NewProposalPage = lazy(() => import('./pages/NewProposalPage')
  .then(module => ({ default: module.NewProposalPage })))
const ProposalPage = lazy(() => import('./pages/ProposalPage')
  .then(module => ({ default: module.ProposalPage })))
const OohInboxPage = lazy(() => import('./pages/OohInboxPage')
  .then(module => ({ default: module.OohInboxPage })))
const MarketplacePage = lazy(() => import('./pages/MarketplacePage')
  .then(module => ({ default: module.MarketplacePage })))
const CommercialPolicyPage = lazy(() => import('./pages/CommercialPolicyPage')
  .then(module => ({ default: module.CommercialPolicyPage })))

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
          <Route path="/opportunities" element={<OpportunitiesPage />} />
          <Route path="/opportunities/:opportunityId" element={<OpportunityDetailPage />} />
          <Route path="/strategies/:strategyId" element={<StrategyPage />} />
          <Route path="/runs/:runId" element={<RunPage />} />
          <Route path="/briefs/new" element={<NewBriefPage />} />
          <Route path="/briefs/:briefId" element={<BriefPage />} />
          <Route path="/inventory" element={<InventoryPage />} />
          <Route path="/inventory/imports/:importId" element={<InventoryImportPage />} />
          <Route path="/inventory/products/:productId" element={<InventoryProductPage />} />
          <Route path="/planning/:briefVersionId" element={<Suspense fallback={<LoadingState label="Loading media planning" />}><PlanningPage /></Suspense>} />
          <Route path="/briefs/:briefId/proposals/new" element={<Suspense fallback={<LoadingState label="Loading proposal builder" />}><NewProposalPage /></Suspense>} />
          <Route path="/proposals/:proposalId" element={<Suspense fallback={<LoadingState label="Loading proposal" />}><ProposalPage /></Suspense>} />
          <Route path="/ooh-inbox" element={<Suspense fallback={<LoadingState label="Loading OOH proposal inbox" />}><OohInboxPage /></Suspense>} />
          <Route path="/marketplace" element={<Suspense fallback={<LoadingState label="Loading supplier marketplace" />}><MarketplacePage /></Suspense>} />
          <Route path="/admin/commercial" element={<Suspense fallback={<LoadingState label="Loading commercial settings" />}><CommercialPolicyPage /></Suspense>} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/tasks" element={<TasksPage />} />
          <Route path="/notifications" element={<DeferredPage destination="Notifications" />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
