import { useState } from 'react';
import { Alert, Button, Card, Col, Form, Row, Spinner } from 'react-bootstrap';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ensemblesApi } from '../../api/ensembles';
import ConfirmModal from '../../components/common/ConfirmModal';
import DataTable from '../../components/common/DataTable';
import { useAuth } from '../../hooks/useAuth';

export default function EnsembleDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [showDelete, setShowDelete] = useState(false);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState('name');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');

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
