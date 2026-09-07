import { useEffect, useState } from 'react';
import { Alert, Button, Card, Col, Form, ListGroup, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { performancesApi } from '../../api/performances';
import { ensemblesApi } from '../../api/ensembles';
import { arrangementsApi } from '../../api/arrangements';
import { seasonsApi } from '../../api/seasons';
import SearchableSelect from '../../components/common/SearchableSelect';
import ProgramSection from '../../components/performances/ProgramSection';
import ArrangementPickerModal from '../../components/arrangements/ArrangementPickerModal';
import { useMyEnsembles } from '../../hooks/useMyEnsembles';
import type { Arrangement } from '../../types';

export default function PerformanceForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [name, setName] = useState('');
  const [link, setLink] = useState('');
  const [performanceDate, setPerformanceDate] = useState('');
  const [notes, setNotes] = useState('');
  const [ensembleId, setEnsembleId] = useState<number | null>(null);
  const [showAddArrangement, setShowAddArrangement] = useState(false);
  const [seasonId, setSeasonId] = useState<number | null>(null);

  const { data: existing, isLoading } = useQuery({
    queryKey: ['performance', id],
    queryFn: () => performancesApi.get(Number(id)),
    enabled: isEdit,
  });

  const { data: programFiles = [] } = useQuery({
    queryKey: ['performance-files', Number(id)],
    queryFn: () => performancesApi.listFiles(Number(id)),
    enabled: isEdit,
  });

  const { myEnsembles, canEditAllEnsembles } = useMyEnsembles();

  const { data: ensembles } = useQuery({
    queryKey: ['ensembles-all'],
    queryFn: () => ensemblesApi.getAll(),
    enabled: canEditAllEnsembles,
  });

  // Ensemble Librarians may only tie a performance to one of their own ensembles.
  const ensembleOptions = (canEditAllEnsembles ? ensembles : myEnsembles) ?? [];

  const { data: seasonsAll } = useQuery({
    queryKey: ['seasons-all', ensembleId],
    queryFn: () => seasonsApi.list({ pageSize: 100, ensembleIds: [ensembleId!] }),
    enabled: !isEdit && ensembleId !== null,
  });

  const { data: selectedSeason, isLoading: seasonLoading } = useQuery({
    queryKey: ['season-for-performance', seasonId],
    queryFn: () => seasonsApi.get(seasonId!),
    enabled: !isEdit && seasonId !== null,
  });

  useEffect(() => {
    if (existing) {
      setName(existing.name);
      setLink(existing.link);
      setPerformanceDate(existing.performanceDate ? existing.performanceDate.split('T')[0] : '');
      setNotes(existing.notes || '');
      setEnsembleId(existing.ensembleId ?? null);
    }
  }, [existing]);

  const addArrangementMutation = useMutation({
    mutationFn: (arrangementId: number) => arrangementsApi.addPerformance(arrangementId, Number(id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['performance', id] });
      setShowAddArrangement(false);
    },
    onError: () => setError('Failed to add arrangement'),
  });

  const removeArrangementMutation = useMutation({
    mutationFn: (arrangementId: number) => arrangementsApi.removePerformance(arrangementId, Number(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['performance', id] }),
    onError: () => setError('Failed to remove arrangement'),
  });

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name,
        link,
        performanceDate: performanceDate || undefined,
        notes: notes || undefined,
        ensembleId: ensembleId ?? undefined,
      };
      if (isEdit) {
        await performancesApi.update(Number(id), payload);
        return Number(id);
      } else {
        const created = await performancesApi.create(payload);
        if (seasonId) {
          await seasonsApi.addPerformance(seasonId, created.id);
          for (const arr of selectedSeason?.arrangements ?? []) {
            await arrangementsApi.addPerformance(arr.id, created.id);
          }
        }
        return created.id;
      }
    },
    onSuccess: (newId) => {
      queryClient.invalidateQueries({ queryKey: ['performances'] });
      queryClient.invalidateQueries({ queryKey: ['performance', id] });
      navigate(`/performances/${newId}`);
    },
    onError: () => setError('Failed to save performance'),
  });

  if (isEdit && isLoading) return <Spinner animation="border" />;

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Performance</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
      <Card>
        <Card.Body>
          <Form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
            <Form.Group className="mb-3">
              <Form.Label>Name *</Form.Label>
              <Form.Control value={name} onChange={(e) => setName(e.target.value)} required />
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Label>Link</Form.Label>
              <Form.Control type="url" value={link} onChange={(e) => setLink(e.target.value)} placeholder="https://..." />
            </Form.Group>
            <Row>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Performance Date</Form.Label>
                  <Form.Control type="date" value={performanceDate} onChange={(e) => setPerformanceDate(e.target.value)} />
                </Form.Group>
              </Col>
            </Row>
            <Form.Group className="mb-3">
              <Form.Label>Ensemble</Form.Label>
              <SearchableSelect
                placeholder="No Ensemble"
                options={ensembleOptions.map((ens) => ({ value: ens.id, label: ens.name }))}
                value={ensembleId}
                onChange={(v) => { setEnsembleId(v); setSeasonId(null); }}
              />
            </Form.Group>
            {!isEdit && (
              <Form.Group className="mb-3">
                <Form.Label>Season</Form.Label>
                <SearchableSelect
                  placeholder={ensembleId === null ? 'Select an ensemble first' : 'No Season'}
                  disabled={ensembleId === null}
                  options={seasonsAll?.items.map((s) => ({ value: s.id, label: s.name })) ?? []}
                  value={seasonId}
                  onChange={(v) => setSeasonId(v)}
                />
              </Form.Group>
            )}
            <Form.Group className="mb-3">
              <Form.Label>Notes</Form.Label>
              <Form.Control as="textarea" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </Form.Group>
            <Button type="submit" disabled={mutation.isPending || (!isEdit && seasonId !== null && seasonLoading)}>
              {mutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save' : 'Create')}
            </Button>
            <Button variant="secondary" className="ms-2" onClick={() => navigate(isEdit ? `/performances/${id}` : '/performances')}>Cancel</Button>
          </Form>
        </Card.Body>
      </Card>

      {!isEdit && seasonId !== null && selectedSeason && (
        <Card className="mt-4">
          <Card.Body>
            <Card.Title>Arrangements from {selectedSeason.name}</Card.Title>
            {selectedSeason.arrangements && selectedSeason.arrangements.length > 0 ? (
              <ListGroup variant="flush">
                {selectedSeason.arrangements.map((a) => (
                  <ListGroup.Item key={a.id} className="px-0">
                    <div className="fw-semibold">{a.name}</div>
                    <div className="text-muted small">
                      {[
                        a.composers?.length > 0 && `Composed by ${a.composers.join(', ')}`,
                        a.arrangers?.length > 0 && `Arranged by ${a.arrangers.join(', ')}`,
                      ].filter(Boolean).join(' · ')}
                    </div>
                  </ListGroup.Item>
                ))}
              </ListGroup>
            ) : (
              <p className="text-muted mb-0">No arrangements in this season.</p>
            )}
          </Card.Body>
        </Card>
      )}

      {isEdit && (() => {
        const linkedArrangementIds = new Set(existing?.arrangements?.map((a: Arrangement) => a.id) ?? []);
        return (
          <>
            <Card className="mb-4 mt-4">
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
                        </div>
                        <Button
                          size="sm"
                          variant="outline-danger"
                          onClick={() => removeArrangementMutation.mutate(a.id)}
                          disabled={removeArrangementMutation.isPending}
                        >
                          Remove
                        </Button>
                      </ListGroup.Item>
                    ))}
                  </ListGroup>
                ) : (
                  <p className="text-muted mb-0">No arrangements linked to this performance.</p>
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

            <div className="mt-4">
              <ProgramSection
                files={programFiles}
                performanceId={Number(id)}
                editable={true}
              />
            </div>
          </>
        );
      })()}
    </>
  );
}
