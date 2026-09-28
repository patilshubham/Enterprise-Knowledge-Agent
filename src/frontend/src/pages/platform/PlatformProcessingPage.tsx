import { Server } from 'lucide-react';

import { useCapabilities } from '@/app/authz/useCapabilities';
import { useJoinOperationsGroup } from '@/app/realtime/useRealtimeGroups';
import { PageHeader } from '@/components/patterns/PageHeader';
import { ProcessingJobsView } from '@/components/processing/ProcessingJobsView';

/**
 * TASK-PRDSH-01: `/platform/processing` - platform-wide processing-job
 * listing across every application (SuperAdmin/SuperViewer only, gated by
 * `RequireCapability`-equivalent route guard upstream + server enforcement).
 * Retry is restricted to SuperAdmin here (rows span many applications, so
 * per-application ApplicationAdmin rights don't apply at this scope - use
 * the per-application Processing page for that).
 */
export default function PlatformProcessingPage() {
  const capabilities = useCapabilities();

  // TASK-PDSH-02/SIG-05: joins `platform:operations` so `DashboardChanged`
  // (the only event reaching this group) refreshes the list on terminal
  // state changes anywhere in the platform.
  useJoinOperationsGroup();

  return (
    <div aria-labelledby="platform-processing-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Platform"
        icon={Server}
        iconTone="processing"
        title="Processing"
        description="Every embedding and chunking job across all applications."
      />
      <ProcessingJobsView scope={{ platform: true }} canRetry={capabilities.isSuperAdmin} />
    </div>
  );
}
