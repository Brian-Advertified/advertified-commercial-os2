import { lazy, Suspense, type ReactNode } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import './App.css'
import './pages.css'
import './brief-intake.css'
import './planning.css'
import './planning-workbench.css'
import './proposal.css'
import './proposal-funding.css'
import './email-automation.css'
import './marketplace.css'
import './booking.css'
import './funding.css'
import './campaign-list.css'
import './campaign-workbench.css'
import './campaign-creative.css'
import './campaign-proof.css'
import './campaign-measurement.css'
import './delivery-requests.css'
import './workbench.css'
import './brief-workbench.css'
import './brief-record.css'
import './inventory-workbench.css'
import './operations-workbench.css'
import './agent-operations.css'
import './approved-ui.css'
import './approved-workflow.css'
import './approved-workflow-extensions.css'
import './approved-workspace-controls.css'
import './global-search.css'
import { LoadingState } from './components/PageState'
import { SignInPage } from './pages/SignInPage'
import { publicRoutes } from './public/publicRoutes'

const AuthenticatedApplication = lazy(() => import('./routing/AuthenticatedApplication')
  .then(module => ({ default: module.AuthenticatedApplication })))
const PublicSiteRoute = lazy(() => import('./public/PublicSiteRoute')
  .then(module => ({ default: module.PublicSiteRoute })))
const DeferredPage = lazy(() => import('./pages/DeferredPage')
  .then(module => ({ default: module.DeferredPage })))
const NotFoundPage = lazy(() => import('./pages/DeferredPage')
  .then(module => ({ default: module.NotFoundPage })))
const WorkspacesPage = lazy(() => import('./pages/WorkspacesPage')
  .then(module => ({ default: module.WorkspacesPage })))
const HomePage = lazy(() => import('./pages/HomePage')
  .then(module => ({ default: module.HomePage })))
const OpportunitiesPage = lazy(() => import('./pages/OpportunitiesPage')
  .then(module => ({ default: module.OpportunitiesPage })))
const OpportunityDetailPage = lazy(() => import('./pages/OpportunityDetailPage')
  .then(module => ({ default: module.OpportunityDetailPage })))
const StrategyPage = lazy(() => import('./pages/StrategyPage')
  .then(module => ({ default: module.StrategyPage })))
const RunPage = lazy(() => import('./pages/RunPage')
  .then(module => ({ default: module.RunPage })))
const NewBriefPage = lazy(() => import('./pages/NewBriefPage')
  .then(module => ({ default: module.NewBriefPage })))
const BriefPage = lazy(() => import('./pages/BriefPage')
  .then(module => ({ default: module.BriefPage })))
const StpPage = lazy(() => import('./pages/StpPage')
  .then(module => ({ default: module.StpPage })))
const BriefsIndexPage = lazy(() => import('./pages/WorkIndexPages')
  .then(module => ({ default: module.BriefsIndexPage })))
const StrategyStpIndexPage = lazy(() => import('./pages/WorkIndexPages')
  .then(module => ({ default: module.StrategyStpIndexPage })))
const PlanningIndexPage = lazy(() => import('./pages/WorkIndexPages')
  .then(module => ({ default: module.PlanningIndexPage })))
const ProposalsIndexPage = lazy(() => import('./pages/WorkIndexPages')
  .then(module => ({ default: module.ProposalsIndexPage })))
const ApprovalsIndexPage = lazy(() => import('./pages/WorkIndexPages')
  .then(module => ({ default: module.ApprovalsIndexPage })))
const MeasurementIndexPage = lazy(() => import('./pages/WorkIndexPages')
  .then(module => ({ default: module.MeasurementIndexPage })))
const ReportsIndexPage = lazy(() => import('./pages/WorkIndexPages')
  .then(module => ({ default: module.ReportsIndexPage })))
const InventoryPage = lazy(() => import('./pages/InventoryPage')
  .then(module => ({ default: module.InventoryPage })))
const InventoryImportPage = lazy(() => import('./pages/InventoryImportPage')
  .then(module => ({ default: module.InventoryImportPage })))
const InventoryProductPage = lazy(() => import('./pages/InventoryProductPage')
  .then(module => ({ default: module.InventoryProductPage })))
const ProfilePage = lazy(() => import('./pages/ProfilePage')
  .then(module => ({ default: module.ProfilePage })))
const TasksPage = lazy(() => import('./pages/TasksPage')
  .then(module => ({ default: module.TasksPage })))
const SearchPage = lazy(() => import('./pages/SearchPage')
  .then(module => ({ default: module.SearchPage })))

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
const AgentOperationsPage = lazy(() => import('./pages/AgentOperationsPage')
  .then(module => ({ default: module.AgentOperationsPage })))
const BookingsPage = lazy(() => import('./pages/BookingsPage')
  .then(module => ({ default: module.BookingsPage })))
const FundingPage = lazy(() => import('./pages/FundingPage')
  .then(module => ({ default: module.FundingPage })))
const CampaignsPage = lazy(() => import('./pages/CampaignsPage')
  .then(module => ({ default: module.CampaignsPage })))
const CampaignPage = lazy(() => import('./pages/CampaignPage')
  .then(module => ({ default: module.CampaignPage })))
const SupplierCreativePage = lazy(() => import('./pages/SupplierCreativePage')
  .then(module => ({ default: module.SupplierCreativePage })))
const DeliveryProofRequestsPage = lazy(() => import('./pages/DeliveryProofRequestsPage')
  .then(module => ({ default: module.DeliveryProofRequestsPage })))
