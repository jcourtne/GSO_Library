import { useState } from 'react';
import { Alert, Button, Col, Form, Row } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { seasonsApi } from '../../api/seasons';
import { ensemblesApi } from '../../api/ensembles';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import ConfirmModal from '../../components/common/ConfirmModal';
import FilterPanelSection from '../../components/common/FilterPanel';
import { useMyEnsembles } from '../../hooks/useMyEnsembles';
import type { Season } from '../../types';

function formatDateRange(startDate?: string, endDate?: string) {
  if (!startDate && !endDate) return '-';
  const fmt = (d: string) => new Date(d).toLocaleDateString();
  if (startDate && endDate) return `${fmt(startDate)} – ${fmt(endDate)}`;
  if (startDate) return `From ${fmt(startDate)}`;
  return `Until ${fmt(endDate!)}`;
}

export default function SeasonList() {
  const navigate = useNavigate();
  const { canEditForEnsemble, canCreateForEnsemble } = useMyEnsembles();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [deleteTarget, setDeleteTarget] = useState<Season | null>(null);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [ensembleIds, setEnsembleIds] = useState<number[]>([]);
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['seasons', { page, pageSize, sortBy, sortDirection, search, ensembleIds, dateFrom, dateTo }],
    queryFn: () => seasonsApi.list({
      page, pageSize, sortBy, sortDirection,
      search: search || undefined,
      ensembleIds: ensembleIds.length ? ensembleIds : undefined,
      dateFrom: dateFrom || undefined,
      dateTo: dateTo || undefined,
    }),
  });

  const { data: ensembles } = useQuery({
    queryKey: ['ensembles', 'all'],
    queryFn: () => ensemblesApi.getAll(),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => seasonsApi.delete(id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['seasons'] }); setDeleteTarget(null); },
    onError: () => setError('Failed to delete season'),
  });

  const handleSort = (key: string) => {
    if (sortBy === key) setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortBy(key); setSortDirection('asc'); }
    setPage(1);
  };

  const clearAllFilters = () => {
    setSearch('');
    setEnsembleIds([]);
    setDateFrom('');
    setDateTo('');
    setPage(1);
  };

  const hasFilters = search || ensembleIds.length || dateFrom || dateTo;

  const columns = [
    { key: 'name', label: 'Name', sortable: true },
    {
      key: 'ensemble',
      label: 'Ensemble',
      render: (s: Season) => s.ensemble ? (
        <Link to={`/ensembles/${s.ensemble.id}`} onClick={(e) => e.stopPropagation()}>
          {s.ensemble.name}
        </Link>
      ) : '-',
    },
    {
      key: 'startdate',
      label: 'Date Range',
      sortable: true,
      render: (s: Season) => formatDateRange(s.startDate, s.endDate),
    },
    { key: 'notes', label: 'Notes', render: (s: Season) => s.notes ? s.notes.slice(0, 60) + (s.notes.length > 60 ? '…' : '') : '-' },
    ...(canCreateForEnsemble ? [{
      key: 'actions',
      label: '',
      render: (s: Season) => canEditForEnsemble(s.ensembleId) ? (
        <div className="d-flex gap-1" onClick={(e) => e.stopPropagation()}>
          <Link to={`/seasons/${s.id}/edit`} className="btn btn-sm btn-outline-primary">Edit</Link>
          <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(s)}>Delete</Button>
        </div>
      ) : null,
    }] : []),
  ];

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>Seasons</h2>
        {canCreateForEnsemble && <Link to="/seasons/new" className="btn btn-primary">New Season</Link>}
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
              label="Ensemble"
              options={ensembles?.map((e) => ({ value: e.id, label: e.name })) ?? []}
              selected={ensembleIds}
              onChange={(v) => { setEnsembleIds(v as number[]); setPage(1); }}
            />

            <div className="mb-3">
              <strong className="small text-uppercase text-muted d-block mb-1" style={{ fontSize: '0.7rem', letterSpacing: '0.05em' }}>
                Start Date Range
              </strong>
              <Form.Control
                type="date"
                size="sm"
                value={dateFrom}
                onChange={(e) => { setDateFrom(e.target.value); setPage(1); }}
                className="mb-1"
              />
              <Form.Control
                type="date"
                size="sm"
                value={dateTo}
                onChange={(e) => { setDateTo(e.target.value); setPage(1); }}
              />
            </div>

            {hasFilters ? (
              <Button variant="outline-secondary" size="sm" className="w-100 mt-1" onClick={clearAllFilters}>
                Clear all filters
              </Button>
            ) : null}
          </div>
        </Col>

        <Col md={9}>
          <DataTable
            columns={columns}
            data={data?.items ?? []}
            isLoading={isLoading}
            sortBy={sortBy}
            sortDirection={sortDirection}
            onSort={handleSort}
            onRowClick={(s) => navigate(`/seasons/${s.id}`)}
          />
          {data && data.totalPages > 0 && (
            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              pageSize={pageSize}
              onPageChange={setPage}
              onPageSizeChange={(s) => { setPageSize(s); setPage(1); }}
            />
          )}
        </Col>
      </Row>

      <ConfirmModal
        show={!!deleteTarget}
        title="Delete Season"
        message={`Delete "${deleteTarget?.name}"?`}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </>
  );
}
