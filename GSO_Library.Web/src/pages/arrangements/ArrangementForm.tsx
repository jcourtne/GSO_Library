import { useEffect, useState } from 'react';
import { Alert, Badge, Button, Card, Col, Form, Row, Spinner } from 'react-bootstrap';
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

  const toggleLinked = (set: Set<number>, setFn: React.Dispatch<React.SetStateAction<Set<number>>>, itemId: number) => {
    const next = new Set(set);
    if (next.has(itemId)) next.delete(itemId);
    else next.add(itemId);
    setFn(next);
  };

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
                <div className="d-flex flex-wrap gap-1">
                  {allGames.data?.items.map((g) => (
                    <Badge
                      key={g.id}
                      bg={linkedGameIds.has(g.id) ? 'info' : 'secondary'}
                      style={{ cursor: 'pointer' }}
                      onClick={() => toggleLinked(linkedGameIds, setLinkedGameIds, g.id)}
                    >
                      {g.name}
                    </Badge>
                  ))}
                </div>
              </Card.Body>
            </Card>

            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Instruments</Card.Title>
                <div className="d-flex flex-wrap gap-1">
                  {allInstruments.data?.items.map((i) => (
                    <Badge
                      key={i.id}
                      bg={linkedInstrumentIds.has(i.id) ? 'success' : 'secondary'}
                      style={{ cursor: 'pointer' }}
                      onClick={() => toggleLinked(linkedInstrumentIds, setLinkedInstrumentIds, i.id)}
                    >
                      {i.name}
                    </Badge>
                  ))}
                </div>
              </Card.Body>
            </Card>

            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Performances</Card.Title>
                <div className="d-flex flex-wrap gap-1">
                  {allPerformances.data?.items.map((p) => (
                    <Badge
                      key={p.id}
                      bg={linkedPerformanceIds.has(p.id) ? 'warning' : 'secondary'}
                      style={{ cursor: 'pointer' }}
                      onClick={() => toggleLinked(linkedPerformanceIds, setLinkedPerformanceIds, p.id)}
                    >
                      {p.name}
                    </Badge>
                  ))}
                </div>
              </Card.Body>
            </Card>
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
