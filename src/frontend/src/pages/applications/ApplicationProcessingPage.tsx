import { useParams } from 'react-router-dom';
import { Server } from 'lucide-react';

import { useCapabilities } from '@/app/authz/useCapabilities';
import { useJoinApplicationGroup } from '@/app/realtime/useRealtimeGroups';
import { PageHeader } from '@/components/patterns/PageHeader';
import { ProcessingJobsView } from '@/components/processing/ProcessingJobsView';

/**
 * TASK-PRDSH-02: `/applications/:applicationId/processing` - the
 * application-scoped processing-job listing. Retry uses `canManageKnowledge`
 * (SuperAdmin or this application's ApplicationAdmin), matching the server's
 * `CanManageKnowledge` policy on the retry endpoint.
 */
export default function ApplicationProcessingPage() {
  const { applicationId } = useParams<{ applicationId: string }>();
  const capabilities = useCapabilities(applicationId);

  // TASK-SIG-05/06: joins `application:{id}` so File* events invalidate this list live.
  useJoinApplicationGroup(applicationId);

  return (
    <div aria-labelledby="application-processing-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Application"
        icon={Server}
        iconTone="processing"
        title="Processing"
        description="Embedding and chunking jobs that turn your uploaded files into retrievable knowledge."
      />
      <ProcessingJobsView
        scope={{ applicationId: applicationId ?? '' }}
        canRetry={capabilities.canManageKnowledge}
      />
    </div>
  );
}
