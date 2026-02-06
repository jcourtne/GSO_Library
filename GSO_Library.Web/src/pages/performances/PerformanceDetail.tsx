import { useState } from 'react';
import { Alert, Badge, Button, Card, Col, ListGroup, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { performancesApi } from '../../api/performances';
import { arrangementsApi } from '../../api/arrangements';
import ConfirmModal from '../../components/common/ConfirmModal';
import { useAuth } from '../../hooks/useAuth';

function formatDuration(seconds?: number) {
  if (!seconds) return '-';
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

export default function PerformanceDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [showDelete, setShowDelete] = useState(false);
  const [error, setError] = useState('');

  const { data: performance, isLoading } = useQuery({
    queryKey: ['performance', id],
    queryFn: () => performancesApi.get(Number(id)),
    enabled: !!id,
  });

  const { data: arrangements, isLoading: arrangementsLoading } = useQuery({
    queryKey: ['arrangements', { performanceId: Number(id) }],
    queryFn: () => arrangementsApi.list({ performanceId: Number(id), pageSize: 100 }),
    enabled: !!id,
  });

  const deleteMutation = useMutation({
    mutationFn: () => performancesApi.delete(Number(id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['performances'] });
      navigate('/performances');
    },
    onError: () => setError('Failed to delete performance'),
  });

  if (isLoading) return <Spinner animation="border" />;
  if (!performance) return <Alert variant="danger">Performance not found</Alert>;

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <div className="d-flex justify-content-between align-items-start mb-3">
        <div>
          <h2>{performance.name}</h2>
          {performance.performanceDate && (
            <p className="text-muted mb-0">
              {new Date(performance.performanceDate).toLocaleDateString()}
            </p>
          )}
        </div>
        <div>
          {performance.link && (
            <a href={performance.link} target="_blank" rel="noopener noreferrer" className="btn btn-outline-secondary me-2">
              External Link
            </a>
          )}
          {canEdit() && (
            <>
              <Link to={`/performances/${id}/edit`} className="btn btn-outline-primary me-2">
                Edit
              </Link>
              <Button variant="outline-danger" onClick={() => setShowDelete(true)}>Delete</Button>
            </>
          )}
        </div>
      </div>

      <Row className="g-4">
        <Col md={8}>
          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Arrangements</Card.Title>
              {arrangementsLoading ? (
                <Spinner animation="border" size="sm" />
              ) : arrangements?.items && arrangements.items.length > 0 ? (
                <ListGroup variant="flush">
                  {arrangements.items.map((a) => (
                    <ListGroup.Item key={a.id} className="d-flex justify-content-between align-items-start">
                      <div>
                        <Link to={`/arrangements/${a.id}`} className="fw-semibold text-decoration-none">
                          {a.name}
                        </Link>
                        <div className="text-muted small">
                          {[
                            a.composer && `Composed by ${a.composer}`,
                            a.arranger && `Arranged by ${a.arranger}`,
                          ].filter(Boolean).join(' · ')}
                        </div>
                        {a.games?.length > 0 && (
                          <div className="mt-1 d-flex flex-wrap gap-1">
                            {a.games.map((g) => (
                              <Badge key={g.id} bg="info" className="fw-normal">{g.name}</Badge>
                            ))}
                          </div>
                        )}
                      </div>
                      <div className="text-end text-muted small text-nowrap ms-3">
                        {a.key && <div>{a.key}</div>}
                        <div>{formatDuration(a.durationSeconds)}</div>
                      </div>
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No arrangements linked to this performance.</p>
              )}
            </Card.Body>
          </Card>
        </Col>

        <Col md={4}>
          {performance.notes && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Notes</Card.Title>
                <Card.Text>{performance.notes}</Card.Text>
              </Card.Body>
            </Card>
          )}

          {performance.ensemble && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Ensemble</Card.Title>
                <p>
                  <Link to={`/ensembles/${performance.ensemble.id}`} className="fw-semibold text-decoration-none">
                    {performance.ensemble.name}
                  </Link>
                </p>
                {performance.ensemble.website && (
                  <p>
                    <a href={performance.ensemble.website} target="_blank" rel="noopener noreferrer">
                      Website
                    </a>
                  </p>
                )}
              </Card.Body>
            </Card>
          )}

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Details</Card.Title>
              <p><strong>Created by:</strong> {performance.createdBy || '-'}</p>
            </Card.Body>
          </Card>
        </Col>
      </Row>

      <ConfirmModal
        show={showDelete}
        title="Delete Performance"
        message={`Are you sure you want to delete "${performance.name}"?`}
        onConfirm={() => deleteMutation.mutate()}
        onCancel={() => setShowDelete(false)}
      />
    </>
  );
}
