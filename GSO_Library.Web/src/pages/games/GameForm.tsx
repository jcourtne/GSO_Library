import { useEffect, useState } from 'react';
import { Alert, Button, Card, Form, Spinner } from 'react-bootstrap';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { gamesApi } from '../../api/games';
import { seriesApi } from '../../api/series';
import QuickCreateSeriesModal from '../../components/common/QuickCreateSeriesModal';
import SearchableSelect from '../../components/common/SearchableSelect';

export default function GameForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [seriesId, setSeriesId] = useState<number | ''>('');
  const [showCreateSeries, setShowCreateSeries] = useState(false);

  const { data: existing, isLoading } = useQuery({
    queryKey: ['game', id],
    queryFn: () => gamesApi.get(Number(id)),
    enabled: isEdit,
  });

  const allSeries = useQuery({ queryKey: ['series-all'], queryFn: () => seriesApi.list({ page: 1, pageSize: 100 }) });

  useEffect(() => {
    if (existing) {
      setName(existing.name);
      setDescription(existing.description || '');
      setSeriesId(existing.seriesId);
    }
  }, [existing]);

  const mutation = useMutation({
    mutationFn: () => {
      const payload = { name, description: description || undefined, seriesId: Number(seriesId) };
      return isEdit ? gamesApi.update(Number(id), payload) : gamesApi.create(payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['games'] });
      navigate('/games');
    },
    onError: () => setError('Failed to save game'),
  });

  if (isEdit && isLoading) return <Spinner animation="border" />;

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Game</h2>
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
              <Form.Label>Series *</Form.Label>
              <div className="d-flex gap-2">
                <div className="flex-grow-1">
                  <SearchableSelect
                    placeholder="Select a series"
                    options={allSeries.data?.items.map((s) => ({ value: s.id, label: s.name })) ?? []}
                    value={seriesId || null}
                    onChange={(v) => setSeriesId(v ?? '')}
                    required
                  />
                </div>
                <Button variant="outline-secondary" onClick={() => setShowCreateSeries(true)} title="Create New Series">
                  +
                </Button>
              </div>
            </Form.Group>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save' : 'Create')}
            </Button>
            <Button variant="secondary" className="ms-2" onClick={() => navigate('/games')}>Cancel</Button>
          </Form>
        </Card.Body>
      </Card>
      <QuickCreateSeriesModal
        show={showCreateSeries}
        onHide={() => setShowCreateSeries(false)}
        onCreated={(series) => setSeriesId(series.id)}
      />
    </>
  );
}
