import { useMemo, useRef, useState } from 'react';
import { Alert, Badge, Button, Card, Col, Form, Row, Spinner } from 'react-bootstrap';
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
  checkCanModifyFiles?: () => boolean;
}

interface Section {
  type: 'row' | 'header';
  key: string;
  label: string;
  files: ArrangementFile[];
  scorePartType: string;
  instrumentId: number | null;
  indented?: boolean;
}

function filterVisible(sections: Section[]): Section[] {
  const out: Section[] = [];
  let pending: Section | null = null;
  for (const s of sections) {
    if (s.type === 'header') {
      pending = s;
    } else if (s.files.length > 0) {
      if (pending) { out.push(pending); pending = null; }
      out.push(s);
    }
  }
  return out;
}

export default function RenderedScoreGrid({ arrangement, files, editable, canDownload, checkCanModifyFiles = () => true }: Props) {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ArrangementFile | null>(null);
  const [dragOverRow, setDragOverRow] = useState<string | null>(null);
  const [autoSorting, setAutoSorting] = useState(false);
  const [selectedSortOrderId, setSelectedSortOrderId] = useState<number | null | undefined>(undefined);
  const arrangementIdStr = String(arrangement.id);

  const instruments: Instrument[] = arrangement.instruments ?? [];
  const instrumentIds = new Set(instruments.map((i) => i.id));
  const instrumentNameMap = new Map(instruments.map((i) => [i.id, i.name]));

  const { data: sortOrders } = useQuery({
    queryKey: ['instrument-sort-orders'],
    queryFn: instrumentSortOrdersApi.list,
  });

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

  const { renderedScoreFiles: rawRenderedScoreFiles } = categorizeFiles(files);
  const renderedScoreFiles = [...rawRenderedScoreFiles].sort((a, b) => a.fileName.localeCompare(b.fileName));
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
    mutationFn: ({ fileId, scorePartType, instrumentIds: ids }: { fileId: number; scorePartType: string; instrumentIds: number[] }) =>
      arrangementsApi.updateFileMetadata(arrangement.id, fileId, scorePartType, ids),
    onSuccess: () => invalidate(),
    onError: () => setError('Failed to move file'),
  });

  const uploadFiles = (filesToUpload: File[]) => {
    if (filesToUpload.length === 0) return;
    if (!checkCanModifyFiles()) return;
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

  const isNamedSection = (type: string | null | undefined) =>
    type === SCORE_PART_TYPES.CONDUCTOR_SCORE
    || type === SCORE_PART_TYPES.PERCUSSION_PART
    || type === SCORE_PART_TYPES.VOICE_PART;

  const handleRowDrop = (e: React.DragEvent, scorePartType: string, instrumentId: number | null) => {
    e.preventDefault();
    setDragOverRow(null);
    if (!checkCanModifyFiles()) return;
    const fileIdStr = e.dataTransfer.getData('fileId');
    if (!fileIdStr) return;
    const fileId = parseInt(fileIdStr, 10);
    const file = renderedScoreFiles.find((f) => f.id === fileId);
    if (!file) return;

    if (instrumentId !== null) {
      // Adding to an instrument: preserve any existing generic section assignment
      const newIds = file.instrumentIds.includes(instrumentId)
        ? file.instrumentIds
        : [...file.instrumentIds, instrumentId];
      const newType = isNamedSection(file.scorePartType) ? file.scorePartType : SCORE_PART_TYPES.INSTRUMENT_PART;
      reassignMutation.mutate({ fileId, scorePartType: newType, instrumentIds: newIds });
    } else {
      // Adding to a generic section: preserve existing instrument assignments
      reassignMutation.mutate({ fileId, scorePartType, instrumentIds: file.instrumentIds });
    }
  };

  const handleRemoveFromInstrument = (fileId: number, instrumentId: number) => {
    if (!checkCanModifyFiles()) return;
    const file = renderedScoreFiles.find((f) => f.id === fileId);
    if (!file) return;
    const newIds = file.instrumentIds.filter((id) => id !== instrumentId);
    // If still in a named generic section, keep that type; otherwise fall back to unlisted
    const newType = (newIds.length === 0 && !isNamedSection(file.scorePartType))
      ? SCORE_PART_TYPES.UNLISTED_PART
      : (file.scorePartType ?? SCORE_PART_TYPES.INSTRUMENT_PART);
    reassignMutation.mutate({ fileId, scorePartType: newType, instrumentIds: newIds });
  };

  const handleRemoveFromSection = (fileId: number) => {
    if (!checkCanModifyFiles()) return;
    const file = renderedScoreFiles.find((f) => f.id === fileId);
    if (!file) return;
    // Keep any instrument assignments; only clear the generic section type
    const newType = file.instrumentIds.length > 0
      ? SCORE_PART_TYPES.INSTRUMENT_PART
      : SCORE_PART_TYPES.UNLISTED_PART;
    reassignMutation.mutate({ fileId, scorePartType: newType, instrumentIds: file.instrumentIds });
  };

  const handleAutoSort = async () => {
    if (!checkCanModifyFiles()) return;
    const matches = autoMatch(grouped.unlisted, instruments);
    if (matches.length === 0) return;
    setAutoSorting(true);
    try {
      await Promise.all(
        matches.map(({ fileId, instrumentId }) =>
          arrangementsApi.updateFileMetadata(arrangement.id, fileId, SCORE_PART_TYPES.INSTRUMENT_PART, [instrumentId]),
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

  const sections: Section[] = (() => {
    const result: Section[] = [
      { type: 'row', key: 'conductor_score', label: "Conductor's Score", files: grouped.conductorScore, scorePartType: SCORE_PART_TYPES.CONDUCTOR_SCORE, instrumentId: null },
    ];

    const familyGroups = new Map<string, Instrument[]>();
    const noFamilyInstruments: Instrument[] = [];
    for (const inst of sortedInstruments) {
      if (inst.familyName) {
        if (!familyGroups.has(inst.familyName)) familyGroups.set(inst.familyName, []);
        familyGroups.get(inst.familyName)!.push(inst);
      } else {
        noFamilyInstruments.push(inst);
      }
    }

    let percussionHandled = false;
    let voiceHandled = false;

    for (const [familyName, familyInstruments] of familyGroups) {
      result.push({ type: 'header', key: `family_${familyName}`, label: familyName, files: [], scorePartType: '', instrumentId: null });
      for (const inst of familyInstruments) {
        result.push({ type: 'row', key: `instrument_${inst.id}`, label: inst.name, files: grouped.byInstrument.get(inst.id) ?? [], scorePartType: SCORE_PART_TYPES.INSTRUMENT_PART, instrumentId: inst.id });
      }
      if (familyName === 'Percussion') {
        result.push({ type: 'row', key: 'percussion_part', label: 'Percussion (Generic)', files: grouped.percussion, scorePartType: SCORE_PART_TYPES.PERCUSSION_PART, instrumentId: null, indented: true });
        percussionHandled = true;
      }
      if (familyName === 'Voice') {
        result.push({ type: 'row', key: 'voice_part', label: 'Voice (Generic)', files: grouped.voice, scorePartType: SCORE_PART_TYPES.VOICE_PART, instrumentId: null, indented: true });
        voiceHandled = true;
      }
    }

    for (const inst of noFamilyInstruments) {
      result.push({ type: 'row', key: `instrument_${inst.id}`, label: inst.name, files: grouped.byInstrument.get(inst.id) ?? [], scorePartType: SCORE_PART_TYPES.INSTRUMENT_PART, instrumentId: inst.id });
    }

    if (!percussionHandled) {
      result.push({ type: 'header', key: 'family_Percussion', label: 'Percussion', files: [], scorePartType: '', instrumentId: null });
      result.push({ type: 'row', key: 'percussion_part', label: 'Percussion (Generic)', files: grouped.percussion, scorePartType: SCORE_PART_TYPES.PERCUSSION_PART, instrumentId: null, indented: true });
    }
    if (!voiceHandled) {
      result.push({ type: 'header', key: 'family_Voice', label: 'Voice', files: [], scorePartType: '', instrumentId: null });
      result.push({ type: 'row', key: 'voice_part', label: 'Voice (Generic)', files: grouped.voice, scorePartType: SCORE_PART_TYPES.VOICE_PART, instrumentId: null, indented: true });
    }

    return result;
  })();

  const visibleSections = editable ? sections : filterVisible(sections);

  if (renderedScoreFiles.length === 0 && !editable) return null;

  const getFileAssignmentLabel = (file: ArrangementFile): string | null => {
    if (file.scorePartType === SCORE_PART_TYPES.CONDUCTOR_SCORE) return "Conductor's Score";
    if (file.scorePartType === SCORE_PART_TYPES.PERCUSSION_PART) return 'Percussion';
    if (file.scorePartType === SCORE_PART_TYPES.VOICE_PART) return 'Voice';
    return null;
  };

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Card className="mb-3">
        <Card.Body>
          <div className="d-flex justify-content-between align-items-center mb-3">
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

          <Row>
            {/* Left column: instrument sections */}
            <Col md={6} style={{ borderRight: '1px solid var(--bs-border-color)' }}>
              <div className="small fw-semibold text-muted text-uppercase mb-2" style={{ letterSpacing: '0.05em' }}>Sections</div>
              {visibleSections.map((section) => {
                if (section.type === 'header') {
                  return (
                    <div key={section.key} className="fw-semibold py-1 px-2 mb-1 rounded" style={{ background: 'var(--bs-light)', fontSize: '0.95rem', color: '#495057' }}>
                      {section.label}
                    </div>
                  );
                }
                const { key, label, files: sectionFiles, scorePartType, instrumentId } = section;
                const isOver = dragOverRow === key;
                return (
                  <div
                    key={key}
                    className={`mb-1 rounded px-2 py-1${(instrumentId !== null || section.indented) ? ' ms-3' : ''}`}
                    style={{
                      border: `1px solid ${isOver ? '#86b7fe' : 'var(--bs-border-color)'}`,
                      backgroundColor: isOver ? '#cfe2ff' : undefined,
                      transition: 'background-color 0.1s ease, border-color 0.1s ease',
                      minHeight: 36,
                    }}
                    onDragOver={editable ? (e) => { e.preventDefault(); setDragOverRow(key); } : undefined}
                    onDragLeave={editable ? () => setDragOverRow(null) : undefined}
                    onDrop={editable ? (e) => handleRowDrop(e, scorePartType, instrumentId) : undefined}
                  >
                    <div className="small fw-medium mb-1" style={{ color: isOver ? '#084298' : undefined }}>
                      {label}
                    </div>
                    {sectionFiles.length === 0 ? (
                      <div className="text-muted" style={{ fontSize: '0.75rem', fontStyle: 'italic' }}>
                        {editable ? 'Drop files here' : '—'}
                      </div>
                    ) : (
                      sectionFiles.map((f) => (
                        <div
                          key={f.id}
                          className="d-flex justify-content-between align-items-center px-2 py-1 mb-1 rounded border bg-white"
                          style={{ fontSize: '0.8rem' }}
                        >
                          <span className="me-1" style={{ wordBreak: 'break-word', minWidth: 0 }}>{f.fileName}</span>
                          {editable && (
                            <Button
                              size="sm"
                              variant="link"
                              className="p-0 text-danger flex-shrink-0"
                              style={{ fontSize: '0.75rem', lineHeight: 1 }}
                              onClick={() => instrumentId !== null
                                ? handleRemoveFromInstrument(f.id, instrumentId)
                                : handleRemoveFromSection(f.id)
                              }
                              title="Remove from this section"
                            >
                              ×
                            </Button>
                          )}
                        </div>
                      ))
                    )}
                  </div>
                );
              })}
            </Col>

            {/* Right column: files split by sort status */}
            <Col md={6}>
              {renderedScoreFiles.length === 0 ? (
                <div className="text-muted small fst-italic">No files uploaded yet</div>
              ) : (() => {
                const unlistedFiles = renderedScoreFiles.filter((f) => f.scorePartType === SCORE_PART_TYPES.UNLISTED_PART || !f.scorePartType);
                const assignedFiles = renderedScoreFiles.filter((f) => f.scorePartType && f.scorePartType !== SCORE_PART_TYPES.UNLISTED_PART);

                const renderFileCard = (f: ArrangementFile) => {
                  const assignmentLabel = getFileAssignmentLabel(f);
                  const assignedInstrumentIds = f.instrumentIds.filter((id) => instrumentIds.has(id));
                  return (
                    <div
                      key={f.id}
                      draggable={editable}
                      onDragStart={editable ? (e) => e.dataTransfer.setData('fileId', String(f.id)) : undefined}
                      className="d-flex flex-column px-2 py-1 mb-1 rounded border bg-light"
                      style={editable ? { cursor: 'grab' } : undefined}
                    >
                      <div className="d-flex justify-content-between align-items-start">
                        <span className="small me-2" style={{ wordBreak: 'break-word', minWidth: 0 }}>
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
                            <Button size="sm" variant="outline-danger" onClick={() => { if (!checkCanModifyFiles()) return; setDeleteTarget(f); }}>
                              Delete
                            </Button>
                          )}
                        </div>
                      </div>
                      {(assignmentLabel || assignedInstrumentIds.length > 0) && (
                        <div className="d-flex flex-wrap gap-1 mt-1">
                          {assignmentLabel && (
                            <Badge bg="secondary" style={{ fontSize: '0.7rem' }}>{assignmentLabel}</Badge>
                          )}
                          {assignedInstrumentIds.map((id) => (
                            <Badge key={id} bg="primary" style={{ fontSize: '0.7rem' }}>
                              {instrumentNameMap.get(id) ?? `Instrument ${id}`}
                            </Badge>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                };

                return (
                  <>
                    {unlistedFiles.length > 0 && (
                      <>
                        <div className="small fw-semibold text-muted text-uppercase mb-2" style={{ letterSpacing: '0.05em' }}>
                          Needs Sorting <span className="fw-normal">({unlistedFiles.length})</span>
                        </div>
                        {unlistedFiles.map(renderFileCard)}
                        {assignedFiles.length > 0 && <hr className="my-2" />}
                      </>
                    )}
                    {assignedFiles.length > 0 && (
                      <>
                        <div className="small fw-semibold text-muted text-uppercase mb-2" style={{ letterSpacing: '0.05em' }}>
                          Sorted <span className="fw-normal">({assignedFiles.length})</span>
                        </div>
                        {assignedFiles.map(renderFileCard)}
                      </>
                    )}
                  </>
                );
              })()}

              {editable && (
                <div className="mt-2">
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
            </Col>
          </Row>
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
