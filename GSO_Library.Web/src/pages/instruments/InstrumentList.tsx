import { useState } from 'react';
import { Alert, Button, Form } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { instrumentsApi } from '../../api/instruments';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import ConfirmModal from '../../components/common/ConfirmModal';
import { useAuth } from '../../hooks/useAuth';
import type { Instrument } from '../../types';

export default function InstrumentList() {
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [deleteTarget, setDeleteTarget] = useState<Instrument | null>(null);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['instruments', { page, pageSize, sortBy, sortDirection, search }],
    queryFn: () => instrumentsApi.list({ page, pageSize, sortBy, sortDirection, search: search || undefined }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => instrumentsApi.delete(id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['instruments'] }); setDeleteTarget(null); },
    onError: () => setError('Failed to delete instrument'),
  });

  const handleSort = (key: string) => {
    if (sortBy === key) setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortBy(key); setSortDirection('asc'); }
    setPage(1);
  };

  const columns = [
    { key: 'name', label: 'Name', sortable: true },
    ...(canEdit() ? [{
      key: 'actions',
      label: '',
      render: (i: Instrument) => (
        <div className="d-flex gap-1" onClick={(e) => e.stopPropagation()}>
          <Link to={`/instruments/${i.id}/edit`} className="btn btn-sm btn-outline-primary">Edit</Link>
          <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(i)}>Delete</Button>
        </div>
      ),
    }] : []),
  ];

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>Instruments</h2>
        {canEdit() && <Link to="/instruments/new" className="btn btn-primary">New Instrument</Link>}
      </div>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
      <Form.Control
        size="sm"
        placeholder="Search by name..."
        value={search}
        onChange={(e) => { setSearch(e.target.value); setPage(1); }}
        className="mb-3"
        style={{ maxWidth: '300px' }}
      />
      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} sortBy={sortBy} sortDirection={sortDirection} onSort={handleSort} onRowClick={(i) => navigate(`/instruments/${i.id}/edit`)} />
      {data && data.totalPages > 0 && (
        <Pagination page={data.page} totalPages={data.totalPages} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(s) => { setPageSize(s); setPage(1); }} />
      )}
      <ConfirmModal show={!!deleteTarget} title="Delete Instrument" message={`Delete "${deleteTarget?.name}"?`} onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)} onCancel={() => setDeleteTarget(null)} />
    </>
  );
}
