import { useEffect, useState } from 'react';
import { Alert, Badge, Button, Card, Col, Form, ListGroup, Modal, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { seasonsApi } from '../../api/seasons';
import { ensemblesApi } from '../../api/ensembles';
import { performancesApi } from '../../api/performances';
import SearchableSelect from '../../components/common/SearchableSelect';
import ArrangementPickerModal from '../../components/arrangements/ArrangementPickerModal';
import type { Arrangement, Performance } from '../../types';

function formatDuration(seconds?: number) {
  if (!seconds) return '-';
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

export default function SeasonForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [name, setName] = useState('');
  const [ensembleId, setEnsembleId] = useState<number | null>(null);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [notes, setNotes] = useState('');
  const [showAddArrangement, setShowAddArrangement] = useState(false);
  const [showAddPerformance, setShowAddPerformance] = useState(false);
  const [performanceSearch, setPerformanceSearch] = useState('');

  const { data: existing, isLoading } = useQuery({
    queryKey: ['season', id],
    queryFn: () => seasonsApi.get(Number(id)),
    enabled: isEdit,
  });

  const { data: ensembles } = useQuery({
    queryKey: ['ensembles-all'],
    queryFn: () => ensemblesApi.getAll(),
  });

  const { data: allPerformances } = useQuery({
    queryKey: ['performances', { search: performanceSearch, pageSize: 20 }],
    queryFn: () => performancesApi.list({ search: performanceSearch || undefined, pageSize: 20 }),
    enabled: showAddPerformance,
  });

  useEffect(() => {
    if (existing) {
      setName(existing.name);
      setEnsembleId(existing.ensembleId);
      setStartDate(existing.startDate ? existing.startDate.split('T')[0] : '');
      setEndDate(existing.endDate ? existing.endDate.split('T')[0] : '');
      setNotes(existing.notes || '');
    }
  }, [existing]);

  const saveMutation = useMutation({
    mutationFn: () => {
      if (!ensembleId) throw new Error('Ensemble is required');
      const payload = {
        name,
        ensembleId,
        startDate: startDate || undefined,
        endDate: endDate || undefined,
        notes: notes || undefined,
      };
      return isEdit ? seasonsApi.update(Number(id), payload) : seasonsApi.create(payload);
    },
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['seasons'] });
      queryClient.invalidateQueries({ queryKey: ['season', id] });
      navigate(isEdit ? `/seasons/${id}` : `/seasons/${created.id}`);
    },
    onError: () => setError('Failed to save season'),
  });

  const addArrangementMutation = useMutation({
    mutationFn: (arrangementId: number) => seasonsApi.addArrangement(Number(id), arrangementId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['season', id] });
      setShowAddArrangement(false);
    },
    onError: () => setError('Failed to add arrangement'),
  });

  const removeArrangementMutation = useMutation({
    mutationFn: (arrangementId: number) => seasonsApi.removeArrangement(Number(id), arrangementId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['season', id] }),
    onError: () => setError('Failed to remove arrangement'),
  });

  const addPerformanceMutation = useMutation({
    mutationFn: (performanceId: number) => seasonsApi.addPerformance(Number(id), performanceId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['season', id] });
      setShowAddPerformance(false);
      setPerformanceSearch('');
    },
    onError: () => setError('Failed to add performance'),
  });

  const removePerformanceMutation = useMutation({
    mutationFn: (performanceId: number) => seasonsApi.removePerformance(Number(id), performanceId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['season', id] }),
    onError: () => setError('Failed to remove performance'),
  });

  if (isEdit && isLoading) return <Spinner animation="border" />;

  const linkedArrangementIds = new Set(existing?.arrangements?.map((a) => a.id) ?? []);
  const linkedPerformanceIds = new Set(existing?.performances?.map((p) => p.id) ?? []);

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Season</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Card className="mb-4">
        <Card.Body>
          <Form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }}>
            <Form.Group className="mb-3">
              <Form.Label>Name *</Form.Label>
              <Form.Control value={name} onChange={(e) => setName(e.target.value)} required />
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Label>Ensemble *</Form.Label>
              <SearchableSelect
                placeholder="Select an ensemble..."
                options={ensembles?.map((ens) => ({ value: ens.id, label: ens.name })) ?? []}
                value={ensembleId}
                onChange={(v) => setEnsembleId(v)}
              />
              {!ensembleId && <Form.Text className="text-muted">An ensemble is required.</Form.Text>}
            </Form.Group>
            <Row>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Start Date</Form.Label>
                  <Form.Control type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
                </Form.Group>
              </Col>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>End Date</Form.Label>
                  <Form.Control type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
                </Form.Group>
              </Col>
            </Row>
            <Form.Group className="mb-3">
              <Form.Label>Notes</Form.Label>
              <Form.Control as="textarea" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </Form.Group>
            <Button type="submit" disabled={saveMutation.isPending || !ensembleId}>
              {saveMutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save' : 'Create')}
            </Button>
            <Button variant="secondary" className="ms-2" onClick={() => navigate(isEdit ? `/seasons/${id}` : '/seasons')}>Cancel</Button>
          </Form>
        </Card.Body>
      </Card>

      {isEdit && (
        <>
          {/* Arrangements */}
          <Card className="mb-4">
            <Card.Body>
              <div className="d-flex justify-content-between align-items-center mb-2">
                <Card.Title className="mb-0">Arrangements</Card.Title>
                <Button size="sm" variant="outline-primary" onClick={() => setShowAddArrangement(true)}>
                  + Add
                </Button>
              </div>
              {existing?.arrangements && existing.arrangements.length > 0 ? (
                <ListGroup variant="flush">
                  {existing.arrangements.map((a: Arrangement) => (
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
                      <div className="d-flex align-items-center gap-2 ms-3">
                        <span className="text-muted small text-nowrap">{formatDuration(a.durationSeconds)}</span>
                        <Button
                          size="sm"
                          variant="outline-danger"
                          onClick={() => removeArrangementMutation.mutate(a.id)}
                          disabled={removeArrangementMutation.isPending}
                        >
                          Remove
                        </Button>
                      </div>
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No arrangements linked to this season.</p>
              )}
            </Card.Body>
          </Card>

          {/* Performances */}
          <Card className="mb-4">
            <Card.Body>
              <div className="d-flex justify-content-between align-items-center mb-2">
                <Card.Title className="mb-0">Performances</Card.Title>
                <Button size="sm" variant="outline-primary" onClick={() => setShowAddPerformance(true)}>
                  + Add
                </Button>
              </div>
              {existing?.performances && existing.performances.length > 0 ? (
                <ListGroup variant="flush">
                  {existing.performances.map((p: Performance) => (
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
                      <div className="d-flex align-items-center gap-2 ms-3">
                        {p.link && (
                          <a href={p.link} target="_blank" rel="noopener noreferrer" className="btn btn-sm btn-outline-secondary">
                            Link
                          </a>
                        )}
                        <Button
                          size="sm"
                          variant="outline-danger"
                          onClick={() => removePerformanceMutation.mutate(p.id)}
                          disabled={removePerformanceMutation.isPending}
                        >
                          Remove
                        </Button>
                      </div>
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No performances linked to this season.</p>
              )}
            </Card.Body>
          </Card>

          <ArrangementPickerModal
            show={showAddArrangement}
            onHide={() => setShowAddArrangement(false)}
            excludeIds={linkedArrangementIds}
            onSelect={(arrangementId) => addArrangementMutation.mutate(arrangementId)}
            isPending={addArrangementMutation.isPending}
          />

          {/* Add Performance Modal */}
          <Modal show={showAddPerformance} onHide={() => { setShowAddPerformance(false); setPerformanceSearch(''); }} size="lg">
            <Modal.Header closeButton>
              <Modal.Title>Add Performance</Modal.Title>
            </Modal.Header>
            <Modal.Body>
              <Form.Control
                size="sm"
                placeholder="Search performances..."
                value={performanceSearch}
                onChange={(e) => setPerformanceSearch(e.target.value)}
                className="mb-3"
              />
              <ListGroup>
                {allPerformances?.items.filter((p) => !linkedPerformanceIds.has(p.id)).map((p: Performance) => (
                  <ListGroup.Item key={p.id} className="d-flex justify-content-between align-items-center">
                    <div>
                      <div className="fw-semibold">{p.name}</div>
                      {p.performanceDate && (
                        <div className="text-muted small">{new Date(p.performanceDate).toLocaleDateString()}</div>
                      )}
                    </div>
                    <Button
                      size="sm"
                      variant="outline-primary"
                      onClick={() => addPerformanceMutation.mutate(p.id)}
                      disabled={addPerformanceMutation.isPending}
                    >
                      Add
                    </Button>
                  </ListGroup.Item>
                ))}
                {allPerformances?.items.filter((p) => !linkedPerformanceIds.has(p.id)).length === 0 && (
                  <ListGroup.Item className="text-muted">No performances found.</ListGroup.Item>
                )}
              </ListGroup>
            </Modal.Body>
          </Modal>
        </>
      )}
    </>
  );
}
