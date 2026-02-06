import { useEffect, useState } from 'react';
import { Alert, Button, Card, Form, Spinner } from 'react-bootstrap';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ensemblesApi } from '../../api/ensembles';

export default function EnsembleForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [website, setWebsite] = useState('');
  const [contactInfo, setContactInfo] = useState('');

  const { data: existing, isLoading } = useQuery({
    queryKey: ['ensemble', id],
    queryFn: () => ensemblesApi.get(Number(id)),
    enabled: isEdit,
  });

  useEffect(() => {
    if (existing) {
      setName(existing.name);
      setDescription(existing.description || '');
      setWebsite(existing.website || '');
      setContactInfo(existing.contactInfo || '');
    }
  }, [existing]);

  const mutation = useMutation({
    mutationFn: () => {
      const payload = {
        name,
        description: description || undefined,
        website: website || undefined,
        contactInfo: contactInfo || undefined,
      };
      return isEdit ? ensemblesApi.update(Number(id), payload) : ensemblesApi.create(payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ensembles'] });
      navigate('/ensembles');
    },
    onError: () => setError('Failed to save ensemble'),
  });

  if (isEdit && isLoading) return <Spinner animation="border" />;

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Ensemble</h2>
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
            <Form.Group className="mb-3">
              <Form.Label>Website</Form.Label>
              <Form.Control type="url" value={website} onChange={(e) => setWebsite(e.target.value)} placeholder="https://..." />
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Label>Contact Info</Form.Label>
              <Form.Control as="textarea" rows={2} value={contactInfo} onChange={(e) => setContactInfo(e.target.value)} />
            </Form.Group>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save' : 'Create')}
            </Button>
            <Button variant="secondary" className="ms-2" onClick={() => navigate('/ensembles')}>Cancel</Button>
          </Form>
        </Card.Body>
      </Card>
    </>
  );
}
