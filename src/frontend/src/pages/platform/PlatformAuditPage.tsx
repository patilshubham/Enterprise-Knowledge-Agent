import { ScrollText } from 'lucide-react';

import { AuditEventsView } from '@/components/audit/AuditEventsView';
import { PageHeader } from '@/components/patterns/PageHeader';

/**
 * TASK-AUD-03: `/platform/audit` - platform-wide audit-event search across
 * every application (SuperAdmin/SuperViewer only, matching `CanViewAudit`).
 */
export default function PlatformAuditPage() {
  return (
    <div aria-labelledby="platform-audit-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Platform"
        icon={ScrollText}
        title="Audit"
        description="A searchable, tamper-evident trail of every action across all applications."
      />
      <AuditEventsView scope={{ platform: true }} />
    </div>
  );
}
