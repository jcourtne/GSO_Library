import { useState } from 'react';
import { Alert, Badge, Button, Card, Col, ListGroup, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import ConfirmModal from '../../components/common/ConfirmModal';
import FileSection from '../../components/arrangements/FileSection';
import { categorizeFiles } from '../../utils/fileCategories';
import { useAuth } from '../../hooks/useAuth';

function formatDuration(seconds?: number) {
  if (!seconds) return '-';
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

export default function ArrangementDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [showDelete, setShowDelete] = useState(false);
  const [error, setError] = useState('');

  const { data: arrangement, isLoading } = useQuery({
    queryKey: ['arrangement', id],
    queryFn: () => arrangementsApi.get(Number(id)),
    enabled: !!id,
  });

  const deleteMutation = useMutation({
    mutationFn: () => arrangementsApi.delete(Number(id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['arrangements'] });
      navigate('/arrangements');
    },
    onError: () => setError('Failed to delete arrangement'),
  });

  if (isLoading) return <Spinner animation="border" />;
  if (!arrangement) return <Alert variant="danger">Arrangement not found</Alert>;

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <div className="d-flex justify-content-between align-items-start mb-3">
        <div>
          <h2>{arrangement.name}</h2>
          {arrangement.composer && <p className="text-muted mb-0">Composed by {arrangement.composer}</p>}
          {arrangement.arranger && <p className="text-muted mb-0">Arranged by {arrangement.arranger}</p>}
        </div>
        {canEdit() && (
          <div>
            <Link to={`/arrangements/${id}/edit`} className="btn btn-outline-primary me-2">
              Edit
            </Link>
            <Button variant="outline-danger" onClick={() => setShowDelete(true)}>Delete</Button>
          </div>
        )}
      </div>

      <Row className="g-4">
        <Col md={8}>
          {arrangement.description && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Description</Card.Title>
                <Card.Text>{arrangement.description}</Card.Text>
              </Card.Body>
            </Card>
          )}

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Details</Card.Title>
              <Row>
                <Col sm={6}>
                  <p><strong>Key:</strong> {arrangement.key || '-'}</p>
                  <p><strong>Duration:</strong> {formatDuration(arrangement.durationSeconds)}</p>
                </Col>
                <Col sm={6}>
                  <p><strong>Year:</strong> {arrangement.year || '-'}</p>
                  <p><strong>Created by:</strong> {arrangement.createdBy || '-'}</p>
                </Col>
              </Row>
            </Card.Body>
          </Card>

          {arrangement.files?.length > 0 && (() => {
            const categorized = categorizeFiles(arrangement.files);
            return (
              <>
                {categorized.arrangementFiles.length > 0 && (
                  <FileSection title="Arrangement Files" files={categorized.arrangementFiles} arrangementId={arrangement.id} editable={false} canDownload={canEdit()} />
                )}
                {categorized.pdfFiles.length > 0 && (
                  <FileSection title="PDF Files" files={categorized.pdfFiles} arrangementId={arrangement.id} editable={false} canDownload={canEdit()} />
                )}
                {categorized.playbackFiles.length > 0 && (
                  <FileSection title="Playback Files" files={categorized.playbackFiles} arrangementId={arrangement.id} editable={false} />
                )}
              </>
            );
          })()}
        </Col>

        <Col md={4}>
          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Games</Card.Title>
              {arrangement.games?.length > 0 ? (
                <div className="d-flex flex-wrap gap-1">
                  {arrangement.games.map((g) => (
                    <Link key={g.id} to={`/games/${g.id}/edit`} className="text-decoration-none">
                      <Badge bg="info">{g.name}</Badge>
                    </Link>
                  ))}
                </div>
              ) : (
                <p className="text-muted mb-0">None</p>
              )}
            </Card.Body>
          </Card>

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Instruments</Card.Title>
              {arrangement.instruments?.length > 0 ? (
                <div className="d-flex flex-wrap gap-1">
                  {arrangement.instruments.map((i) => (
                    <Badge key={i.id} bg="success">{i.name}</Badge>
                  ))}
                </div>
              ) : (
                <p className="text-muted mb-0">None</p>
              )}
            </Card.Body>
          </Card>

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Performances</Card.Title>
              {arrangement.performances?.length > 0 ? (
                <ListGroup variant="flush">
                  {arrangement.performances.map((p) => (
                    <ListGroup.Item key={p.id}>
                      <Link to={`/performances/${p.id}`}>{p.name}</Link>
                      {p.performanceDate && (
                        <small className="text-muted d-block">
                          {new Date(p.performanceDate).toLocaleDateString()}
                        </small>
                      )}
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">None</p>
              )}
            </Card.Body>
          </Card>
        </Col>
      </Row>

      <ConfirmModal
        show={showDelete}
        title="Delete Arrangement"
        message={`Are you sure you want to delete "${arrangement.name}"? This will also delete all associated files.`}
        onConfirm={() => deleteMutation.mutate()}
        onCancel={() => setShowDelete(false)}
      />
    </>
  );
}
