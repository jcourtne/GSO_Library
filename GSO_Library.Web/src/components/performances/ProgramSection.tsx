import { useRef, useState } from 'react';
import { Alert, Button, Card, ListGroup, ProgressBar, Spinner } from 'react-bootstrap';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { performancesApi } from '../../api/performances';
import ConfirmModal from '../common/ConfirmModal';
import { formatFileSize } from '../../utils/fileCategories';
import type { PerformanceFile } from '../../types';

interface ProgramSectionProps {
  files: PerformanceFile[];
  performanceId: number;
  editable: boolean;
}

export default function ProgramSection({ files, performanceId, editable }: ProgramSectionProps) {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<PerformanceFile | null>(null);

  const invalidateFiles = () => {
    queryClient.invalidateQueries({ queryKey: ['performance-files', performanceId] });
  };

  const uploadMutation = useMutation({
    mutationFn: (file: File) => performancesApi.uploadFile(performanceId, file),
    onSuccess: () => {
      invalidateFiles();
      setUploading(false);
    },
    onError: () => {
      setError('Failed to upload file. Only PDF files up to 50MB are allowed.');
      setUploading(false);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (fileId: number) => performancesApi.deleteFile(performanceId, fileId),
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

  const handleDownload = async (file: PerformanceFile) => {
    try {
      const response = await performancesApi.downloadFile(performanceId, file.id);
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
          <Card.Title>Concert Programs</Card.Title>

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
                <small className="text-muted">Drag & drop or click to upload PDF</small>
              )}
              <input
                type="file"
                ref={fileInputRef}
                className="d-none"
                accept=".pdf"
                onChange={handleFileSelect}
              />
            </Card>
          )}

          {files.length === 0 ? (
            <p className="text-muted mb-0">No programs uploaded</p>
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
                      <Button size="sm" variant="outline-primary" className="me-2" onClick={() => handleDownload(f)}>
                        Download
                      </Button>
                      {editable && (
                        <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(f)}>
                          Delete
                        </Button>
                      )}
                    </div>
                  </div>
                </ListGroup.Item>
              ))}
            </ListGroup>
          )}
        </Card.Body>
      </Card>

      <ConfirmModal
        show={!!deleteTarget}
        title="Delete Program"
        message={`Are you sure you want to delete "${deleteTarget?.fileName}"?`}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </>
  );
}
