import { useEffect, useState } from 'react';
import { Alert, Button, Card, Form, Spinner } from 'react-bootstrap';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { groupInstrumentationsApi } from '../../api/groupInstrumentations';
import { getApiErrorMessage } from '../../api/errors';

export default function GroupInstrumentationForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const { data: existing, isLoading } = useQuery({
    queryKey: ['group-instrumentation', id],
    queryFn: () => groupInstrumentationsApi.get(Number(id)),
    enabled: isEdit,
  });

  useEffect(() => {
    if (existing) {
      setName(existing.name);
      setDescription(existing.description || '');
    }
  }, [existing]);

  const mutation = useMutation({
    mutationFn: () => {
      const payload = { name, description: description || undefined };
      return isEdit ? groupInstrumentationsApi.update(Number(id), payload) : groupInstrumentationsApi.create(payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['group-instrumentations'] });
      queryClient.invalidateQueries({ queryKey: ['group-instrumentations-all'] });
      navigate('/group-instrumentations');
    },
    onError: (err) => setError(getApiErrorMessage(err, 'Failed to save group instrumentation')),
  });

  if (isEdit && isLoading) return <Spinner animation="border" />;

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Group Instrumentation</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
      <Card>
        <Card.Body>
          <Form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
            <Form.Group className="mb-3">
              <Form.Label>Name *</Form.Label>
              <Form.Control value={name} onChange={(e) => setName(e.target.value)} required />
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Label>Description</Form.Label>
              <Form.Control as="textarea" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
            </Form.Group>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save' : 'Create')}
            </Button>
            <Button variant="secondary" className="ms-2" onClick={() => navigate('/group-instrumentations')}>Cancel</Button>
          </Form>
        </Card.Body>
      </Card>
    </>
  );
}
