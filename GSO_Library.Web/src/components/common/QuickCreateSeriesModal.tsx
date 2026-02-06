import { useState } from 'react';
import { Alert, Button, Form, Modal, Spinner } from 'react-bootstrap';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { seriesApi } from '../../api/series';
import type { Series } from '../../types';

interface QuickCreateSeriesModalProps {
  show: boolean;
  onHide: () => void;
  onCreated: (series: Series) => void;
}

export default function QuickCreateSeriesModal({ show, onHide, onCreated }: QuickCreateSeriesModalProps) {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');

  const mutation = useMutation({
    mutationFn: () => seriesApi.create({ name, description: description || undefined }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['series-all'] });
      onCreated(created);
      resetAndClose();
    },
    onError: () => setError('Failed to create series'),
  });

  const resetAndClose = () => {
    setName('');
    setDescription('');
    setError('');
    onHide();
  };

  return (
    <Modal show={show} onHide={resetAndClose}>
      <Modal.Header closeButton>
        <Modal.Title>Create New Series</Modal.Title>
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
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={resetAndClose}>Cancel</Button>
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner size="sm" animation="border" /> : 'Create'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}
