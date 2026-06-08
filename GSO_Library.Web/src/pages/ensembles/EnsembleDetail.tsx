import { useState } from 'react';
import { Alert, Badge, Button, Card, Col, Form, ListGroup, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ensemblesApi } from '../../api/ensembles';
import { authApi } from '../../api/auth';
import { seasonsApi } from '../../api/seasons';
import ConfirmModal from '../../components/common/ConfirmModal';
import DataTable from '../../components/common/DataTable';
import { useAuth } from '../../hooks/useAuth';

export default function EnsembleDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [showDelete, setShowDelete] = useState(false);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState('name');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');
  const [selectedUserId, setSelectedUserId] = useState('');

  const { data: ensemble, isLoading } = useQuery({
    queryKey: ['ensemble', id],
    queryFn: () => ensemblesApi.get(Number(id)),
    enabled: !!id,
  });

  const { data: seasons } = useQuery({
    queryKey: ['seasons', { ensembleId: id }],
    queryFn: () => seasonsApi.list({ ensembleIds: [Number(id)], pageSize: 100 }),
    enabled: !!id,
  });

  const { data: members } = useQuery({
    queryKey: ['ensemble-members', id],
    queryFn: () => ensemblesApi.getMembers(Number(id)),
    enabled: !!id && isAdmin(),
  });

  const { data: allUsers } = useQuery({
    queryKey: ['users'],
    queryFn: () => authApi.getUsers(),
    enabled: isAdmin(),
  });

  const addMemberMutation = useMutation({
    mutationFn: (userId: string) => ensemblesApi.addMember(Number(id), userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ensemble-members', id] });
      setSelectedUserId('');
    },
    onError: () => setError('Failed to add member'),
  });

  const removeMemberMutation = useMutation({
    mutationFn: (userId: string) => ensemblesApi.removeMember(Number(id), userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ensemble-members', id] }),
    onError: () => setError('Failed to remove member'),
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

  type EnsemblePerformance = { id: number; name: string; performanceDate?: string; link: string };

  const handleSort = (key: string) => {
    if (sortBy === key) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    else { setSortBy(key); setSortDir('asc'); }
  };

  const filteredPerformances = (ensemble.performances ?? [] as EnsemblePerformance[])
    .filter((p: EnsemblePerformance) => p.name.toLowerCase().includes(search.toLowerCase()))
    .sort((a: EnsemblePerformance, b: EnsemblePerformance) => {
      let cmp = 0;
      if (sortBy === 'name') {
        cmp = a.name.localeCompare(b.name);
      } else {
        const da = a.performanceDate ? new Date(a.performanceDate).getTime() : 0;
        const db = b.performanceDate ? new Date(b.performanceDate).getTime() : 0;
        cmp = da - db;
      }
      return sortDir === 'asc' ? cmp : -cmp;
    });

  const performanceColumns = [
    {
      key: 'name',
      label: 'Name',
      sortable: true,
      render: (p: EnsemblePerformance) => (
        <Link to={`/performances/${p.id}`} className="text-decoration-none" onClick={e => e.stopPropagation()}>
          {p.name}
        </Link>
      ),
    },
    {
      key: 'performanceDate',
      label: 'Date',
      sortable: true,
      render: (p: EnsemblePerformance) => p.performanceDate ? new Date(p.performanceDate).toLocaleDateString() : '-',
    },
    {
      key: 'link',
      label: 'Link',
      render: (p: EnsemblePerformance) => (
        <a href={p.link} target="_blank" rel="noopener noreferrer" onClick={e => e.stopPropagation()}>
          View
        </a>
      ),
    },
  ];

  return (
    <>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <div className="d-flex justify-content-between align-items-start mb-3">
        <h2>{ensemble.name}</h2>
        <div>
          {isAdmin() && (
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
              <Card.Title>Seasons</Card.Title>
              {seasons && seasons.items.length > 0 ? (
                <ListGroup variant="flush">
                  {seasons.items.map((s) => (
                    <ListGroup.Item key={s.id} className="d-flex justify-content-between align-items-center px-0">
                      <Link to={`/seasons/${s.id}`} className="fw-semibold text-decoration-none">
                        {s.name}
                      </Link>
                      <span className="text-muted small">
                        {[
                          s.startDate && new Date(s.startDate).toLocaleDateString(),
                          s.endDate && new Date(s.endDate).toLocaleDateString(),
                        ].filter(Boolean).join(' – ')}
                      </span>
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No seasons for this ensemble.</p>
              )}
            </Card.Body>
          </Card>

          <Card className="mb-3">
            <Card.Body>
              <Card.Title>Performances</Card.Title>
              {ensemble.performances && ensemble.performances.length > 0 && (
                <div className="d-flex gap-2 mb-3">
                  <Form.Control
                    size="sm"
                    placeholder="Search by name..."
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    style={{ maxWidth: '300px' }}
                  />
                </div>
              )}
              <DataTable
                columns={performanceColumns}
                data={filteredPerformances}
                sortBy={sortBy}
                sortDirection={sortDir}
                onSort={handleSort}
                onRowClick={p => navigate(`/performances/${p.id}`)}
              />
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

          {isAdmin() && (
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Members</Card.Title>
                <div className="d-flex flex-wrap gap-2 mb-3">
                  {members && members.length > 0 ? members.map((m) => (
                    <Badge key={m.id} bg="secondary" className="d-flex align-items-center gap-1 fs-6">
                      {m.firstName && m.lastName ? `${m.firstName} ${m.lastName}` : m.userName}
                      <Button
                        size="sm"
                        variant="link"
                        className="text-white p-0 ms-1"
                        onClick={() => removeMemberMutation.mutate(m.id)}
                        title="Remove member"
                      >
                        &times;
                      </Button>
                    </Badge>
                  )) : (
                    <span className="text-muted small">No members yet.</span>
                  )}
                </div>
                {allUsers && (
                  <Form
                    className="d-flex gap-2"
                    onSubmit={(e) => { e.preventDefault(); if (selectedUserId) addMemberMutation.mutate(selectedUserId); }}
                  >
                    <Form.Select
                      size="sm"
                      value={selectedUserId}
                      onChange={(e) => setSelectedUserId(e.target.value)}
                    >
                      <option value="">Add member...</option>
                      {allUsers
                        .filter((u) => !members?.some((m) => m.id === u.id))
                        .map((u) => (
                          <option key={u.id} value={u.id}>
                            {u.firstName && u.lastName ? `${u.firstName} ${u.lastName}` : u.userName}
                          </option>
                        ))}
                    </Form.Select>
                    <Button size="sm" type="submit" disabled={!selectedUserId || addMemberMutation.isPending}>
                      Add
                    </Button>
                  </Form>
                )}
              </Card.Body>
            </Card>
          )}
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
