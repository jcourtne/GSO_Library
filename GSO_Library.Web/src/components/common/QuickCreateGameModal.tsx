import { useState } from 'react';
import { Alert, Button, Form, Modal, Spinner } from 'react-bootstrap';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { gamesApi } from '../../api/games';
import { seriesApi } from '../../api/series';
import SearchableSelect from './SearchableSelect';
import QuickCreateSeriesModal from './QuickCreateSeriesModal';
import type { Game } from '../../types';

interface QuickCreateGameModalProps {
  show: boolean;
  onHide: () => void;
  onCreated: (game: Game) => void;
}

export default function QuickCreateGameModal({ show, onHide, onCreated }: QuickCreateGameModalProps) {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [seriesId, setSeriesId] = useState<number | ''>('');
  const [error, setError] = useState('');
  const [showCreateSeries, setShowCreateSeries] = useState(false);

  const allSeries = useQuery({ queryKey: ['series-all'], queryFn: () => seriesApi.list({ page: 1, pageSize: 100 }), enabled: show });

  const mutation = useMutation({
    mutationFn: () => gamesApi.create({ name, description: description || undefined, seriesId: Number(seriesId) }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['games-all'] });
      onCreated(created);
      resetAndClose();
    },
    onError: () => setError('Failed to create game'),
  });

  const resetAndClose = () => {
    setName('');
    setDescription('');
    setSeriesId('');
    setError('');
    onHide();
  };

  return (
    <>
      <Modal show={show} onHide={resetAndClose}>
        <Modal.Header closeButton>
          <Modal.Title>Create New Game</Modal.Title>
        </Modal.Header>
        <Form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
          <Modal.Body>
            {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
            <Form.Group className="mb-3">
              <Form.Label>Name *</Form.Label>
              <Form.Control value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Label>Description</Form.Label>
              <Form.Control as="textarea" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
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
                <Button variant="outline-secondary" size="sm" onClick={() => setShowCreateSeries(true)} title="Create New Series">
                  +
                </Button>
              </div>
            </Form.Group>
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={resetAndClose}>Cancel</Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? <Spinner size="sm" animation="border" /> : 'Create'}
            </Button>
          </Modal.Footer>
        </Form>
      </Modal>

      <QuickCreateSeriesModal
        show={showCreateSeries}
        onHide={() => setShowCreateSeries(false)}
        onCreated={(series) => setSeriesId(series.id)}
      />
    </>
  );
}
