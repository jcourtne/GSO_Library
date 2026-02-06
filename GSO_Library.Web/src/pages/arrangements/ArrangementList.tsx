import { useState } from 'react';
import { Col, Row } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import { gamesApi } from '../../api/games';
import { seriesApi } from '../../api/series';
import { instrumentsApi } from '../../api/instruments';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import SearchableSelect from '../../components/common/SearchableSelect';
import { useAuth } from '../../hooks/useAuth';
import type { Arrangement } from '../../types';

export default function ArrangementList() {
  const navigate = useNavigate();
  const { canEdit } = useAuth();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [gameId, setGameId] = useState<number | undefined>();
  const [seriesId, setSeriesId] = useState<number | undefined>();
  const [instrumentId, setInstrumentId] = useState<number | undefined>();

  const { data, isLoading } = useQuery({
    queryKey: ['arrangements', { page, pageSize, sortBy, sortDirection, gameId, seriesId, instrumentId }],
    queryFn: () => arrangementsApi.list({ page, pageSize, sortBy, sortDirection, gameId, seriesId, instrumentId }),
  });

  const allGames = useQuery({ queryKey: ['games-all'], queryFn: () => gamesApi.list({ page: 1, pageSize: 100 }) });
  const allSeries = useQuery({ queryKey: ['series-all'], queryFn: () => seriesApi.list({ page: 1, pageSize: 100 }) });
  const allInstruments = useQuery({ queryKey: ['instruments-all'], queryFn: () => instrumentsApi.list({ page: 1, pageSize: 100 }) });

  const handleSort = (key: string) => {
    if (sortBy === key) {
      setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(key);
      setSortDirection('asc');
    }
    setPage(1);
  };

  const columns = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'composer', label: 'Composer', sortable: true },
    { key: 'arranger', label: 'Arranger', sortable: true },
    { key: 'key', label: 'Key', sortable: true },
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

      <Row className="g-2 mb-3">
        <Col md={3}>
          <SearchableSelect
            size="sm"
            placeholder="All Games"
            options={allGames.data?.items.map((g) => ({ value: g.id, label: g.name })) ?? []}
            value={gameId ?? null}
            onChange={(v) => { setGameId(v ?? undefined); setPage(1); }}
          />
        </Col>
        <Col md={3}>
          <SearchableSelect
            size="sm"
            placeholder="All Series"
            options={allSeries.data?.items.map((s) => ({ value: s.id, label: s.name })) ?? []}
            value={seriesId ?? null}
            onChange={(v) => { setSeriesId(v ?? undefined); setPage(1); }}
          />
        </Col>
        <Col md={3}>
          <SearchableSelect
            size="sm"
            placeholder="All Instruments"
            options={allInstruments.data?.items.map((i) => ({ value: i.id, label: i.name })) ?? []}
            value={instrumentId ?? null}
            onChange={(v) => { setInstrumentId(v ?? undefined); setPage(1); }}
          />
        </Col>
      </Row>

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
    </>
  );
}
