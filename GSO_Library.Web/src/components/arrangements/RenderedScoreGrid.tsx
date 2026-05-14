import { useMemo, useRef, useState } from 'react';
import { Alert, Button, Card, Form, Spinner, Table } from 'react-bootstrap';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import { instrumentSortOrdersApi } from '../../api/instrumentSortOrders';
import ConfirmModal from '../common/ConfirmModal';
import { categorizeFiles, groupScoreFiles, formatFileSize, RENDERED_SCORE_ACCEPT } from '../../utils/fileCategories';
import { SCORE_PART_TYPES } from '../../utils/scorePartTypes';
import type { Arrangement, ArrangementFile, Instrument } from '../../types';

const STOPWORDS = new Set(['in', 'and', 'the', 'a', 'of']);

function tokenize(text: string): Set<string> {
  return new Set(
    text.toLowerCase()
      .replace(/[^a-z0-9]+/g, ' ')
      .trim()
      .split(' ')
      .filter((w) => w.length > 0 && !STOPWORDS.has(w)),
  );
}

function autoMatch(
  unlistedFiles: ArrangementFile[],
  instruments: Instrument[],
): { fileId: number; instrumentId: number }[] {
  const sorted = [...instruments].sort(
    (a, b) => tokenize(b.name).size - tokenize(a.name).size,
  );
  const results: { fileId: number; instrumentId: number }[] = [];
  for (const f of unlistedFiles) {
    const fileTokens = tokenize(f.fileName.replace(/\.[^.]+$/, ''));
    const match = sorted.find((inst) => {
      const instTokens = tokenize(inst.name);
      return instTokens.size > 0 && [...instTokens].every((t) => fileTokens.has(t));
    });
    if (match) results.push({ fileId: f.id, instrumentId: match.id });
  }
  return results;
}

interface Props {
  arrangement: Arrangement;
  files: ArrangementFile[];
  editable: boolean;
  canDownload: boolean;
}

interface Section {
  key: string;
  label: string;
  files: ArrangementFile[];
  scorePartType: string;
  instrumentId: number | null;
}

