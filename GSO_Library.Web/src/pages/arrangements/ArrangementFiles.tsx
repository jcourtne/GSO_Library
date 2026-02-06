import { useRef, useState } from 'react';
import { Alert, Button, Card, ListGroup, ProgressBar, Spinner } from 'react-bootstrap';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import ConfirmModal from '../../components/common/ConfirmModal';
import type { ArrangementFile } from '../../types';

function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function ArrangementFiles() {
  const { id } = useParams<{ id: string }>();
  const arrangementId = Number(id);
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ArrangementFile | null>(null);

  const { data: arrangement, isLoading: loadingArrangement } = useQuery({
    queryKey: ['arrangement', id],
    queryFn: () => arrangementsApi.get(arrangementId),
  });

  const { data: files, isLoading: loadingFiles } = useQuery({
    queryKey: ['arrangement-files', id],
    queryFn: () => arrangementsApi.listFiles(arrangementId),
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => arrangementsApi.uploadFile(arrangementId, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['arrangement-files', id] });
      queryClient.invalidateQueries({ queryKey: ['arrangement', id] });
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
      queryClient.invalidateQueries({ queryKey: ['arrangement-files', id] });
      queryClient.invalidateQueries({ queryKey: ['arrangement', id] });
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

  if (loadingArrangement) return <Spinner animation="border" />;

  return (
    <>
      <h2>Files for: {arrangement?.name}</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Card
        className="mb-4 text-center p-4"
        style={{ border: '2px dashed #ccc', cursor: 'pointer' }}
        onDrop={handleDrop}
        onDragOver={(e) => e.preventDefault()}
        onClick={() => fileInputRef.current?.click()}
      >
        <Card.Body>
          {uploading ? (
            <>
              <Spinner animation="border" className="mb-2" />
              <ProgressBar animated now={100} className="mt-2" />
            </>
          ) : (
            <>
              <p className="mb-1">Drag & drop a file here, or click to browse</p>
              <small className="text-muted">Supported formats: PDF, MIDI, MusicXML, etc.</small>
            </>
          )}
          <input
            type="file"
            ref={fileInputRef}
            className="d-none"
            onChange={handleFileSelect}
          />
        </Card.Body>
      </Card>

      <Card>
        <Card.Body>
          <Card.Title>Uploaded Files</Card.Title>
          {loadingFiles ? (
            <Spinner animation="border" />
          ) : !files?.length ? (
            <p className="text-muted">No files uploaded yet.</p>
          ) : (
            <ListGroup variant="flush">
              {files.map((f) => (
                <ListGroup.Item key={f.id} className="d-flex justify-content-between align-items-center">
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
                    <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(f)}>
                      Delete
                    </Button>
                  </div>
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
