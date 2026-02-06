import { Card, Col, Row, Spinner } from 'react-bootstrap';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { arrangementsApi } from '../api/arrangements';
import { gamesApi } from '../api/games';
import { seriesApi } from '../api/series';
import { instrumentsApi } from '../api/instruments';
import { performancesApi } from '../api/performances';
import { useAuth } from '../hooks/useAuth';

export default function Dashboard() {
  const { username, canEdit, isAdmin } = useAuth();

  const arrangements = useQuery({ queryKey: ['arrangements', { page: 1, pageSize: 5 }], queryFn: () => arrangementsApi.list({ page: 1, pageSize: 5, sortBy: 'created_at', sortDirection: 'desc' }) });
  const games = useQuery({ queryKey: ['games-count'], queryFn: () => gamesApi.list({ page: 1, pageSize: 1 }) });
  const series = useQuery({ queryKey: ['series-count'], queryFn: () => seriesApi.list({ page: 1, pageSize: 1 }) });
  const instruments = useQuery({ queryKey: ['instruments-count'], queryFn: () => instrumentsApi.list({ page: 1, pageSize: 1 }) });
  const performances = useQuery({ queryKey: ['performances-count'], queryFn: () => performancesApi.list({ page: 1, pageSize: 1 }) });

  const stats = [
    { label: 'Arrangements', count: arrangements.data?.totalCount, link: '/arrangements' },
    { label: 'Games', count: games.data?.totalCount, link: '/games' },
    { label: 'Series', count: series.data?.totalCount, link: '/series' },
    { label: 'Instruments', count: instruments.data?.totalCount, link: '/instruments' },
    { label: 'Performances', count: performances.data?.totalCount, link: '/performances' },
  ];

  return (
    <>
      <h2 className="mb-4">Welcome, {username}</h2>

      <Row className="g-3 mb-4">
        {stats.map((s) => (
          <Col key={s.label} xs={6} md={4} lg>
            <Card as={Link} to={s.link} className="text-decoration-none text-center h-100">
              <Card.Body>
                <Card.Title className="display-6">
                  {s.count !== undefined ? s.count : <Spinner size="sm" animation="border" />}
                </Card.Title>
                <Card.Text className="text-muted">{s.label}</Card.Text>
              </Card.Body>
            </Card>
          </Col>
        ))}
      </Row>

      <h4>Recent Arrangements</h4>
      {arrangements.isLoading ? (
        <Spinner animation="border" />
      ) : arrangements.data?.items.length === 0 ? (
        <p className="text-muted">No arrangements yet.</p>
      ) : (
        <Row className="g-3">
          {arrangements.data?.items.map((a) => (
            <Col key={a.id} md={6} lg={4}>
              <Card>
                <Card.Body>
                  <Card.Title>
                    <Link to={`/arrangements/${a.id}`}>{a.name}</Link>
                  </Card.Title>
                  {a.composer && <Card.Subtitle className="mb-2 text-muted">{a.composer}</Card.Subtitle>}
                  {a.arranger && <Card.Text className="mb-1"><small>Arr. {a.arranger}</small></Card.Text>}
                  {a.games?.length > 0 && (
                    <Card.Text className="mb-0">
                      <small className="text-muted">{a.games.map((g) => g.name).join(', ')}</small>
                    </Card.Text>
                  )}
                </Card.Body>
              </Card>
            </Col>
          ))}
        </Row>
      )}

      {canEdit() && (
        <div className="mt-4">
          <h5>Quick Actions</h5>
          <Link to="/arrangements/new" className="btn btn-primary me-2">New Arrangement</Link>
          <Link to="/games/new" className="btn btn-outline-primary me-2">New Game</Link>
          <Link to="/series/new" className="btn btn-outline-primary me-2">New Series</Link>
          <Link to="/instruments/new" className="btn btn-outline-primary me-2">New Instrument</Link>
          {isAdmin() && <Link to="/admin/users" className="btn btn-outline-secondary">Manage Users</Link>}
        </div>
      )}
    </>
  );
}
