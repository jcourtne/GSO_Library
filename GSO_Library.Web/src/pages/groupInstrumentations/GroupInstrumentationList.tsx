import { useState } from 'react';
import { Alert, Button, Col, Form, Row } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { groupInstrumentationsApi } from '../../api/groupInstrumentations';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import ConfirmModal from '../../components/common/ConfirmModal';
import { useAuth } from '../../hooks/useAuth';
import type { GroupInstrumentation } from '../../types';

export default function GroupInstrumentationList() {
  const navigate = useNavigate();
  const { canEditReferenceData } = useAuth();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [deleteTarget, setDeleteTarget] = useState<GroupInstrumentation | null>(null);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['group-instrumentations', { page, pageSize, sortBy, sortDirection, search }],
    queryFn: () => groupInstrumentationsApi.list({ page, pageSize, sortBy, sortDirection, search: search || undefined }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => groupInstrumentationsApi.delete(id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['group-instrumentations'] }); setDeleteTarget(null); },
    onError: () => setError('Failed to delete group instrumentation'),
  });

  const handleSort = (key: string) => {
    if (sortBy === key) setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortBy(key); setSortDirection('asc'); }
    setPage(1);
  };

  const columns = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'description', label: 'Description', render: (g: GroupInstrumentation) => g.description || '-' },
    ...(canEditReferenceData() ? [{
      key: 'actions',
      label: '',
      render: (g: GroupInstrumentation) => (
        <div className="d-flex gap-1" onClick={(e) => e.stopPropagation()}>
          <Link to={`/group-instrumentations/${g.id}/edit`} className="btn btn-sm btn-outline-primary">Edit</Link>
          <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(g)}>Delete</Button>
        </div>
      ),
    }] : []),
  ];

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>Group Instrumentations</h2>
        {canEditReferenceData() && <Link to="/group-instrumentations/new" className="btn btn-primary">New Group Instrumentation</Link>}
      </div>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Row className="g-3">
        <Col md={3} style={{ borderRight: '1px solid var(--bs-border-color)' }}>
          <div className="pe-2">
            <Form.Control
              size="sm"
              placeholder="Search by name..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              className="mb-3"
            />

            {search ? (
              <Button variant="outline-secondary" size="sm" className="w-100 mt-1" onClick={() => { setSearch(''); setPage(1); }}>
                Clear all filters
              </Button>
            ) : null}
          </div>
        </Col>

        <Col md={9}>
          <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} sortBy={sortBy} sortDirection={sortDirection} onSort={handleSort} onRowClick={(g) => navigate(`/group-instrumentations/${g.id}/edit`)} />
          {data && data.totalPages > 0 && (
            <Pagination page={data.page} totalPages={data.totalPages} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(s) => { setPageSize(s); setPage(1); }} />
          )}
        </Col>
      </Row>

      <ConfirmModal show={!!deleteTarget} title="Delete Group Instrumentation" message={`Delete "${deleteTarget?.name}"?`} onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)} onCancel={() => setDeleteTarget(null)} />
    </>
  );
}
