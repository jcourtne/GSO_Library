import { useState } from 'react';
import { Alert, Button, Form, Modal, Spinner } from 'react-bootstrap';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { instrumentsApi } from '../../api/instruments';
import type { Instrument } from '../../types';

interface QuickCreateInstrumentModalProps {
  show: boolean;
  onHide: () => void;
  onCreated: (instrument: Instrument) => void;
}

export default function QuickCreateInstrumentModal({ show, onHide, onCreated }: QuickCreateInstrumentModalProps) {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [error, setError] = useState('');

  const mutation = useMutation({
    mutationFn: () => instrumentsApi.create({ name }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['instruments-all'] });
      onCreated(created);
      resetAndClose();
    },
    onError: () => setError('Failed to create instrument'),
  });

  const resetAndClose = () => {
    setName('');
    setError('');
    onHide();
  };

  return (
    <Modal show={show} onHide={resetAndClose}>
      <Modal.Header closeButton>
        <Modal.Title>Create New Instrument</Modal.Title>
      </Modal.Header>
      <Form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
        <Modal.Body>
          {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
          <Form.Group className="mb-3">
            <Form.Label>Name *</Form.Label>
            <Form.Control value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
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
