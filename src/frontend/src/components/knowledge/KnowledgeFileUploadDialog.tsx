import { useRef, useState } from 'react';
import { UploadIcon, XIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { formatFileSize } from '@/lib/utils';
import type { UploadFileResult } from '@/api/knowledgeFiles';

export interface KnowledgeFileUploadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpload: (files: File[]) => void;
  isPending?: boolean;
  /** Per-file outcomes from the most recently completed upload (FR-KNW-002), rendered until the dialog is reopened/closed. */
  results?: UploadFileResult[] | null;
}

/**
 * TASK-KNW-06: multi-file upload dialog (FR-KNW-002). Uses a native
 * `<input type="file" multiple>` (no drag-and-drop precedent exists
 * elsewhere in the app yet) with a staged file list the user can trim
 * before submitting; per-file accepted/warning/rejected outcomes from the
 * server are shown after upload completes.
 */
export function KnowledgeFileUploadDialog({
  open,
  onOpenChange,
  onUpload,
  isPending = false,
  results,
}: KnowledgeFileUploadDialogProps) {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const inputRef = useRef<HTMLInputElement>(null);

  function handleFilesSelected(fileList: FileList | null) {
    if (!fileList) {
      return;
    }
    setSelectedFiles((current) => [...current, ...Array.from(fileList)]);
    if (inputRef.current) {
      inputRef.current.value = '';
    }
  }

  function removeFile(index: number) {
    setSelectedFiles((current) => current.filter((_, i) => i !== index));
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      setSelectedFiles([]);
    }
    onOpenChange(nextOpen);
  }

  function handleSubmit() {
    if (selectedFiles.length === 0) {
      return;
    }
    onUpload(selectedFiles);
    setSelectedFiles([]);
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Upload knowledge files</DialogTitle>
          <DialogDescription>
            Add one or more text/markdown files. Each file is validated and processed independently.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <input
            ref={inputRef}
            type="file"
            multiple
            onChange={(event) => handleFilesSelected(event.target.files)}
            className="sr-only"
            aria-hidden="true"
            tabIndex={-1}
          />

          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={isPending}
            className="flex w-full flex-col items-center justify-center gap-2 rounded-control border border-dashed border-border bg-surface px-4 py-6 text-center transition-colors hover:border-primary hover:bg-surface-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-60"
          >
            <UploadIcon className="size-6 text-text-muted" />
            <span className="text-sm font-medium text-text-primary">Choose files</span>
            <span className="text-xs text-text-muted">
              Click to browse for text or markdown files
            </span>
          </button>

          {selectedFiles.length > 0 && (
            <ul className="max-h-48 space-y-1 overflow-y-auto">
              {selectedFiles.map((file, index) => (
                <li
                  key={`${file.name}-${index}`}
                  className="flex items-center justify-between gap-2 rounded-control border border-border px-3 py-2 text-sm"
                >
                  <span className="truncate">{file.name}</span>
                  <div className="flex shrink-0 items-center gap-2">
                    <span className="text-text-muted">{formatFileSize(file.size)}</span>
                    <button
                      type="button"
                      aria-label={`Remove ${file.name}`}
                      onClick={() => removeFile(index)}
                      disabled={isPending}
                      className="text-text-muted hover:text-text-primary"
                    >
                      <XIcon className="size-4" />
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}

          {results && results.length > 0 && (
            <ul className="space-y-1 text-sm">
              {results.map((result) => (
                <li key={result.fileName} className="flex items-center justify-between gap-2">
                  <span className="truncate">{result.fileName}</span>
                  <span
                    className={
                      result.outcome === 0
                        ? 'text-success'
                        : result.outcome === 1
                          ? 'text-warning'
                          : 'text-danger'
                    }
                  >
                    {result.outcome === 0 ? 'Accepted' : (result.reason ?? 'Rejected')}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)} disabled={isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={isPending || selectedFiles.length === 0}>
            <UploadIcon />
            Upload {selectedFiles.length > 0 ? `(${selectedFiles.length})` : ''}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
