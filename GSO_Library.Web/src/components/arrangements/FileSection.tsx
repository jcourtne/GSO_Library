import { useRef, useState } from 'react';
import { Alert, Button, Card, ListGroup, ProgressBar, Spinner } from 'react-bootstrap';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import ConfirmModal from '../common/ConfirmModal';
import AudioPlayer from './AudioPlayer';
import { formatFileSize, isBrowserPlayable } from '../../utils/fileCategories';
import type { ArrangementFile } from '../../types';

interface FileSectionProps {
  title: string;
  files: ArrangementFile[];
  arrangementId: number;
  editable: boolean;
  accept?: string;
  canDownload?: boolean;
}

export default function FileSection({ title, files, arrangementId, editable, accept, canDownload = true }: FileSectionProps) {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ArrangementFile | null>(null);
  const arrangementIdStr = String(arrangementId);

  const invalidateFiles = () => {
    queryClient.invalidateQueries({ queryKey: ['arrangement-files', arrangementIdStr] });
    queryClient.invalidateQueries({ queryKey: ['arrangement', arrangementIdStr] });
  };

  const uploadMutation = useMutation({
    mutationFn: (file: File) => arrangementsApi.uploadFile(arrangementId, file),
    onSuccess: () => {
      invalidateFiles();
      setUploading(false);
    },
    onError: () => {
      setError('Failed to upload file. Check file size and type.');
      setUploading(false);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (fileId: number) => arrangementsApi.deleteFile(arrangementId, fileId),
    onSuccess: () => {
      invalidateFiles();
      setDeleteTarget(null);
    },
    onError: () => setError('Failed to delete file'),
  });

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setError('');
    setUploading(true);
    uploadMutation.mutate(file);
    e.target.value = '';
  };

  const handleDownload = async (file: ArrangementFile) => {
    try {
      const response = await arrangementsApi.downloadFile(arrangementId, file.id);
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      link.href = url;
      link.download = file.fileName;
      link.click();
      window.URL.revokeObjectURL(url);
    } catch {
      setError('Failed to download file');
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    const file = e.dataTransfer.files[0];
    if (!file) return;
    setError('');
    setUploading(true);
    uploadMutation.mutate(file);
  };

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Card className="mb-3">
        <Card.Body>
          <Card.Title>{title}</Card.Title>

          {editable && (
            <Card
              className="mb-3 text-center p-3"
              style={{ border: '2px dashed #ccc', cursor: 'pointer' }}
              onDrop={handleDrop}
              onDragOver={(e) => e.preventDefault()}
              onClick={() => fileInputRef.current?.click()}
            >
              {uploading ? (
                <>
                  <Spinner animation="border" size="sm" className="mb-2" />
                  <ProgressBar animated now={100} className="mt-2" />
                </>
              ) : (
                <small className="text-muted">Drag & drop or click to upload</small>
              )}
              <input
                type="file"
                ref={fileInputRef}
                className="d-none"
                accept={accept}
                onChange={handleFileSelect}
              />
            </Card>
          )}

          {files.length === 0 ? (
            <p className="text-muted mb-0">No files</p>
          ) : (
            <ListGroup variant="flush">
              {files.map((f) => (
                <ListGroup.Item key={f.id}>
                  <div className="d-flex justify-content-between align-items-center">
                    <div>
                      <span className="fw-medium">{f.fileName}</span>
                      <small className="text-muted ms-2">
                        ({formatFileSize(f.fileSize)}) &middot; {new Date(f.uploadedAt).toLocaleDateString()}
                      </small>
                    </div>
                    <div>
                      {canDownload && (
                        <Button size="sm" variant="outline-primary" className="me-2" onClick={() => handleDownload(f)}>
                          Download
                        </Button>
                      )}
                      {editable && (
                        <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(f)}>
                          Delete
                        </Button>
                      )}
                    </div>
                  </div>
                  {canDownload && isBrowserPlayable(f.fileName) && (
                    <AudioPlayer arrangementId={arrangementId} file={f} />
                  )}
                </ListGroup.Item>
              ))}
            </ListGroup>
          )}
        </Card.Body>
      </Card>

      <ConfirmModal
        show={!!deleteTarget}
        title="Delete File"
        message={`Are you sure you want to delete "${deleteTarget?.fileName}"?`}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </>
  );
}
