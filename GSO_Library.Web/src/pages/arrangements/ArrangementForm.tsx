import { useEffect, useState } from 'react';
import { Alert, Button, Card, Col, Form, ListGroup, Modal, Row, Spinner } from 'react-bootstrap';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import { gamesApi } from '../../api/games';
import { instrumentsApi } from '../../api/instruments';
import { performancesApi } from '../../api/performances';
import type { ArrangementRequest } from '../../types';

export default function ArrangementForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');

  const [form, setForm] = useState<ArrangementRequest>({
    name: '',
    description: '',
    arranger: '',
    composer: '',
    key: '',
    durationSeconds: undefined,
    year: undefined,
  });

  // Load existing arrangement for edit mode
  const { data: existing, isLoading: loadingExisting } = useQuery({
    queryKey: ['arrangement', id],
    queryFn: () => arrangementsApi.get(Number(id)),
    enabled: isEdit,
  });

  // Load reference data for linking
  const allGames = useQuery({ queryKey: ['games-all'], queryFn: () => gamesApi.list({ page: 1, pageSize: 100 }) });
  const allInstruments = useQuery({ queryKey: ['instruments-all'], queryFn: () => instrumentsApi.list({ page: 1, pageSize: 100 }) });
  const allPerformances = useQuery({ queryKey: ['performances-all'], queryFn: () => performancesApi.list({ page: 1, pageSize: 100 }) });

  // Track linked entity IDs
  const [linkedGameIds, setLinkedGameIds] = useState<Set<number>>(new Set());
  const [linkedInstrumentIds, setLinkedInstrumentIds] = useState<Set<number>>(new Set());
  const [linkedPerformanceIds, setLinkedPerformanceIds] = useState<Set<number>>(new Set());

  // Picker modal state
  const [showGamePicker, setShowGamePicker] = useState(false);
  const [gameSearch, setGameSearch] = useState('');
  const [showInstrumentPicker, setShowInstrumentPicker] = useState(false);
  const [instrumentSearch, setInstrumentSearch] = useState('');
  const [showPerformancePicker, setShowPerformancePicker] = useState(false);
  const [performanceSearch, setPerformanceSearch] = useState('');

  useEffect(() => {
    if (existing) {
      setForm({
        name: existing.name,
        description: existing.description || '',
        arranger: existing.arranger || '',
        composer: existing.composer || '',
        key: existing.key || '',
        durationSeconds: existing.durationSeconds,
        year: existing.year,
      });
      setLinkedGameIds(new Set(existing.games?.map((g) => g.id) || []));
      setLinkedInstrumentIds(new Set(existing.instruments?.map((i) => i.id) || []));
      setLinkedPerformanceIds(new Set(existing.performances?.map((p) => p.id) || []));
    }
  }, [existing]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      let arrangementId: number;
      if (isEdit) {
        await arrangementsApi.update(Number(id), form);
        arrangementId = Number(id);
      } else {
        const created = await arrangementsApi.create(form);
        arrangementId = created.id;
      }

      // Sync relationships in edit mode
      if (isEdit && existing) {
        const oldGameIds = new Set(existing.games?.map((g) => g.id) || []);
        const oldInstrumentIds = new Set(existing.instruments?.map((i) => i.id) || []);
        const oldPerformanceIds = new Set(existing.performances?.map((p) => p.id) || []);

        // Games
        for (const gid of linkedGameIds) {
          if (!oldGameIds.has(gid)) await arrangementsApi.addGame(arrangementId, gid);
        }
        for (const gid of oldGameIds) {
          if (!linkedGameIds.has(gid)) await arrangementsApi.removeGame(arrangementId, gid);
        }
        // Instruments
        for (const iid of linkedInstrumentIds) {
          if (!oldInstrumentIds.has(iid)) await arrangementsApi.addInstrument(arrangementId, iid);
        }
        for (const iid of oldInstrumentIds) {
          if (!linkedInstrumentIds.has(iid)) await arrangementsApi.removeInstrument(arrangementId, iid);
        }
        // Performances
        for (const pid of linkedPerformanceIds) {
          if (!oldPerformanceIds.has(pid)) await arrangementsApi.addPerformance(arrangementId, pid);
        }
        for (const pid of oldPerformanceIds) {
          if (!linkedPerformanceIds.has(pid)) await arrangementsApi.removePerformance(arrangementId, pid);
        }
      } else if (!isEdit) {
        // New arrangement - add all relationships
        for (const gid of linkedGameIds) await arrangementsApi.addGame(arrangementId, gid);
        for (const iid of linkedInstrumentIds) await arrangementsApi.addInstrument(arrangementId, iid);
        for (const pid of linkedPerformanceIds) await arrangementsApi.addPerformance(arrangementId, pid);
      }

      return arrangementId;
    },
    onSuccess: (arrangementId) => {
      queryClient.invalidateQueries({ queryKey: ['arrangements'] });
      queryClient.invalidateQueries({ queryKey: ['arrangement', id] });
      navigate(`/arrangements/${arrangementId}`);
    },
    onError: () => setError('Failed to save arrangement'),
  });

  if (isEdit && loadingExisting) return <Spinner animation="border" />;

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Arrangement</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }}>
        <Row className="g-4">
          <Col md={8}>
            <Card className="mb-3">
              <Card.Body>
                <Form.Group className="mb-3">
                  <Form.Label>Name *</Form.Label>
                  <Form.Control
                    value={form.name}
                    onChange={(e) => setForm({ ...form, name: e.target.value })}
                    required
                  />
                </Form.Group>
                <Form.Group className="mb-3">
                  <Form.Label>Description</Form.Label>
                  <Form.Control
                    as="textarea"
                    rows={3}
                    value={form.description || ''}
                    onChange={(e) => setForm({ ...form, description: e.target.value || undefined })}
                  />
                </Form.Group>
                <Row>
                  <Col md={6}>
                    <Form.Group className="mb-3">
                      <Form.Label>Composer</Form.Label>
                      <Form.Control
                        value={form.composer || ''}
                        onChange={(e) => setForm({ ...form, composer: e.target.value || undefined })}
                      />
                    </Form.Group>
                  </Col>
                  <Col md={6}>
                    <Form.Group className="mb-3">
                      <Form.Label>Arranger</Form.Label>
                      <Form.Control
                        value={form.arranger || ''}
                        onChange={(e) => setForm({ ...form, arranger: e.target.value || undefined })}
                      />
                    </Form.Group>
                  </Col>
                </Row>
                <Row>
                  <Col md={4}>
                    <Form.Group className="mb-3">
                      <Form.Label>Key</Form.Label>
                      <Form.Control
                        value={form.key || ''}
                        onChange={(e) => setForm({ ...form, key: e.target.value || undefined })}
                        placeholder="e.g. C Major"
                      />
                    </Form.Group>
                  </Col>
                  <Col md={4}>
                    <Form.Group className="mb-3">
                      <Form.Label>Duration (seconds)</Form.Label>
                      <Form.Control
                        type="number"
                        value={form.durationSeconds ?? ''}
                        onChange={(e) => setForm({ ...form, durationSeconds: e.target.value ? Number(e.target.value) : undefined })}
                      />
                    </Form.Group>
                  </Col>
                  <Col md={4}>
                    <Form.Group className="mb-3">
                      <Form.Label>Year</Form.Label>
                      <Form.Control
                        type="number"
                        value={form.year ?? ''}
                        onChange={(e) => setForm({ ...form, year: e.target.value ? Number(e.target.value) : undefined })}
                      />
                    </Form.Group>
                  </Col>
                </Row>
              </Card.Body>
            </Card>
          </Col>

          <Col md={4}>
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Games</Card.Title>
                {linkedGameIds.size > 0 ? (
                  <ListGroup variant="flush" className="mb-2">
                    {allGames.data?.items
                      .filter((g) => linkedGameIds.has(g.id))
                      .map((g) => (
                        <ListGroup.Item key={g.id} className="d-flex justify-content-between align-items-center px-0">
                          {g.name}
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => {
                              const next = new Set(linkedGameIds);
                              next.delete(g.id);
                              setLinkedGameIds(next);
                            }}
                          >
                            Remove
                          </Button>
                        </ListGroup.Item>
                      ))}
                  </ListGroup>
                ) : (
                  <p className="text-muted mb-2">No games selected</p>
                )}
                <Button variant="outline-primary" size="sm" onClick={() => { setGameSearch(''); setShowGamePicker(true); }}>
                  Add
                </Button>
              </Card.Body>
            </Card>

            <Modal show={showGamePicker} onHide={() => setShowGamePicker(false)}>
              <Modal.Header closeButton>
                <Modal.Title>Add Games</Modal.Title>
              </Modal.Header>
              <Modal.Body>
                <Form.Control
                  placeholder="Search games..."
                  value={gameSearch}
                  onChange={(e) => setGameSearch(e.target.value)}
                  className="mb-3"
                  autoFocus
                />
                <ListGroup style={{ maxHeight: '300px', overflowY: 'auto' }}>
                  {allGames.data?.items
                    .filter((g) => !linkedGameIds.has(g.id) && g.name.toLowerCase().includes(gameSearch.toLowerCase()))
                    .map((g) => (
                      <ListGroup.Item
                        key={g.id}
                        action
                        onClick={() => {
                          const next = new Set(linkedGameIds);
                          next.add(g.id);
                          setLinkedGameIds(next);
                        }}
                      >
                        {g.name}
                      </ListGroup.Item>
                    ))}
                </ListGroup>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onClick={() => setShowGamePicker(false)}>
                  Done
                </Button>
              </Modal.Footer>
            </Modal>

            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Instruments</Card.Title>
                {linkedInstrumentIds.size > 0 ? (
                  <ListGroup variant="flush" className="mb-2">
                    {allInstruments.data?.items
                      .filter((i) => linkedInstrumentIds.has(i.id))
                      .map((i) => (
                        <ListGroup.Item key={i.id} className="d-flex justify-content-between align-items-center px-0">
                          {i.name}
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => {
                              const next = new Set(linkedInstrumentIds);
                              next.delete(i.id);
                              setLinkedInstrumentIds(next);
                            }}
                          >
                            Remove
                          </Button>
                        </ListGroup.Item>
                      ))}
                  </ListGroup>
                ) : (
                  <p className="text-muted mb-2">No instruments selected</p>
                )}
                <Button variant="outline-primary" size="sm" onClick={() => { setInstrumentSearch(''); setShowInstrumentPicker(true); }}>
                  Add
                </Button>
              </Card.Body>
            </Card>

            <Modal show={showInstrumentPicker} onHide={() => setShowInstrumentPicker(false)}>
              <Modal.Header closeButton>
                <Modal.Title>Add Instruments</Modal.Title>
              </Modal.Header>
              <Modal.Body>
                <Form.Control
                  placeholder="Search instruments..."
                  value={instrumentSearch}
                  onChange={(e) => setInstrumentSearch(e.target.value)}
                  className="mb-3"
                  autoFocus
                />
                <ListGroup style={{ maxHeight: '300px', overflowY: 'auto' }}>
                  {allInstruments.data?.items
                    .filter((i) => !linkedInstrumentIds.has(i.id) && i.name.toLowerCase().includes(instrumentSearch.toLowerCase()))
                    .map((i) => (
                      <ListGroup.Item
                        key={i.id}
                        action
                        onClick={() => {
                          const next = new Set(linkedInstrumentIds);
                          next.add(i.id);
                          setLinkedInstrumentIds(next);
                        }}
                      >
                        {i.name}
                      </ListGroup.Item>
                    ))}
                </ListGroup>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onClick={() => setShowInstrumentPicker(false)}>
                  Done
                </Button>
              </Modal.Footer>
            </Modal>

            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Performances</Card.Title>
                {linkedPerformanceIds.size > 0 ? (
                  <ListGroup variant="flush" className="mb-2">
                    {allPerformances.data?.items
                      .filter((p) => linkedPerformanceIds.has(p.id))
                      .map((p) => (
                        <ListGroup.Item key={p.id} className="d-flex justify-content-between align-items-center px-0">
                          {p.name}
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => {
                              const next = new Set(linkedPerformanceIds);
                              next.delete(p.id);
                              setLinkedPerformanceIds(next);
                            }}
                          >
                            Remove
                          </Button>
                        </ListGroup.Item>
                      ))}
                  </ListGroup>
                ) : (
                  <p className="text-muted mb-2">No performances selected</p>
                )}
                <Button variant="outline-primary" size="sm" onClick={() => { setPerformanceSearch(''); setShowPerformancePicker(true); }}>
                  Add
                </Button>
              </Card.Body>
            </Card>

            <Modal show={showPerformancePicker} onHide={() => setShowPerformancePicker(false)}>
              <Modal.Header closeButton>
                <Modal.Title>Add Performances</Modal.Title>
              </Modal.Header>
              <Modal.Body>
                <Form.Control
                  placeholder="Search performances..."
                  value={performanceSearch}
                  onChange={(e) => setPerformanceSearch(e.target.value)}
                  className="mb-3"
                  autoFocus
                />
                <ListGroup style={{ maxHeight: '300px', overflowY: 'auto' }}>
                  {allPerformances.data?.items
                    .filter((p) => !linkedPerformanceIds.has(p.id) && p.name.toLowerCase().includes(performanceSearch.toLowerCase()))
                    .map((p) => (
                      <ListGroup.Item
                        key={p.id}
                        action
                        onClick={() => {
                          const next = new Set(linkedPerformanceIds);
                          next.add(p.id);
                          setLinkedPerformanceIds(next);
                        }}
                      >
                        {p.name}
                      </ListGroup.Item>
                    ))}
                </ListGroup>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onClick={() => setShowPerformancePicker(false)}>
                  Done
                </Button>
              </Modal.Footer>
            </Modal>
          </Col>
        </Row>

        <div className="mt-3">
          <Button type="submit" disabled={saveMutation.isPending}>
            {saveMutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save Changes' : 'Create Arrangement')}
          </Button>
          <Button variant="secondary" className="ms-2" onClick={() => navigate(-1)}>Cancel</Button>
        </div>
      </Form>
    </>
  );
}