export default function RenderedScoreGrid({ arrangement, files, editable, canDownload }: Props) {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ArrangementFile | null>(null);
  const [dragOverRow, setDragOverRow] = useState<string | null>(null);
  const [autoSorting, setAutoSorting] = useState(false);
  // undefined = not yet overridden by user; null = user chose Alphabetical; number = user chose a sort order
  const [selectedSortOrderId, setSelectedSortOrderId] = useState<number | null | undefined>(undefined);
  const arrangementIdStr = String(arrangement.id);

  const instruments: Instrument[] = arrangement.instruments ?? [];
  const instrumentIds = new Set(instruments.map((i) => i.id));

  const { data: sortOrders } = useQuery({
    queryKey: ['instrument-sort-orders'],
    queryFn: instrumentSortOrdersApi.list,
  });

  // Effective sort order: user's choice if set, otherwise the default, otherwise null (alphabetical)
  const effectiveSortOrderId = selectedSortOrderId !== undefined
    ? selectedSortOrderId
    : (sortOrders?.find((so) => so.isDefault)?.id ?? null);

  const sortedInstruments = useMemo(() => {
    const selectedOrder = sortOrders?.find((so) => so.id === effectiveSortOrderId);
    if (!selectedOrder) {
      return [...instruments].sort((a, b) => a.name.localeCompare(b.name));
    }
    const positionMap = new Map(selectedOrder.instruments.map((inst, i) => [inst.id, i]));
    return [...instruments].sort((a, b) => {
      const posA = positionMap.get(a.id) ?? Infinity;
      const posB = positionMap.get(b.id) ?? Infinity;
      if (posA !== posB) return posA - posB;
      return a.name.localeCompare(b.name);
    });
  }, [instruments, sortOrders, effectiveSortOrderId]);

  const { renderedScoreFiles } = categorizeFiles(files);
  const grouped = groupScoreFiles(renderedScoreFiles, instrumentIds);

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['arrangement-files', arrangementIdStr] });
    queryClient.invalidateQueries({ queryKey: ['arrangement', arrangementIdStr] });
  };

  const uploadMutation = useMutation({
    mutationFn: (file: File) => arrangementsApi.uploadFile(arrangement.id, file),
  });

  const deleteMutation = useMutation({
    mutationFn: (fileId: number) => arrangementsApi.deleteFile(arrangement.id, fileId),
    onSuccess: () => { invalidate(); setDeleteTarget(null); },
    onError: () => setError('Failed to delete file'),
  });

  const reassignMutation = useMutation({
    mutationFn: ({ fileId, scorePartType, instrumentId }: { fileId: number; scorePartType: string; instrumentId: number | null }) =>
      arrangementsApi.updateFileMetadata(arrangement.id, fileId, scorePartType, instrumentId),
    onSuccess: () => invalidate(),
    onError: () => setError('Failed to move file'),
  });

  const uploadFiles = (filesToUpload: File[]) => {
    if (filesToUpload.length === 0) return;
    setError('');
    setUploading(true);
    Promise.allSettled(filesToUpload.map((f) => uploadMutation.mutateAsync(f)))
      .then((results) => {
        invalidate();
        setUploading(false);
        if (results.some((r) => r.status === 'rejected'))
          setError('One or more files failed to upload.');
      });
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    uploadFiles(Array.from(e.target.files ?? []));
    e.target.value = '';
  };

  const handleUploadDrop = (e: React.DragEvent) => {
    e.preventDefault();
    if (e.dataTransfer.files.length > 0) uploadFiles(Array.from(e.dataTransfer.files));
  };

  const handleRowDrop = (e: React.DragEvent, scorePartType: string, instrumentId: number | null) => {
    e.preventDefault();
    setDragOverRow(null);
    const fileIdStr = e.dataTransfer.getData('fileId');
    if (!fileIdStr) return;
    reassignMutation.mutate({ fileId: parseInt(fileIdStr, 10), scorePartType, instrumentId });
  };

  const handleAutoSort = async () => {
    const matches = autoMatch(grouped.unlisted, instruments);
    if (matches.length === 0) return;
    setAutoSorting(true);
    try {
      await Promise.all(
        matches.map(({ fileId, instrumentId }) =>
          arrangementsApi.updateFileMetadata(arrangement.id, fileId, SCORE_PART_TYPES.INSTRUMENT_PART, instrumentId),
        ),
      );
      invalidate();
    } catch {
      setError('Auto-sort failed for one or more files');
    } finally {
      setAutoSorting(false);
    }
  };

  const handleDownload = async (file: ArrangementFile) => {
    try {
      const response = await arrangementsApi.downloadFile(arrangement.id, file.id);
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

  const sections: Section[] = [
    { key: SCORE_PART_TYPES.CONDUCTOR_SCORE, label: "Conductor's Score", files: grouped.conductorScore, scorePartType: SCORE_PART_TYPES.CONDUCTOR_SCORE, instrumentId: null },
    ...sortedInstruments.map((inst) => ({
      key: `instrument_${inst.id}`,
      label: inst.name,
      files: grouped.byInstrument.get(inst.id) ?? [],
      scorePartType: SCORE_PART_TYPES.INSTRUMENT_PART,
      instrumentId: inst.id,
    })),
    { key: SCORE_PART_TYPES.PERCUSSION_PART, label: 'Percussion (Generic)', files: grouped.percussion, scorePartType: SCORE_PART_TYPES.PERCUSSION_PART, instrumentId: null },
    { key: SCORE_PART_TYPES.UNLISTED_PART, label: 'Unlisted Parts', files: grouped.unlisted, scorePartType: SCORE_PART_TYPES.UNLISTED_PART, instrumentId: null },
  ];

  const visibleSections = editable ? sections : sections.filter((s) => s.files.length > 0);

  if (renderedScoreFiles.length === 0 && !editable) return null;

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Card className="mb-3">
        <Card.Body>
          <div className="d-flex justify-content-between align-items-center mb-2">
            <Card.Title className="mb-0">Rendered Score/Parts</Card.Title>
            <div className="d-flex align-items-center gap-2">
              {editable && sortOrders && sortOrders.length > 0 && (
                <Form.Select
                  size="sm"
                  style={{ width: 'auto' }}
                  value={effectiveSortOrderId ?? ''}
                  onChange={(e) => setSelectedSortOrderId(e.target.value ? Number(e.target.value) : null)}
                >
                  <option value="">Alphabetical</option>
                  {sortOrders.map((so) => (
                    <option key={so.id} value={so.id}>
                      {so.isDefault ? `★ ${so.name}` : so.name}
                    </option>
                  ))}
                </Form.Select>
              )}
              {editable && grouped.unlisted.length > 0 && (
                <Button size="sm" variant="outline-secondary" onClick={handleAutoSort} disabled={autoSorting}>
                  {autoSorting ? <><Spinner animation="border" size="sm" className="me-1" />Sorting...</> : 'Auto-sort'}
                </Button>
              )}
            </div>
          </div>

          <Table bordered size="sm" className="mb-0">
            <tbody>
              {visibleSections.map(({ key, label, files: sectionFiles, scorePartType, instrumentId }) => (
                <tr
                  key={key}
                  onDragOver={editable ? (e) => { e.preventDefault(); setDragOverRow(key); } : undefined}
                  onDragLeave={editable ? () => setDragOverRow(null) : undefined}
                  onDrop={editable ? (e) => handleRowDrop(e, scorePartType, instrumentId) : undefined}
                >
                  <td
                    style={{
                      width: '25%', verticalAlign: 'top', whiteSpace: 'nowrap',
                      fontWeight: dragOverRow === key ? 700 : 500,
                      backgroundColor: dragOverRow === key ? '#cfe2ff' : undefined,
                      transition: 'background-color 0.1s ease',
                    }}
                    className="py-2 px-3"
                  >
                    {label}
                  </td>
                  <td className="py-1 px-2">
                    {sectionFiles.length === 0 ? (
                      <span className="text-muted small fst-italic">
                        {editable ? 'Drop files here' : '—'}
                      </span>
                    ) : (
                      sectionFiles.map((f) => (
                        <div
                          key={f.id}
                          draggable={editable}
                          onDragStart={editable ? (e) => e.dataTransfer.setData('fileId', String(f.id)) : undefined}
                          className="d-flex justify-content-between align-items-center px-2 py-1 mb-1 rounded border bg-light"
                          style={editable ? { cursor: 'grab' } : undefined}
                        >
                          <span className="small me-2">
                            {f.fileName}
                            <span className="text-muted ms-1">({formatFileSize(f.fileSize)})</span>
                          </span>
                          <div className="d-flex gap-1 flex-shrink-0">
                            {canDownload && !editable && (
                              <Button size="sm" variant="outline-primary" onClick={() => handleDownload(f)}>
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
                      ))
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>

          {editable && (
            <div className="mt-3">
              <input
                type="file"
                ref={fileInputRef}
                className="d-none"
                accept={RENDERED_SCORE_ACCEPT}
                multiple
                onChange={handleFileSelect}
              />
              <Card
                className="text-center p-3"
                style={{ border: '2px dashed #ccc', cursor: 'pointer' }}
                onDrop={handleUploadDrop}
                onDragOver={(e) => e.preventDefault()}
                onClick={() => fileInputRef.current?.click()}
              >
                {uploading ? (
                  <>
                    <Spinner animation="border" size="sm" className="mb-1" />
                    <div><small className="text-muted">Uploading...</small></div>
                  </>
                ) : (
                  <small className="text-muted">Drag & drop or click to upload new files</small>
                )}
              </Card>
            </div>
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
