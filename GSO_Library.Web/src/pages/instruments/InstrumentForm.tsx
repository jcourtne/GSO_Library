import { useEffect, useState } from 'react';
import { Alert, Button, Card, Form, Spinner } from 'react-bootstrap';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { instrumentsApi } from '../../api/instruments';
import { instrumentFamiliesApi } from '../../api/instrumentFamilies';
import { getApiErrorMessage } from '../../api/errors';

export default function InstrumentForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [name, setName] = useState('');
  const [familyId, setFamilyId] = useState<number | null>(null);

  const { data: existing, isLoading } = useQuery({
    queryKey: ['instrument', id],
    queryFn: () => instrumentsApi.get(Number(id)),
    enabled: isEdit,
  });

  const { data: families } = useQuery({
    queryKey: ['instrument-families'],
    queryFn: instrumentFamiliesApi.getAll,
  });

  useEffect(() => {
    if (existing) {
      setName(existing.name);
      setFamilyId(existing.familyId ?? null);
    }
  }, [existing]);

  const mutation = useMutation({
    mutationFn: () =>
      isEdit
        ? instrumentsApi.update(Number(id), { name, familyId })
        : instrumentsApi.create({ name, familyId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instruments'] });
      navigate('/instruments');
    },
    onError: (err) => setError(getApiErrorMessage(err, 'Failed to save instrument')),
  });

  if (isEdit && isLoading) return <Spinner animation="border" />;

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Instrument</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
      <Card>
        <Card.Body>
          <Form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
            <Form.Group className="mb-3">
              <Form.Label>Name *</Form.Label>
              <Form.Control value={name} onChange={(e) => setName(e.target.value)} required />
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Label>Family</Form.Label>
              <Form.Select
                value={familyId ?? ''}
                onChange={(e) => setFamilyId(e.target.value ? Number(e.target.value) : null)}
              >
                <option value="">— No Family —</option>
                {families?.map((f) => (
                  <option key={f.id} value={f.id}>{f.name}</option>
                ))}
              </Form.Select>
            </Form.Group>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save' : 'Create')}
            </Button>
            <Button variant="secondary" className="ms-2" onClick={() => navigate('/instruments')}>Cancel</Button>
          </Form>
        </Card.Body>
      </Card>
    </>
  );
}
