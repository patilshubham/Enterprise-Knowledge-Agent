/* eslint-disable react-refresh/only-export-components -- route config file: lazy component
   references live alongside the non-component `router` export by design. */
import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { RootLayout } from './RootLayout';

const PlatformDashboardPage = lazy(() => import('../pages/platform/PlatformDashboardPage'));
const PlatformApplicationsPage = lazy(() => import('../pages/platform/PlatformApplicationsPage'));
const PlatformProcessingPage = lazy(() => import('../pages/platform/PlatformProcessingPage'));
const PlatformAuditPage = lazy(() => import('../pages/platform/PlatformAuditPage'));
const PlatformOperationsPage = lazy(() => import('../pages/platform/PlatformOperationsPage'));

const ApplicationOverviewPage = lazy(() => import('../pages/applications/ApplicationOverviewPage'));
const ApplicationKnowledgePage = lazy(
  () => import('../pages/applications/ApplicationKnowledgePage'),
);
const KnowledgeFileDetailPage = lazy(() => import('../pages/applications/KnowledgeFileDetailPage'));
const ApplicationProcessingPage = lazy(
  () => import('../pages/applications/ApplicationProcessingPage'),
);
const ApplicationAgentsPage = lazy(() => import('../pages/applications/ApplicationAgentsPage'));
const AgentDetailPage = lazy(() => import('../pages/applications/AgentDetailPage'));
const ApplicationRunsPage = lazy(() => import('../pages/applications/ApplicationRunsPage'));
const RunDetailPage = lazy(() => import('../pages/applications/RunDetailPage'));
const ApplicationOutputsPage = lazy(() => import('../pages/applications/ApplicationOutputsPage'));
const OutputDetailPage = lazy(() => import('../pages/applications/OutputDetailPage'));
const ApplicationChatPage = lazy(() => import('../pages/applications/ApplicationChatPage'));
const ApplicationActivityPage = lazy(() => import('../pages/applications/ApplicationActivityPage'));

const NotFoundPage = lazy(() => import('../pages/NotFoundPage'));

function withSuspense(element: React.ReactNode) {
  return <Suspense fallback={null}>{element}</Suspense>;
}

/**
 * Route table mirrors ASDD §7.2 exactly. All feature pages are placeholder
 * stubs (owning modules replace their contents; the route tree itself does
 * not change). There is no login/authentication in this app - every route
 * is reachable directly.
 */
export const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/platform/dashboard" replace /> },
  {
    element: <RootLayout />,
    children: [
      { path: '/platform/dashboard', element: withSuspense(<PlatformDashboardPage />) },
      { path: '/platform/applications', element: withSuspense(<PlatformApplicationsPage />) },
      { path: '/platform/processing', element: withSuspense(<PlatformProcessingPage />) },
      { path: '/platform/audit', element: withSuspense(<PlatformAuditPage />) },
      { path: '/platform/operations', element: withSuspense(<PlatformOperationsPage />) },

      {
        path: '/applications/:applicationId/overview',
        element: withSuspense(<ApplicationOverviewPage />),
      },
      {
        path: '/applications/:applicationId/knowledge',
        element: withSuspense(<ApplicationKnowledgePage />),
      },
      {
        path: '/applications/:applicationId/knowledge/:fileId',
        element: withSuspense(<KnowledgeFileDetailPage />),
      },
      {
        path: '/applications/:applicationId/processing',
        element: withSuspense(<ApplicationProcessingPage />),
      },
      {
        path: '/applications/:applicationId/agents',
        element: withSuspense(<ApplicationAgentsPage />),
      },
      {
        path: '/applications/:applicationId/agents/:agentId',
        element: withSuspense(<AgentDetailPage />),
      },
      { path: '/applications/:applicationId/runs', element: withSuspense(<ApplicationRunsPage />) },
      {
        path: '/applications/:applicationId/runs/:runId',
        element: withSuspense(<RunDetailPage />),
      },
      {
        path: '/applications/:applicationId/outputs',
        element: withSuspense(<ApplicationOutputsPage />),
      },
      {
        path: '/applications/:applicationId/outputs/:outputId',
        element: withSuspense(<OutputDetailPage />),
      },
      {
        path: '/applications/:applicationId/chat',
        element: withSuspense(<ApplicationChatPage />),
      },
      {
        path: '/applications/:applicationId/chat/:conversationId',
        element: withSuspense(<ApplicationChatPage />),
      },
      {
        path: '/applications/:applicationId/activity',
        element: withSuspense(<ApplicationActivityPage />),
      },
    ],
  },
  { path: '*', element: withSuspense(<NotFoundPage />) },
]);