const DeliveryProofSubmissionPage = lazy(() => import('./pages/DeliveryProofSubmissionPage')
  .then(module => ({ default: module.DeliveryProofSubmissionPage })))
const DeliveryProofPage = lazy(() => import('./pages/DeliveryProofPage')
  .then(module => ({ default: module.DeliveryProofPage })))
const PerformanceEvidencePage = lazy(() => import('./pages/PerformanceEvidencePage')
  .then(module => ({ default: module.PerformanceEvidencePage })))
const MeasurementReportPage = lazy(() => import('./pages/MeasurementReportPage')
  .then(module => ({ default: module.MeasurementReportPage })))

function deferredRoute(content: ReactNode) {
  return <Suspense fallback={<LoadingState label="Loading page" />}>{content}</Suspense>
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {publicRoutes.map((route) => (
          <Route key={route.path} path={route.path} element={deferredRoute(<PublicSiteRoute />)} />
        ))}
        <Route path="/terms" element={deferredRoute(<PublicSiteRoute />)} />
        <Route path="/cookies" element={deferredRoute(<PublicSiteRoute />)} />
        <Route path="/media-network/:channel" element={deferredRoute(<PublicSiteRoute />)} />
        <Route path="/solutions/:channel" element={deferredRoute(<PublicSiteRoute />)} />
        <Route path="/register/:registrationType" element={deferredRoute(<PublicSiteRoute />)} />
        <Route path="/sign-in" element={<SignInPage />} />
        <Route element={deferredRoute(<AuthenticatedApplication />)}>
          <Route path="/workspaces" element={deferredRoute(<WorkspacesPage />)} />
          <Route path="/home" element={deferredRoute(<HomePage />)} />
          <Route path="/opportunities" element={deferredRoute(<OpportunitiesPage />)} />
          <Route path="/opportunities/:opportunityId" element={deferredRoute(<OpportunityDetailPage />)} />
          <Route path="/strategies/:strategyId" element={deferredRoute(<StrategyPage />)} />
          <Route path="/runs/:runId" element={deferredRoute(<RunPage />)} />
          <Route path="/briefs" element={deferredRoute(<BriefsIndexPage />)} />
          <Route path="/briefs/new" element={deferredRoute(<NewBriefPage />)} />
          <Route path="/briefs/:briefId" element={deferredRoute(<BriefPage />)} />
          <Route path="/strategy-stp" element={deferredRoute(<StrategyStpIndexPage />)} />
          <Route path="/stp/:briefVersionId" element={deferredRoute(<StpPage />)} />
          <Route path="/inventory" element={deferredRoute(<InventoryPage />)} />
          <Route path="/inventory/imports/:importId" element={deferredRoute(<InventoryImportPage />)} />
          <Route path="/inventory/products/:productId" element={deferredRoute(<InventoryProductPage />)} />
          <Route path="/planning" element={deferredRoute(<PlanningIndexPage />)} />
          <Route path="/planning/:briefVersionId" element={deferredRoute(<PlanningPage />)} />
          <Route path="/proposals" element={deferredRoute(<ProposalsIndexPage />)} />
          <Route path="/briefs/:briefId/proposals/new" element={deferredRoute(<NewProposalPage />)} />
          <Route path="/proposals/:proposalId" element={deferredRoute(<ProposalPage />)} />
          <Route path="/ooh-inbox" element={deferredRoute(<OohInboxPage />)} />
          <Route path="/marketplace" element={deferredRoute(<MarketplacePage />)} />
          <Route path="/bookings" element={deferredRoute(<BookingsPage />)} />
          <Route path="/funding" element={deferredRoute(<FundingPage />)} />
          <Route path="/campaigns" element={deferredRoute(<CampaignsPage />)} />
          <Route path="/campaigns/:campaignId" element={deferredRoute(<CampaignPage />)} />
          <Route path="/creative-assets/:assetId" element={deferredRoute(<SupplierCreativePage />)} />
          <Route path="/delivery-proof-requests" element={deferredRoute(<DeliveryProofRequestsPage />)} />
          <Route path="/campaigns/:campaignId/bookings/:bookingId/delivery-proof/new" element={deferredRoute(<DeliveryProofSubmissionPage />)} />
          <Route path="/delivery-proofs/:proofId" element={deferredRoute(<DeliveryProofPage />)} />
          <Route path="/performance-evidence/:evidenceId" element={deferredRoute(<PerformanceEvidencePage />)} />
          <Route path="/measurement" element={deferredRoute(<MeasurementIndexPage />)} />
          <Route path="/reports" element={deferredRoute(<ReportsIndexPage />)} />
          <Route path="/measurement-reports/:reportId" element={deferredRoute(<MeasurementReportPage />)} />
          <Route path="/admin/commercial" element={deferredRoute(<CommercialPolicyPage />)} />
          <Route path="/admin/agents" element={deferredRoute(<AgentOperationsPage />)} />
          <Route path="/profile" element={deferredRoute(<ProfilePage />)} />
          <Route path="/tasks" element={deferredRoute(<TasksPage />)} />
          <Route path="/search" element={deferredRoute(<SearchPage />)} />
          <Route path="/approvals" element={deferredRoute(<ApprovalsIndexPage />)} />
          <Route path="/notifications" element={deferredRoute(<DeferredPage destination="Notifications" />)} />
          <Route path="*" element={deferredRoute(<NotFoundPage />)} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
