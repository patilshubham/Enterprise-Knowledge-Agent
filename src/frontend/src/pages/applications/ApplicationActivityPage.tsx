import { useParams } from 'react-router-dom';
import { History } from 'lucide-react';

import { AuditEventsView } from '@/components/audit/AuditEventsView';
import { PageHeader } from '@/components/patterns/PageHeader';

/**
 * TASK-AUD-03: `/applications/:applicationId/activity` - audit-event search
 * scoped to one application. Still gated by the platform-only `CanViewAudit`
 * policy server-side (no per-application variant exists - see
 * `AuditEndpoints.cs`), so this is only reachable/useful for SuperAdmin/
 * SuperViewer even though the route is nested under an application.
 */
export default function ApplicationActivityPage() {
  const { applicationId } = useParams<{ applicationId: string }>();

  return (
    <div aria-labelledby="application-activity-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Application"
        icon={History}
        title="Activity"
        description="A searchable audit trail of everything that has happened in this application."
      />
      <AuditEventsView scope={{ applicationId: applicationId ?? '' }} />
    </div>
  );
}
