import { apiClient } from './apiClient';

/** Mirrors `Application.Status` (src/domain/Applications/Application.cs). */
export type ApplicationStatus = 'Active' | 'Archived' | 'Deleted';

/** Shape of `ApplicationResponse` - see src/application/Applications/ApplicationDtos.cs. */
export interface ApplicationResponse {
  id: string;
  name: string;
  description: string | null;
  status: ApplicationStatus;
  allowViewerExecution: boolean;
  createdBy: string;
  createdAt: string;
  updatedAt: string | null;
  archivedAt: string | null;
  deletedAt: string | null;
}

export interface CreateApplicationRequest {
  name: string;
  description?: string | null;
  allowViewerExecution?: boolean;
}

export interface UpdateApplicationRequest {
  name: string;
  description?: string | null;
  allowViewerExecution: boolean;
}

/** TanStack Query keys for the TASK-APP-02 endpoints, shared by the list and overview pages. */
export const applicationsQueryKeys = {
  all: ['applications'] as const,
  list: () => [...applicationsQueryKeys.all, 'list'] as const,
  detail: (applicationId: string) =>
    [...applicationsQueryKeys.all, 'detail', applicationId] as const,
};

/** TASK-APP-02: `GET /api/applications` - scoped server-side to what the caller may see. */
export function listApplications(): Promise<ApplicationResponse[]> {
  return apiClient.get<ApplicationResponse[]>('/api/applications');
}

/** TASK-APP-02: `GET /api/applications/{id}` - 404s (not 403) for applications the caller can't view. */
export function getApplication(applicationId: string): Promise<ApplicationResponse> {
  return apiClient.get<ApplicationResponse>(`/api/applications/${applicationId}`);
}

/** TASK-APP-02: `POST /api/applications` - requires `CanManageApplications` (SuperAdmin only). */
export function createApplication(request: CreateApplicationRequest): Promise<ApplicationResponse> {
  return apiClient.post<ApplicationResponse>('/api/applications', request);
}

/** TASK-APP-02/06: `PUT /api/applications/{id}` - also persists the viewer-execution flag. */
export function updateApplication(
  applicationId: string,
  request: UpdateApplicationRequest,
): Promise<ApplicationResponse> {
  return apiClient.put<ApplicationResponse>(`/api/applications/${applicationId}`, request);
}

/** TASK-APP-02: `POST /api/applications/{id}/archive` - 409s if not currently Active. */
export function archiveApplication(applicationId: string): Promise<ApplicationResponse> {
  return apiClient.post<ApplicationResponse>(`/api/applications/${applicationId}/archive`);
}

/** TASK-APP-02: `POST /api/applications/{id}/restore` - 409s if not currently Archived. */
export function restoreApplication(applicationId: string): Promise<ApplicationResponse> {
  return apiClient.post<ApplicationResponse>(`/api/applications/${applicationId}/restore`);
}

/** TASK-APP-02: `DELETE /api/applications/{id}` - idempotent soft-delete; historical outputs remain viewable (FR-CIT-004). */
export function deleteApplication(applicationId: string): Promise<ApplicationResponse> {
  return apiClient.delete<ApplicationResponse>(`/api/applications/${applicationId}`);
}
