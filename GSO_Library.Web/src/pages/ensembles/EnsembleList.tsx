import { useState } from 'react';
import { Alert, Button } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ensemblesApi } from '../../api/ensembles';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import ConfirmModal from '../../components/common/ConfirmModal';
import { useAuth } from '../../hooks/useAuth';
import type { Ensemble } from '../../types';

export default function EnsembleList() {
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [deleteTarget, setDeleteTarget] = useState<Ensemble | null>(null);
  const [error, setError] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['ensembles', { page, pageSize, sortBy, sortDirection }],
    queryFn: () => ensemblesApi.list({ page, pageSize, sortBy, sortDirection }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => ensemblesApi.delete(id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['ensembles'] }); setDeleteTarget(null); },
    onError: () => setError('Failed to delete ensemble'),
  });

  const handleSort = (key: string) => {
    if (sortBy === key) setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortBy(key); setSortDirection('asc'); }
    setPage(1);
  };

  const columns = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'description', label: 'Description', render: (e: Ensemble) => e.description || '-' },
    {
      key: 'website',
      label: 'Website',
      render: (e: Ensemble) => e.website ? (
        <a href={e.website} target="_blank" rel="noopener noreferrer" onClick={(ev) => ev.stopPropagation()}>
          Visit
        </a>
      ) : '-',
    },
    ...(canEdit() ? [{
      key: 'actions',
      label: '',
      render: (e: Ensemble) => (
        <div className="d-flex gap-1" onClick={(ev) => ev.stopPropagation()}>
          <Link to={`/ensembles/${e.id}/edit`} className="btn btn-sm btn-outline-primary">Edit</Link>
          <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(e)}>Delete</Button>
        </div>
      ),
    }] : []),
  ];

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>Ensembles</h2>
        {canEdit() && <Link to="/ensembles/new" className="btn btn-primary">New Ensemble</Link>}
      </div>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} sortBy={sortBy} sortDirection={sortDirection} onSort={handleSort} onRowClick={(e) => navigate(`/ensembles/${e.id}`)} />
      {data && data.totalPages > 0 && (
        <Pagination page={data.page} totalPages={data.totalPages} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(s) => { setPageSize(s); setPage(1); }} />
      )}
      <ConfirmModal show={!!deleteTarget} title="Delete Ensemble" message={`Delete "${deleteTarget?.name}"?`} onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)} onCancel={() => setDeleteTarget(null)} />
    </>
  );
}
