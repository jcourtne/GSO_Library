import { useState } from 'react';
import { Alert, Button, Card, Col, ListGroup, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ensemblesApi } from '../../api/ensembles';
import ConfirmModal from '../../components/common/ConfirmModal';
import { useAuth } from '../../hooks/useAuth';

export default function EnsembleDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [showDelete, setShowDelete] = useState(false);
  const [error, setError] = useState('');

  const { data: ensemble, isLoading } = useQuery({
    queryKey: ['ensemble', id],
    queryFn: () => ensemblesApi.get(Number(id)),
    enabled: !!id,
  });

  const deleteMutation = useMutation({
    mutationFn: () => ensemblesApi.delete(Number(id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ensembles'] });
      navigate('/ensembles');
    },
    onError: () => setError('Failed to delete ensemble'),
  });

  if (isLoading) return <Spinner animation="border" />;
  if (!ensemble) return <Alert variant="danger">Ensemble not found</Alert>;

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <div className="d-flex justify-content-between align-items-start mb-3">
        <h2>{ensemble.name}</h2>
        <div>
          {canEdit() && (
            <>
              <Link to={`/ensembles/${id}/edit`} className="btn btn-outline-primary me-2">Edit</Link>
              <Button variant="outline-danger" onClick={() => setShowDelete(true)}>Delete</Button>
            </>
          )}
        </div>
      </div>

      <Row className="g-4">
        <Col md={8}>
          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Performances</Card.Title>
              {ensemble.performances && ensemble.performances.length > 0 ? (
                <ListGroup variant="flush">
                  {ensemble.performances.map((p: { id: number; name: string; performanceDate?: string; link: string }) => (
                    <ListGroup.Item key={p.id} className="d-flex justify-content-between align-items-center">
                      <div>
                        <Link to={`/performances/${p.id}`} className="fw-semibold text-decoration-none">
                          {p.name}
                        </Link>
                        {p.performanceDate && (
                          <div className="text-muted small">
                            {new Date(p.performanceDate).toLocaleDateString()}
                          </div>
                        )}
                      </div>
                      <a href={p.link} target="_blank" rel="noopener noreferrer" className="btn btn-sm btn-outline-secondary">
                        Link
                      </a>
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No performances linked to this ensemble.</p>
              )}
            </Card.Body>
          </Card>
        </Col>

        <Col md={4}>
          {ensemble.description && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Description</Card.Title>
                <Card.Text>{ensemble.description}</Card.Text>
              </Card.Body>
            </Card>
          )}

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Details</Card.Title>
              {ensemble.website && (
                <p>
                  <strong>Website:</strong>{' '}
                  <a href={ensemble.website} target="_blank" rel="noopener noreferrer">{ensemble.website}</a>
                </p>
              )}
              {ensemble.contactInfo && (
                <p><strong>Contact:</strong> {ensemble.contactInfo}</p>
              )}
              <p><strong>Created by:</strong> {ensemble.createdBy || '-'}</p>
            </Card.Body>
          </Card>
        </Col>
      </Row>

      <ConfirmModal
        show={showDelete}
        title="Delete Ensemble"
        message={`Are you sure you want to delete "${ensemble.name}"?`}
        onConfirm={() => deleteMutation.mutate()}
        onCancel={() => setShowDelete(false)}
      />
    </>
  );
}
