import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { knowledgeFilesQueryKeys, listKnowledgeFiles } from '@/api/knowledgeFiles';
import type { KnowledgeScopeType } from '@/api/agents';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

const READY_STATUS = 2;

export interface KnowledgeScopeFieldProps {
  applicationId: string;
  scopeType: KnowledgeScopeType;
  selectedFileIds: string[];
  onScopeTypeChange: (scopeType: KnowledgeScopeType) => void;
  onSelectedFileIdsChange: (fileIds: string[]) => void;
  disabled?: boolean;
}

/**
 * TASK-AGT-04/TASK-AGT-05: knowledge-scope picker - "all ready + enabled
 * files" (the default) or an explicit selection restricted to ready +
 * enabled files only (FR-AGT-004; the server independently re-validates and
 * excludes anything else - this is convenience, not the enforcement point).
 */
export function KnowledgeScopeField({
  applicationId,
  scopeType,
  selectedFileIds,
  onScopeTypeChange,
  onSelectedFileIdsChange,
  disabled = false,
}: KnowledgeScopeFieldProps) {
  const [search, setSearch] = useState('');

  const filesQuery = useQuery({
    queryKey: knowledgeFilesQueryKeys.list(applicationId),
    queryFn: () => listKnowledgeFiles(applicationId),
    enabled: Boolean(applicationId) && scopeType === 'Selected',
  });

  const selectableFiles = useMemo(
    () => (filesQuery.data ?? []).filter((file) => file.status === READY_STATUS && file.isEnabled),
    [filesQuery.data],
  );

  const filteredFiles = useMemo(() => {
    if (!search.trim()) {
      return selectableFiles;
    }
    const term = search.trim().toLowerCase();
    return selectableFiles.filter((file) => file.displayName.toLowerCase().includes(term));
  }, [selectableFiles, search]);

  function toggleFile(fileId: string) {
    if (selectedFileIds.includes(fileId)) {
      onSelectedFileIdsChange(selectedFileIds.filter((id) => id !== fileId));
    } else {
      onSelectedFileIdsChange([...selectedFileIds, fileId]);
    }
  }

  return (
    <div className="space-y-3">
      <div className="space-y-2">
        <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
          <input
            type="radio"
            name="knowledge-scope-type"
            checked={scopeType === 'AllReadyEnabled'}
            onChange={() => onScopeTypeChange('AllReadyEnabled')}
            disabled={disabled}
            className="size-4 border border-border accent-primary"
          />
          All ready + enabled files
        </label>
        <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
          <input
            type="radio"
            name="knowledge-scope-type"
            checked={scopeType === 'Selected'}
            onChange={() => onScopeTypeChange('Selected')}
            disabled={disabled}
            className="size-4 border border-border accent-primary"
          />
          Selected files only
        </label>
      </div>

      {scopeType === 'Selected' && (
        <div className="space-y-2 rounded-control border border-border p-3">
          <Input
            placeholder="Search files..."
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            disabled={disabled}
            aria-label="Search knowledge files"
          />
          {filesQuery.isLoading ? (
            <Skeleton className="h-24 w-full" />
          ) : filesQuery.isError ? (
            <p role="alert" className="text-sm text-danger">
              Failed to load knowledge files.
            </p>
          ) : filteredFiles.length === 0 ? (
            <p className="text-sm text-text-muted">
              {selectableFiles.length === 0
                ? 'No ready and enabled files yet.'
                : 'No files match your search.'}
            </p>
          ) : (
            <ul className="max-h-64 space-y-1 overflow-y-auto">
              {filteredFiles.map((file) => (
                <li key={file.id}>
                  <label
                    className={cn(
                      'flex items-center gap-2 rounded-control px-2 py-1 text-sm text-text-primary hover:bg-surface-hover',
                      disabled && 'pointer-events-none opacity-50',
                    )}
                  >
                    <input
                      type="checkbox"
                      checked={selectedFileIds.includes(file.id)}
                      onChange={() => toggleFile(file.id)}
                      disabled={disabled}
                      className="size-4 rounded-control border border-border accent-primary"
                    />
                    {file.displayName}
                  </label>
                </li>
              ))}
            </ul>
          )}
          <p className="text-xs text-text-muted">{selectedFileIds.length} file(s) selected</p>
        </div>
      )}
    </div>
  );
}
