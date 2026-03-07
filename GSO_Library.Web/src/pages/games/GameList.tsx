import { useState } from 'react';
import { Alert, Button, Col, Form, Row } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { gamesApi } from '../../api/games';
import { seriesApi } from '../../api/series';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import ConfirmModal from '../../components/common/ConfirmModal';
import FilterPanelSection from '../../components/common/FilterPanel';
import { useAuth } from '../../hooks/useAuth';
import type { Game } from '../../types';

export default function GameList() {
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [deleteTarget, setDeleteTarget] = useState<Game | null>(null);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [seriesIds, setSeriesIds] = useState<number[]>([]);

  const { data, isLoading } = useQuery({
    queryKey: ['games', { page, pageSize, sortBy, sortDirection, search, seriesIds }],
    queryFn: () => gamesApi.list({ page, pageSize, sortBy, sortDirection, search: search || undefined, seriesIds: seriesIds.length ? seriesIds : undefined }),
  });

  const allSeries = useQuery({ queryKey: ['series-all'], queryFn: () => seriesApi.list({ page: 1, pageSize: 100 }) });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => gamesApi.delete(id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['games'] }); setDeleteTarget(null); },
    onError: () => setError('Failed to delete game'),
  });

  const handleSort = (key: string) => {
    if (sortBy === key) setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortBy(key); setSortDirection('asc'); }
    setPage(1);
  };

  const clearAllFilters = () => {
    setSearch('');
    setSeriesIds([]);
    setPage(1);
  };

  const hasFilters = search || seriesIds.length;

  const columns = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'description', label: 'Description', render: (g: Game) => g.description || '-' },
    { key: 'series', label: 'Series', render: (g: Game) => g.series?.name || '-' },
    ...(canEdit() ? [{
      key: 'actions',
      label: '',
      render: (g: Game) => (
        <div className="d-flex gap-1" onClick={(e) => e.stopPropagation()}>
          <Link to={`/games/${g.id}/edit`} className="btn btn-sm btn-outline-primary">Edit</Link>
          <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(g)}>Delete</Button>
        </div>
      ),
    }] : []),
  ];

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>Games</h2>
        {canEdit() && <Link to="/games/new" className="btn btn-primary">New Game</Link>}
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

            <FilterPanelSection
              label="Series"
              options={allSeries.data?.items.map((s) => ({ value: s.id, label: s.name })) ?? []}
              selected={seriesIds}
              onChange={(v) => { setSeriesIds(v as number[]); setPage(1); }}
            />

            {hasFilters ? (
              <Button variant="outline-secondary" size="sm" className="w-100 mt-1" onClick={clearAllFilters}>
                Clear all filters
              </Button>
            ) : null}
          </div>
        </Col>

        <Col md={9}>
          <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} sortBy={sortBy} sortDirection={sortDirection} onSort={handleSort} onRowClick={(g) => navigate(`/games/${g.id}/edit`)} />
          {data && data.totalPages > 0 && (
            <Pagination page={data.page} totalPages={data.totalPages} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(s) => { setPageSize(s); setPage(1); }} />
          )}
        </Col>
      </Row>

      <ConfirmModal show={!!deleteTarget} title="Delete Game" message={`Delete "${deleteTarget?.name}"?`} onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)} onCancel={() => setDeleteTarget(null)} />
    </>
  );
}
