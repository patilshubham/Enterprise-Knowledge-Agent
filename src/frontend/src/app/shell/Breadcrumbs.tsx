import { Fragment } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import { applicationNavItems, platformNavItems, resolveApplicationPath } from './nav-items';
import { useApplicationName } from './useApplications';

interface Crumb {
  label: string;
  path?: string;
}

/**
 * TASK-UX-04: derives the breadcrumb trail from the current route rather
 * than per-page config, so every route automatically gets a correct trail
 * without feature pages needing to declare one. The application crumb shows
 * the resolved application NAME (redesign) rather than the raw GUID.
 */
function buildCrumbs(pathname: string, applicationId?: string, applicationName?: string): Crumb[] {
  if (pathname.startsWith('/platform/')) {
    const match = platformNavItems.find((item) => pathname.startsWith(item.path));
    return [{ label: 'Platform' }, { label: match?.label ?? 'Dashboard', path: match?.path }];
  }

  if (applicationId && pathname.startsWith(`/applications/${applicationId}`)) {
    const match = applicationNavItems.find((item) =>
      pathname.startsWith(resolveApplicationPath(item.path, applicationId)),
    );
    const crumbs: Crumb[] = [
      { label: 'Applications', path: '/platform/applications' },
      {
        label: applicationName ?? 'Application',
        path: `/applications/${applicationId}/overview`,
      },
    ];
    if (match) {
      crumbs.push({
        label: match.label,
        path: resolveApplicationPath(match.path, applicationId),
      });
    }
    return crumbs;
  }

  return [{ label: 'Platform' }];
}

export function Breadcrumbs() {
  const location = useLocation();
  const { applicationId } = useParams<{ applicationId?: string }>();
  const applicationName = useApplicationName(applicationId);
  const crumbs = buildCrumbs(location.pathname, applicationId, applicationName);

  return (
    <Breadcrumb>
      <BreadcrumbList>
        {crumbs.map((crumb, index) => {
          const isLast = index === crumbs.length - 1;
          return (
            <Fragment key={`${crumb.label}-${index}`}>
              <BreadcrumbItem>
                {isLast || !crumb.path ? (
                  <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
                ) : (
                  <BreadcrumbLink asChild>
                    <Link to={crumb.path}>{crumb.label}</Link>
                  </BreadcrumbLink>
                )}
              </BreadcrumbItem>
              {!isLast && <BreadcrumbSeparator />}
            </Fragment>
          );
        })}
      </BreadcrumbList>
    </Breadcrumb>
  );
}
