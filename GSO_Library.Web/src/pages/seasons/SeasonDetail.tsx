import { useEffect, useMemo, useState } from 'react';
import { Alert, Badge, Button, Card, Col, Collapse, Form, InputGroup, ListGroup, Row, Spinner, Table } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { seasonsApi } from '../../api/seasons';
import type { ShareConfig } from '../../api/seasons';
import { instrumentSortOrdersApi } from '../../api/instrumentSortOrders';
import ConfirmModal from '../../components/common/ConfirmModal';
import { useAuth } from '../../hooks/useAuth';
import { categorizeFiles, groupScoreFiles } from '../../utils/fileCategories';
import type { Arrangement, Instrument, Performance } from '../../types';

function formatDuration(seconds?: number) {
  if (!seconds) return '-';
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function maxUploadedAt(files: { uploadedAt: string }[]): string | null {
  if (files.length === 0) return null;
  return files.reduce((max, f) => f.uploadedAt > max ? f.uploadedAt : max, files[0].uploadedAt);
}

function formatDateRange(startDate?: string, endDate?: string) {
  if (!startDate && !endDate) return null;
  const fmt = (d: string) => new Date(d).toLocaleDateString();
  if (startDate && endDate) return `${fmt(startDate)} – ${fmt(endDate)}`;
  if (startDate) return `From ${fmt(startDate)}`;
  return `Until ${fmt(endDate!)}`;
}

export default function SeasonDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [showDelete, setShowDelete] = useState(false);
  const [error, setError] = useState('');
  // undefined = not yet overridden; null = alphabetical; number = specific sort order
  const [selectedSortOrderId, setSelectedSortOrderId] = useState<number | null | undefined>(undefined);
  const [showShareConfig, setShowShareConfig] = useState(false);
  const [shareIncludePdf, setShareIncludePdf] = useState(true);
  const [shareIncludeNotation, setShareIncludeNotation] = useState(false);
  const [shareIncludePlayback, setShareIncludePlayback] = useState(false);
  const [sharePassword, setSharePassword] = useState('');
  const [clearPassword, setClearPassword] = useState(false);
  const [showRevokeConfirm, setShowRevokeConfirm] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    setSelectedSortOrderId(undefined);
  }, [id]);

  const { data: season, isLoading } = useQuery({
    queryKey: ['season', id],
    queryFn: () => seasonsApi.get(Number(id)),
    enabled: !!id,
  });

  const { data: sortOrders } = useQuery({
    queryKey: ['instrument-sort-orders'],
    queryFn: instrumentSortOrdersApi.list,
  });

  const effectiveSortOrderId = selectedSortOrderId !== undefined
    ? selectedSortOrderId
    : (sortOrders?.find((so) => so.isDefault)?.id ?? null);

  const grouped = useMemo(() => {
    const allFiles = (season?.arrangements ?? []).flatMap((a) => a.files ?? []);
    const { renderedScoreFiles } = categorizeFiles(allFiles);
    const allInstrumentIds = new Set(
      (season?.arrangements ?? []).flatMap((a) => a.instruments ?? []).map((i) => i.id),
    );
    return groupScoreFiles(renderedScoreFiles, allInstrumentIds);
  }, [season]);

  const sortedInstruments = useMemo(() => {
    const instrumentMap = new Map<number, Instrument>();
    for (const arr of season?.arrangements ?? []) {
      for (const inst of arr.instruments ?? []) {
        if (!instrumentMap.has(inst.id)) instrumentMap.set(inst.id, inst);
      }
    }
    const allInstruments = [...instrumentMap.values()];
    const selectedOrder = sortOrders?.find((so) => so.id === effectiveSortOrderId);
    if (!selectedOrder) {
      return [...allInstruments].sort((a, b) => a.name.localeCompare(b.name));
    }
    const positionMap = new Map(selectedOrder.instruments.map((inst, i) => [inst.id, i]));
    return [...allInstruments].sort((a, b) => {
      const posA = positionMap.get(a.id) ?? Infinity;
      const posB = positionMap.get(b.id) ?? Infinity;
      if (posA !== posB) return posA - posB;
      return a.name.localeCompare(b.name);
    });
  }, [season, sortOrders, effectiveSortOrderId]);

  const hasInstrumentContent = sortedInstruments.length > 0 ||
    grouped.conductorScore.length > 0 ||
    grouped.percussion.length > 0 ||
    grouped.unlisted.length > 0;

  const deleteMutation = useMutation({
    mutationFn: () => seasonsApi.delete(Number(id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['seasons'] });
      navigate('/seasons');
    },
    onError: () => setError('Failed to delete season'),
  });

  const shareMutation = useMutation({
    mutationFn: (config: ShareConfig) => seasonsApi.configureShare(Number(id), config),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['season', id] });
      setSharePassword('');
      setClearPassword(false);
      setShowShareConfig(false);
    },
    onError: () => setError('Failed to configure share link'),
  });

  const revokeMutation = useMutation({
    mutationFn: () => seasonsApi.revokeShare(Number(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['season', id] }),
    onError: () => setError('Failed to revoke share link'),
  });

  if (isLoading) return <Spinner animation="border" />;
  if (!season) return <Alert variant="danger">Season not found</Alert>;

  const dateRange = formatDateRange(season.startDate, season.endDate);

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <div className="d-flex justify-content-between align-items-start mb-3">
        <div>
          <h2>{season.name}</h2>
          {dateRange && <p className="text-muted mb-0">{dateRange}</p>}
        </div>
        <div>
          {canEdit() && (
            <>
              <Link to={`/seasons/${id}/edit`} className="btn btn-outline-primary me-2">Edit</Link>
              <Button variant="outline-danger" onClick={() => setShowDelete(true)}>Delete</Button>
            </>
          )}
        </div>
      </div>

      <Row className="g-4">
        <Col md={8}>
          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Arrangements</Card.Title>
              {season.arrangements && season.arrangements.length > 0 ? (
                <ListGroup variant="flush">
                  {season.arrangements.map((a: Arrangement) => (
                    <ListGroup.Item key={a.id} className="d-flex justify-content-between align-items-start px-0">
                      <div>
                        <Link to={`/arrangements/${a.id}`} className="fw-semibold text-decoration-none">
                          {a.name}
                        </Link>
                        <div className="text-muted small">
                          {[
                            a.composers?.length > 0 && `Composed by ${a.composers.join(', ')}`,
                            a.arrangers?.length > 0 && `Arranged by ${a.arrangers.join(', ')}`,
                          ].filter(Boolean).join(' · ')}
                        </div>
                        {a.games && a.games.length > 0 && (
                          <div className="mt-1 d-flex flex-wrap gap-1">
                            {a.games.map((g) => (
                              <Badge key={g.id} bg="info" className="fw-normal">{g.name}</Badge>
                            ))}
                          </div>
                        )}
                      </div>
                      <span className="text-muted small text-nowrap ms-3">{formatDuration(a.durationSeconds)}</span>
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No arrangements linked to this season.</p>
              )}
            </Card.Body>
          </Card>

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Performances</Card.Title>
              {season.performances && season.performances.length > 0 ? (
                <ListGroup variant="flush">
                  {season.performances.map((p: Performance) => (
                    <ListGroup.Item key={p.id} className="d-flex justify-content-between align-items-center px-0">
                      <div>
                        <Link to={`/performances/${p.id}`} className="fw-semibold text-decoration-none">
                          {p.name}
                        </Link>
                        {p.performanceDate && (
                          <div className="text-muted small">
                            {new Date(p.performanceDate).toLocaleDateString()}
                          </div>
                        )}
                      </div>
                      {p.link && (
                        <a href={p.link} target="_blank" rel="noopener noreferrer" className="btn btn-sm btn-outline-secondary ms-3">
                          Link
                        </a>
                      )}
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No performances linked to this season.</p>
              )}
            </Card.Body>
          </Card>

          {hasInstrumentContent && (
            <Card className="mb-3">
              <Card.Body>
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <Card.Title className="mb-0">Instruments</Card.Title>
                  {sortOrders && sortOrders.length > 0 && (
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
                </div>
                <Table size="sm" bordered className="mb-0">
                  <thead>
                    <tr>
                      <th>Instrument</th>
                      <th>Last Part Uploaded</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[
                      { key: 'conductor', label: "Conductor's Score", files: grouped.conductorScore, italic: true },
                      ...sortedInstruments.map((inst) => ({
                        key: String(inst.id),
                        label: inst.name,
                        files: grouped.byInstrument.get(inst.id) ?? [],
                        italic: false,
                      })),
                      { key: 'percussion', label: 'Percussion (Generic)', files: grouped.percussion, italic: true },
                      { key: 'unlisted', label: 'Unlisted Parts', files: grouped.unlisted, italic: true },
                    ].map(({ key, label, files, italic }) => {
                      const date = maxUploadedAt(files);
                      return (
                        <tr key={key}>
                          <td>{italic ? <em>{label}</em> : label}</td>
                          <td>
                            {date
                              ? new Date(date).toLocaleDateString()
                              : <span className="text-muted">—</span>}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </Table>
              </Card.Body>
            </Card>
          )}
        </Col>

        <Col md={4}>
          {season.notes && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Notes</Card.Title>
                <Card.Text>{season.notes}</Card.Text>
              </Card.Body>
            </Card>
          )}

          {season.ensemble && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Ensemble</Card.Title>
                <p>
                  <Link to={`/ensembles/${season.ensemble.id}`} className="fw-semibold text-decoration-none">
                    {season.ensemble.name}
                  </Link>
                </p>
                {season.ensemble.website && (
                  <p>
                    <a href={season.ensemble.website} target="_blank" rel="noopener noreferrer">
                      Website
                    </a>
                  </p>
                )}
              </Card.Body>
            </Card>
          )}

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Details</Card.Title>
              <p><strong>Created by:</strong> {season.createdBy || '-'}</p>
            </Card.Body>
          </Card>

          {canEdit() && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Public Share Link</Card.Title>

                {!season.shareToken ? (
                  <>
                    <p className="text-muted small mb-3">
                      Generate a public link so musicians can download parts without an account.
                    </p>
                    <Form.Check type="checkbox" label="Include PDFs" checked={shareIncludePdf} onChange={(e) => setShareIncludePdf(e.target.checked)} className="mb-2" />
                    <Form.Check type="checkbox" label="Include notation files" checked={shareIncludeNotation} onChange={(e) => setShareIncludeNotation(e.target.checked)} className="mb-2" />
                    <Form.Check type="checkbox" label="Include playback files" checked={shareIncludePlayback} onChange={(e) => setShareIncludePlayback(e.target.checked)} className="mb-3" />
                    <Form.Group className="mb-3">
                      <Form.Label className="small">Password (optional)</Form.Label>
                      <Form.Control
                        type="password"
                        size="sm"
                        placeholder="Leave blank for no password"
                        value={sharePassword}
                        onChange={(e) => setSharePassword(e.target.value)}
                      />
                    </Form.Group>
                    <Button
                      variant="primary"
                      size="sm"
                      disabled={shareMutation.isPending}
                      onClick={() => shareMutation.mutate({
                        includePdf: shareIncludePdf,
                        includeNotation: shareIncludeNotation,
                        includePlayback: shareIncludePlayback,
                        password: sharePassword || null,
                      })}
                    >
                      Generate Link
                    </Button>
                  </>
                ) : (
                  <>
                    <Form.Group className="mb-2">
                      <InputGroup size="sm">
                        <Form.Control
                          readOnly
                          value={`${window.location.origin}/share/seasons/${season.shareToken}`}
                        />
                        <Button
                          variant="outline-secondary"
                          onClick={() => {
                            navigator.clipboard.writeText(`${window.location.origin}/share/seasons/${season.shareToken}`);
                            setCopied(true);
                            setTimeout(() => setCopied(false), 2000);
                          }}
                        >
                          {copied ? 'Copied!' : 'Copy'}
                        </Button>
                      </InputGroup>
                    </Form.Group>

                    <div className="mb-3 small text-muted">
                      {season.hasSharePassword
                        ? <Badge bg="secondary">Password protected</Badge>
                        : 'No password'}
                    </div>

                    <Button
                      variant="link"
                      size="sm"
                      className="p-0 mb-2 d-block"
                      onClick={() => {
                        setShareIncludePdf(season.shareIncludePdf ?? true);
                        setShareIncludeNotation(season.shareIncludeNotation ?? false);
                        setShareIncludePlayback(season.shareIncludePlayback ?? false);
                        setSharePassword('');
                        setClearPassword(false);
                        setShowShareConfig(!showShareConfig);
                      }}
                    >
                      {showShareConfig ? 'Hide config' : 'Edit config'}
                    </Button>

                    <Collapse in={showShareConfig}>
                      <div>
                        <Form.Check type="checkbox" label="Include PDFs" checked={shareIncludePdf} onChange={(e) => setShareIncludePdf(e.target.checked)} className="mb-2" />
                        <Form.Check type="checkbox" label="Include notation files" checked={shareIncludeNotation} onChange={(e) => setShareIncludeNotation(e.target.checked)} className="mb-2" />
                        <Form.Check type="checkbox" label="Include playback files" checked={shareIncludePlayback} onChange={(e) => setShareIncludePlayback(e.target.checked)} className="mb-3" />
                        <Form.Group className="mb-2">
                          <Form.Label className="small">
                            Password {season.hasSharePassword ? '(leave blank to keep existing)' : '(optional)'}
                          </Form.Label>
                          <Form.Control
                            type="password"
                            size="sm"
                            placeholder={season.hasSharePassword ? 'Leave blank to keep existing' : 'Leave blank for no password'}
                            value={sharePassword}
                            onChange={(e) => setSharePassword(e.target.value)}
                            disabled={clearPassword}
                          />
                        </Form.Group>
                        {season.hasSharePassword && (
                          <Form.Check
                            type="checkbox"
                            label="Remove password"
                            checked={clearPassword}
                            onChange={(e) => setClearPassword(e.target.checked)}
                            className="mb-3"
                          />
                        )}
                        <Button
                          variant="primary"
                          size="sm"
                          disabled={shareMutation.isPending}
                          onClick={() => shareMutation.mutate({
                            includePdf: shareIncludePdf,
                            includeNotation: shareIncludeNotation,
                            includePlayback: shareIncludePlayback,
                            password: sharePassword || null,
                            clearPassword,
                          })}
                        >
                          Save
                        </Button>
                      </div>
                    </Collapse>

                    <div className="mt-3">
                      <Button
                        variant="outline-danger"
                        size="sm"
                        onClick={() => setShowRevokeConfirm(true)}
                      >
                        Revoke Link
                      </Button>
                    </div>
                  </>
                )}
              </Card.Body>
            </Card>
          )}
        </Col>
      </Row>

      <ConfirmModal
        show={showDelete}
        title="Delete Season"
        message={`Are you sure you want to delete "${season.name}"?`}
        onConfirm={() => deleteMutation.mutate()}
        onCancel={() => setShowDelete(false)}
      />

      <ConfirmModal
        show={showRevokeConfirm}
        title="Revoke Share Link"
        message="Are you sure you want to revoke this share link? Anyone with the link will no longer be able to access this page."
        onConfirm={() => { revokeMutation.mutate(); setShowRevokeConfirm(false); }}
        onCancel={() => setShowRevokeConfirm(false)}
      />
    </>
  );
}
