import { useState } from 'react';
import { Col, Form, Row } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import { gamesApi } from '../../api/games';
import { seriesApi } from '../../api/series';
import { instrumentsApi } from '../../api/instruments';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
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
          <Form.Select
            size="sm"
            value={gameId ?? ''}
            onChange={(e) => { setGameId(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
          >
            <option value="">All Games</option>
            {allGames.data?.items.map((g) => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </Form.Select>
        </Col>
        <Col md={3}>
          <Form.Select
            size="sm"
            value={seriesId ?? ''}
            onChange={(e) => { setSeriesId(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
          >
            <option value="">All Series</option>
            {allSeries.data?.items.map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </Form.Select>
        </Col>
        <Col md={3}>
          <Form.Select
            size="sm"
            value={instrumentId ?? ''}
            onChange={(e) => { setInstrumentId(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
          >
            <option value="">All Instruments</option>
            {allInstruments.data?.items.map((i) => (
              <option key={i.id} value={i.id}>{i.name}</option>
            ))}
          </Form.Select>
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
