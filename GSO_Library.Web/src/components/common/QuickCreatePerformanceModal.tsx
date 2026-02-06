import { useState } from 'react';
import { Alert, Button, Form, Modal, Spinner } from 'react-bootstrap';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { performancesApi } from '../../api/performances';
import { ensemblesApi } from '../../api/ensembles';
import SearchableSelect from './SearchableSelect';
import type { Performance } from '../../types';

interface QuickCreatePerformanceModalProps {
  show: boolean;
  onHide: () => void;
  onCreated: (performance: Performance) => void;
}

export default function QuickCreatePerformanceModal({ show, onHide, onCreated }: QuickCreatePerformanceModalProps) {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [link, setLink] = useState('');
  const [performanceDate, setPerformanceDate] = useState('');
  const [notes, setNotes] = useState('');
  const [ensembleId, setEnsembleId] = useState<number | null>(null);
  const [error, setError] = useState('');

  const allEnsembles = useQuery({ queryKey: ['ensembles-all'], queryFn: () => ensemblesApi.list({ page: 1, pageSize: 100 }), enabled: show });

  const mutation = useMutation({
    mutationFn: () => performancesApi.create({
      name,
      link,
      performanceDate: performanceDate || undefined,
      notes: notes || undefined,
      ensembleId: ensembleId ?? undefined,
    }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['performances-all'] });
      onCreated(created);
      resetAndClose();
    },
    onError: () => setError('Failed to create performance'),
  });

  const resetAndClose = () => {
    setName('');
    setLink('');
    setPerformanceDate('');
    setNotes('');
    setEnsembleId(null);
    setError('');
    onHide();
  };

  return (
    <Modal show={show} onHide={resetAndClose}>
      <Modal.Header closeButton>
        <Modal.Title>Create New Performance</Modal.Title>
      </Modal.Header>
      <Form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
        <Modal.Body>
          {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
          <Form.Group className="mb-3">
            <Form.Label>Name *</Form.Label>
            <Form.Control value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Link *</Form.Label>
            <Form.Control value={link} onChange={(e) => setLink(e.target.value)} required placeholder="https://..." />
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Performance Date</Form.Label>
            <Form.Control type="date" value={performanceDate} onChange={(e) => setPerformanceDate(e.target.value)} />
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Notes</Form.Label>
            <Form.Control as="textarea" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Ensemble</Form.Label>
            <SearchableSelect
              placeholder="Select an ensemble (optional)"
              options={allEnsembles.data?.items.map((e) => ({ value: e.id, label: e.name })) ?? []}
              value={ensembleId}
              onChange={(v) => setEnsembleId(v)}
            />
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
