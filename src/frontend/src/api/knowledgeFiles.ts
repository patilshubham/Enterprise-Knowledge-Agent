import { apiClient } from './apiClient';

/**
 * Mirrors `UserFacingFileStatus` (src/shared/Knowledge/UserFacingFileStatus.cs).
 * Unlike `ApplicationResponse.status`, `KnowledgeFileResponse.Status` is the
 * raw C# enum (no `.ToString()` mapping in the DTO), so System.Text.Json
 * serializes it as a number by default - verified against live API
 * responses (`"status":2` for Ready, `"status":3` for Failed).
 */
export type KnowledgeFileStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export const KNOWLEDGE_FILE_STATUS_NAMES = [
  'Queued',
  'Processing',
  'Ready',
  'Failed',
  'Disabled',
  'Deleting',
  'Deleted',
] as const;

export type KnowledgeFileStatusName = (typeof KNOWLEDGE_FILE_STATUS_NAMES)[number];

export function knowledgeFileStatusName(status: KnowledgeFileStatus): KnowledgeFileStatusName {
  return KNOWLEDGE_FILE_STATUS_NAMES[status];
}

/** Mirrors `ProcessingStage` (src/shared/Knowledge/ProcessingStage.cs); also serialized as a number. */
export type ProcessingStage = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8;

export const PROCESSING_STAGE_NAMES = [
  'Queued',
  'Downloading',
  'Validating',
  'Parsing',
  'Chunking',
  'Generating embeddings',
  'Saving vectors',
  'Finalizing',
  'Completed',
] as const;

export function processingStageName(stage: ProcessingStage): string {
  return PROCESSING_STAGE_NAMES[stage];
}

/** Mirrors `UploadOutcome` (src/application/Knowledge/UploadFileResult.cs); serialized as a number. */
export type UploadOutcome = 0 | 1 | 2;

export const UPLOAD_OUTCOME_NAMES = ['Accepted', 'Warning', 'Rejected'] as const;

/** Shape of `KnowledgeFileResponse` - see src/application/Knowledge/KnowledgeFileResponse.cs. */
export interface KnowledgeFileResponse {
  id: string;
  applicationId: string;
  displayName: string;
  fileSize: number;
  mimeType: string;
  encoding: string;
  status: KnowledgeFileStatus;
  isEnabled: boolean;
  uploadedBy: string;
  uploadedAt: string;
  lastProcessedAt: string | null;
  stage: ProcessingStage | null;
  progress: number | null;
}

/** Shape of `UploadFileResult` - see src/application/Knowledge/UploadFileResult.cs. */
export interface UploadFileResult {
  fileName: string;
  outcome: UploadOutcome;
  fileId: string | null;
  reason: string | null;
}

export interface ListKnowledgeFilesParams {
  search?: string;
  status?: KnowledgeFileStatus;
}

/** TanStack Query keys for the TASK-KNW-04/05 endpoints. */
export const knowledgeFilesQueryKeys = {
  all: ['knowledgeFiles'] as const,
  list: (applicationId: string, params: ListKnowledgeFilesParams = {}) =>
    [...knowledgeFilesQueryKeys.all, 'list', applicationId, params] as const,
  preview: (applicationId: string, fileId: string) =>
    [...knowledgeFilesQueryKeys.all, 'preview', applicationId, fileId] as const,
};

/** TASK-KNW-04: `GET /api/applications/{appId}/files?search=&status=` - filtered listing with live stage/progress. */
export function listKnowledgeFiles(
  applicationId: string,
  params: ListKnowledgeFilesParams = {},
): Promise<KnowledgeFileResponse[]> {
  const query = new URLSearchParams();
  if (params.search) {
    query.set('search', params.search);
  }
  if (params.status !== undefined) {
    query.set('status', knowledgeFileStatusName(params.status));
  }
  const qs = query.toString();
  return apiClient.get<KnowledgeFileResponse[]>(
    `/api/applications/${applicationId}/files${qs ? `?${qs}` : ''}`,
  );
}

/** TASK-KNW-03: `POST /api/applications/{appId}/files` - multipart upload; each file validated/stored independently. */
export function uploadKnowledgeFiles(
  applicationId: string,
  files: File[],
  allowDuplicates = false,
): Promise<UploadFileResult[]> {
  const form = new FormData();
  for (const file of files) {
    form.append('files', file);
  }
  const qs = allowDuplicates ? '?allowDuplicates=true' : '';
  return apiClient.post<UploadFileResult[]>(`/api/applications/${applicationId}/files${qs}`, form);
}

/** TASK-KNW-04: `GET /api/applications/{appId}/files/{fileId}/preview` - decoded source text. */
export function previewKnowledgeFile(applicationId: string, fileId: string): Promise<string> {
  return apiClient.getText(`/api/applications/${applicationId}/files/${fileId}/preview`);
}

/** TASK-KNW-05: `POST .../files/{fileId}/disable` - excludes from retrieval, remains admin-visible. */
export function disableKnowledgeFile(
  applicationId: string,
  fileId: string,
): Promise<KnowledgeFileResponse> {
  return apiClient.post<KnowledgeFileResponse>(
    `/api/applications/${applicationId}/files/${fileId}/disable`,
  );
}

/** TASK-KNW-05: `POST .../files/{fileId}/enable` - re-includes a previously-disabled file. */
export function enableKnowledgeFile(
  applicationId: string,
  fileId: string,
): Promise<KnowledgeFileResponse> {
  return apiClient.post<KnowledgeFileResponse>(
    `/api/applications/${applicationId}/files/${fileId}/enable`,
  );
}

/** TASK-KNW-05: `POST .../files/{fileId}/reprocess` - re-queues a Ready/Failed file, dropping prior chunks. */
export function reprocessKnowledgeFile(
  applicationId: string,
  fileId: string,
): Promise<KnowledgeFileResponse> {
  return apiClient.post<KnowledgeFileResponse>(
    `/api/applications/${applicationId}/files/${fileId}/reprocess`,
  );
}

/** TASK-KNW-05: `DELETE .../files/{fileId}` - marks Deleting and enqueues async S3/vector cleanup; idempotent. */
export function deleteKnowledgeFile(applicationId: string, fileId: string): Promise<void> {
  return apiClient.delete<void>(`/api/applications/${applicationId}/files/${fileId}`);
}
