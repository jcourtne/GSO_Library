import { useState } from 'react';
import { Alert, Badge, Button, Card, Col, ListGroup, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import { authApi } from '../../api/auth';
import ConfirmModal from '../../components/common/ConfirmModal';
import FileSection from '../../components/arrangements/FileSection';
import RenderedScoreGrid from '../../components/arrangements/RenderedScoreGrid';
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
  const { canEdit, canDownloadAll, isSubmitter, isEnsembleLibrarian, isEnsembleDownloader, username } = useAuth();
  const isEnsembleScoped = isEnsembleLibrarian() || isEnsembleDownloader();
  const queryClient = useQueryClient();
  const [showDelete, setShowDelete] = useState(false);
  const [error, setError] = useState('');
  const [showAllPerformances, setShowAllPerformances] = useState(false);
  const [showAllSeasons, setShowAllSeasons] = useState(false);

  const { data: arrangement, isLoading } = useQuery({
    queryKey: ['arrangement', id],
    queryFn: () => arrangementsApi.get(Number(id)),
    enabled: !!id,
  });

  const { data: myEnsembles = [] } = useQuery({
    queryKey: ['my-ensembles'],
    queryFn: () => authApi.getMyEnsembles(),
    enabled: isEnsembleScoped,
  });

  const arrangementInMyEnsembles = isEnsembleScoped &&
    (arrangement?.ensembles ?? []).some(e => myEnsembles.some(me => me.id === e.id));

  // An arrangement with no ensemble is public — any download/ensemble-download role can get its files.
  const arrangementIsPublic = !!arrangement && (arrangement.ensembles ?? []).length === 0;

  const isOwner = !!username && !!arrangement?.createdBy &&
    arrangement.createdBy.toLowerCase() === username.toLowerCase();

  const canDownloadNonPlayback = canDownloadAll() || arrangementInMyEnsembles ||
    (isEnsembleScoped && arrangementIsPublic) || (isSubmitter() && isOwner);

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
          {arrangement.composers?.length > 0 && <p className="text-muted mb-0">Composed by {arrangement.composers.join(', ')}</p>}
          {arrangement.arrangers?.length > 0 && <p className="text-muted mb-0">Arranged by {arrangement.arrangers.join(', ')}</p>}
        </div>
        {(canEdit() || (isEnsembleLibrarian() && arrangementInMyEnsembles) || (isSubmitter() && arrangement.createdBy === username)) && (
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
                {categorized.notationFiles.length > 0 && (
                  <FileSection title="Notation Files" files={categorized.notationFiles} arrangementId={arrangement.id} editable={false} canDownload={canDownloadNonPlayback} />
                )}
                <RenderedScoreGrid arrangement={arrangement} files={arrangement.files} editable={false} canDownload={canDownloadNonPlayback} />
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
              {arrangement.instruments?.length > 0 ? (() => {
                const seen = new Map<string, typeof arrangement.instruments>();
                for (const i of arrangement.instruments) {
                  const key = i.familyName ?? '';
                  if (!seen.has(key)) seen.set(key, []);
                  seen.get(key)!.push(i);
                }
                const groups: { label: string; items: typeof arrangement.instruments }[] = [];
                seen.forEach((items, key) => {
                  if (key !== '') groups.push({ label: key, items });
                });
                const unassigned = seen.get('');
                if (unassigned?.length) groups.push({ label: 'Other', items: unassigned });
                return groups.map(({ label, items }) => (
                  <div key={label} className="mb-2">
                    <div className="text-muted small fw-semibold mb-1">{label}</div>
                    <div className="d-flex flex-wrap gap-1">
                      {items.map((i) => <Badge key={i.id} bg="success">{i.name}</Badge>)}
                    </div>
                  </div>
                ));
              })() : (
                <p className="text-muted mb-0">None</p>
              )}
            </Card.Body>
          </Card>

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Ensembles</Card.Title>
              {arrangement.ensembles && arrangement.ensembles.length > 0 ? (
                <div className="d-flex flex-wrap gap-1">
                  {arrangement.ensembles.map((e) => (
                    <Badge key={e.id} bg="warning" text="dark">{e.name}</Badge>
                  ))}
                </div>
              ) : (
                <>
                  <Badge bg="success">Public</Badge>
                  <p className="text-muted mb-0 mt-2 small">
                    This arrangement isn't restricted to an ensemble. Any user with download
                    permission can access its files, and any ensemble can add it to a season.
                  </p>
                </>
              )}
            </Card.Body>
          </Card>

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Recent Performances</Card.Title>
              {arrangement.performances?.length > 0 ? (() => {
                const sorted = [...arrangement.performances].sort((a, b) => {
                  if (!a.performanceDate && !b.performanceDate) return 0;
                  if (!a.performanceDate) return 1;
                  if (!b.performanceDate) return -1;
                  return b.performanceDate.localeCompare(a.performanceDate);
                });
                const visible = showAllPerformances ? sorted : sorted.slice(0, 3);
                return (
                  <>
                    <ListGroup variant="flush">
                      {visible.map((p) => (
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
                    {sorted.length > 3 && (
                      <Button variant="link" size="sm" className="p-0 mt-2" onClick={() => setShowAllPerformances(!showAllPerformances)}>
                        {showAllPerformances ? 'Show less' : `Show all ${sorted.length}`}
                      </Button>
                    )}
                  </>
                );
              })() : (
                <p className="text-muted mb-0">None</p>
              )}
            </Card.Body>
          </Card>

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Recent Seasons</Card.Title>
              {arrangement.seasons && arrangement.seasons.length > 0 ? (() => {
                const sorted = [...arrangement.seasons].sort((a, b) => {
                  if (!a.startDate && !b.startDate) return 0;
                  if (!a.startDate) return 1;
                  if (!b.startDate) return -1;
                  return b.startDate.localeCompare(a.startDate);
                });
                const visible = showAllSeasons ? sorted : sorted.slice(0, 3);
                return (
                  <>
                    <ListGroup variant="flush">
                      {visible.map((s) => (
                        <ListGroup.Item key={s.id} className="px-0">
                          <Link to={`/seasons/${s.id}`} className="text-decoration-none">
                            {s.name}
                          </Link>
                          {s.startDate && (
                            <small className="text-muted d-block">
                              {new Date(s.startDate).toLocaleDateString()}
                            </small>
                          )}
                        </ListGroup.Item>
                      ))}
                    </ListGroup>
                    {sorted.length > 3 && (
                      <Button variant="link" size="sm" className="p-0 mt-2" onClick={() => setShowAllSeasons(!showAllSeasons)}>
                        {showAllSeasons ? 'Show less' : `Show all ${sorted.length}`}
                      </Button>
                    )}
                  </>
                );
              })() : (
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
