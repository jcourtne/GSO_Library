import { useEffect, useState } from 'react';
import { Alert, Button, Card, Col, Form, Row, Spinner } from 'react-bootstrap';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { performancesApi } from '../../api/performances';
import { ensemblesApi } from '../../api/ensembles';

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

  const { data: existing, isLoading } = useQuery({
    queryKey: ['performance', id],
    queryFn: () => performancesApi.get(Number(id)),
    enabled: isEdit,
  });

  const { data: ensembles } = useQuery({
    queryKey: ['ensembles-all'],
    queryFn: () => ensemblesApi.getAll(),
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

  const mutation = useMutation({
    mutationFn: () => {
      const payload = {
        name,
        link,
        performanceDate: performanceDate || undefined,
        notes: notes || undefined,
        ensembleId: ensembleId ?? undefined,
      };
      return isEdit ? performancesApi.update(Number(id), payload) : performancesApi.create(payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['performances'] });
      navigate('/performances');
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
              <Form.Label>Link *</Form.Label>
              <Form.Control type="url" value={link} onChange={(e) => setLink(e.target.value)} required placeholder="https://..." />
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
              <Form.Select
                value={ensembleId ?? ''}
                onChange={(e) => setEnsembleId(e.target.value ? Number(e.target.value) : null)}
              >
                <option value="">No Ensemble</option>
                {ensembles?.map((ens) => (
                  <option key={ens.id} value={ens.id}>{ens.name}</option>
                ))}
              </Form.Select>
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Label>Notes</Form.Label>
              <Form.Control as="textarea" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </Form.Group>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save' : 'Create')}
            </Button>
            <Button variant="secondary" className="ms-2" onClick={() => navigate('/performances')}>Cancel</Button>
          </Form>
        </Card.Body>
      </Card>
    </>
  );
}
