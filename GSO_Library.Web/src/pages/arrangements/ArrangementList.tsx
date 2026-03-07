import { useState } from 'react';
import { Button, Col, Form, Row } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import { gamesApi } from '../../api/games';
import { seriesApi } from '../../api/series';
import { instrumentsApi } from '../../api/instruments';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import FilterPanelSection from '../../components/common/FilterPanel';
import { useAuth } from '../../hooks/useAuth';
import type { Arrangement } from '../../types';

export default function ArrangementList() {
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [gameIds, setGameIds] = useState<number[]>([]);
  const [seriesIds, setSeriesIds] = useState<number[]>([]);
  const [instrumentIds, setInstrumentIds] = useState<number[]>([]);
  const [composers, setComposers] = useState<string[]>([]);
  const [arrangers, setArrangers] = useState<string[]>([]);
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['arrangements', { page, pageSize, sortBy, sortDirection, gameIds, seriesIds, instrumentIds, composers, arrangers, search }],
    queryFn: () => arrangementsApi.list({
      page, pageSize, sortBy, sortDirection,
      gameIds: gameIds.length ? gameIds : undefined,
      seriesIds: seriesIds.length ? seriesIds : undefined,
      instrumentIds: instrumentIds.length ? instrumentIds : undefined,
      composers: composers.length ? composers : undefined,
      arrangers: arrangers.length ? arrangers : undefined,
      search: search || undefined,
    }),
  });

  const allGames = useQuery({ queryKey: ['games-all'], queryFn: () => gamesApi.list({ page: 1, pageSize: 100 }) });
  const allSeries = useQuery({ queryKey: ['series-all'], queryFn: () => seriesApi.list({ page: 1, pageSize: 100 }) });
  const allInstruments = useQuery({ queryKey: ['instruments-all'], queryFn: () => instrumentsApi.list({ page: 1, pageSize: 100 }) });
  const filterOptions = useQuery({ queryKey: ['arrangement-filter-options'], queryFn: () => arrangementsApi.filterOptions() });

  const handleSort = (key: string) => {
    if (sortBy === key) {
      setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(key);
      setSortDirection('asc');
    }
    setPage(1);
  };

  const clearAllFilters = () => {
    setSearch('');
    setGameIds([]);
    setSeriesIds([]);
    setInstrumentIds([]);
    setComposers([]);
    setArrangers([]);
    setPage(1);
  };

  const hasFilters = search || gameIds.length || seriesIds.length || instrumentIds.length || composers.length || arrangers.length;

  const columns = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'composer', label: 'Composer', sortable: true, render: (a: Arrangement) => a.composers?.join(', ') || '-' },
    { key: 'arranger', label: 'Arranger', sortable: true, render: (a: Arrangement) => a.arrangers?.join(', ') || '-' },
    {
      key: 'games',
      label: 'Games',
      render: (a: Arrangement) => a.games?.map((g) => g.name).join(', ') || '-',
    },
    {
      key: 'year',
      label: 'Year',
      sortable: true,
      render: (a: Arrangement) => a.year || '-',
    },
  ];

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>Arrangements</h2>
        {canEdit() && (
          <Link to="/arrangements/new" className="btn btn-primary">New Arrangement</Link>
        )}
      </div>

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
              label="Games"
              options={allGames.data?.items.map((g) => ({ value: g.id, label: g.name })) ?? []}
              selected={gameIds}
              onChange={(v) => { setGameIds(v as number[]); setPage(1); }}
            />
            <FilterPanelSection
              label="Series"
              options={allSeries.data?.items.map((s) => ({ value: s.id, label: s.name })) ?? []}
              selected={seriesIds}
              onChange={(v) => { setSeriesIds(v as number[]); setPage(1); }}
            />
            <FilterPanelSection
              label="Instruments"
              options={allInstruments.data?.items.map((i) => ({ value: i.id, label: i.name })) ?? []}
              selected={instrumentIds}
              onChange={(v) => { setInstrumentIds(v as number[]); setPage(1); }}
            />
            <FilterPanelSection
              label="Composers"
              options={filterOptions.data?.composers.map((c) => ({ value: c, label: c })) ?? []}
              selected={composers}
              onChange={(v) => { setComposers(v as string[]); setPage(1); }}
            />
            <FilterPanelSection
              label="Arrangers"
              options={filterOptions.data?.arrangers.map((a) => ({ value: a, label: a })) ?? []}
              selected={arrangers}
              onChange={(v) => { setArrangers(v as string[]); setPage(1); }}
            />

            {hasFilters && (
              <Button variant="outline-secondary" size="sm" className="w-100 mt-1" onClick={clearAllFilters}>
                Clear all filters
              </Button>
            )}
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
            onRowClick={(a) => navigate(`/arrangements/${a.id}`)}
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
    </>
  );
}
