import { useEffect, useState } from 'react';
import { Badge, Button, Col, Form, ListGroup, Modal, OverlayTrigger, Row, Spinner, Tooltip } from 'react-bootstrap';
import { useQuery } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import FilterPanelSection from '../common/FilterPanel';
import type { Arrangement } from '../../types';

interface Props {
  show: boolean;
  onHide: () => void;
  excludeIds: Set<number>;
  onSelect: (id: number) => void;
  isPending: boolean;
  /**
   * Return a reason string to disable the "Add" button for this arrangement (shown as a
   * tooltip), or undefined to allow it.
   */
  getAddDisabledReason?: (a: Arrangement) => string | undefined;
  /**
   * When provided, a left-panel checkbox appears that filters the list to arrangements for
   * which this returns true (e.g. ones the user has full download permission for). Filtering
   * is applied to the current page of results.
   */
  canDownload?: (a: Arrangement) => boolean;
}

export default function ArrangementPickerModal({ show, onHide, excludeIds, onSelect, isPending, getAddDisabledReason, canDownload }: Props) {
  const [search, setSearch] = useState('');
  const [gameIds, setGameIds] = useState<number[]>([]);
  const [seriesIds, setSeriesIds] = useState<number[]>([]);
  const [instrumentIds, setInstrumentIds] = useState<number[]>([]);
  const [instrumentMatchAll, setInstrumentMatchAll] = useState(false);
  const [composers, setComposers] = useState<string[]>([]);
  const [arrangers, setArrangers] = useState<string[]>([]);
  const [onlyDownloadable, setOnlyDownloadable] = useState(false);
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (show) {
      setSearch('');
      setGameIds([]);
      setSeriesIds([]);
      setInstrumentIds([]);
      setInstrumentMatchAll(false);
      setComposers([]);
      setArrangers([]);
      setOnlyDownloadable(false);
      setPage(1);
    }
  }, [show]);

  const { data: filterOptions } = useQuery({
    queryKey: ['arrangement-filter-options'],
    queryFn: () => arrangementsApi.filterOptions(),
  });

  const { data, isLoading } = useQuery({
    queryKey: ['arrangements-picker', { search, gameIds, seriesIds, instrumentIds, instrumentMatchAll, composers, arrangers, page }],
    queryFn: () => arrangementsApi.list({
      search: search || undefined,
      gameIds: gameIds.length ? gameIds : undefined,
      seriesIds: seriesIds.length ? seriesIds : undefined,
      instrumentIds: instrumentIds.length ? instrumentIds : undefined,
      instrumentMatchAll: instrumentMatchAll || undefined,
      composers: composers.length ? composers : undefined,
      arrangers: arrangers.length ? arrangers : undefined,
      sortBy: 'updatedAt',
      sortDirection: 'desc',
      page,
      pageSize: 20,
    }),
    enabled: show,
  });

  const hasFilters = !!(search || gameIds.length || seriesIds.length || instrumentIds.length || composers.length || arrangers.length);

  const clearAll = () => {
    setSearch('');
    setGameIds([]);
    setSeriesIds([]);
    setInstrumentIds([]);
    setInstrumentMatchAll(false);
    setComposers([]);
    setArrangers([]);
    setPage(1);
  };

  const results = (data?.items ?? [])
    .filter((a) => !excludeIds.has(a.id))
    .filter((a) => !onlyDownloadable || !canDownload || canDownload(a));

  return (
    <Modal show={show} onHide={onHide} size="xl">
      <Modal.Header closeButton>
        <Modal.Title>Add Arrangement</Modal.Title>
      </Modal.Header>
      <Modal.Body style={{ minHeight: 400 }}>
        <Row className="g-0 h-100">
          <Col md={3} style={{ borderRight: '1px solid var(--bs-border-color)' }}>
            <div className="pe-3">
              <Form.Control
                size="sm"
                placeholder="Search by name..."
                value={search}
                onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                className="mb-3"
              />
              {canDownload && (
                <Form.Check
                  type="checkbox"
                  id="picker-only-downloadable"
                  label="Only arrangements I can download"
                  checked={onlyDownloadable}
                  onChange={(e) => { setOnlyDownloadable(e.target.checked); setPage(1); }}
                  className="mb-3 small"
                />
              )}
              <FilterPanelSection
                label="Games"
                options={filterOptions?.games.map((g) => ({ value: g.id, label: g.name })) ?? []}
                selected={gameIds}
                onChange={(v) => { setGameIds(v as number[]); setPage(1); }}
              />
              <FilterPanelSection
                label="Series"
                options={filterOptions?.series.map((s) => ({ value: s.id, label: s.name })) ?? []}
                selected={seriesIds}
                onChange={(v) => { setSeriesIds(v as number[]); setPage(1); }}
              />
              <FilterPanelSection
                label="Instruments"
                options={filterOptions?.instruments.map((i) => ({ value: i.id, label: i.name })) ?? []}
                selected={instrumentIds}
                onChange={(v) => { setInstrumentIds(v as number[]); setPage(1); }}
                matchMode={instrumentMatchAll ? 'all' : 'any'}
                onMatchModeChange={(m) => { setInstrumentMatchAll(m === 'all'); setPage(1); }}
              />
              <FilterPanelSection
                label="Composers"
                options={filterOptions?.composers.map((c) => ({ value: c, label: c })) ?? []}
                selected={composers}
                onChange={(v) => { setComposers(v as string[]); setPage(1); }}
              />
              <FilterPanelSection
                label="Arrangers"
                options={filterOptions?.arrangers.map((a) => ({ value: a, label: a })) ?? []}
                selected={arrangers}
                onChange={(v) => { setArrangers(v as string[]); setPage(1); }}
              />
              {hasFilters && (
                <Button variant="outline-secondary" size="sm" className="w-100 mt-1" onClick={clearAll}>
                  Clear all filters
                </Button>
              )}
            </div>
          </Col>

          <Col md={9}>
            <div className="ps-3">
              {isLoading ? (
                <div className="text-center py-5"><Spinner animation="border" /></div>
              ) : (
                <ListGroup variant="flush">
                  {results.map((a) => {
                    const disabledReason = getAddDisabledReason?.(a);
                    const addButton = (
                      <Button
                        size="sm"
                        variant="outline-primary"
                        className="flex-shrink-0 ms-3"
                        onClick={() => onSelect(a.id)}
                        disabled={isPending || !!disabledReason}
                      >
                        Add
                      </Button>
                    );
                    return (
                    <ListGroup.Item key={a.id} className="d-flex justify-content-between align-items-start px-0">
                      <div>
                        <div className="fw-semibold">{a.name}</div>
                        <div className="text-muted small">
                          {[
                            a.composers?.length > 0 && `Composed by ${a.composers.join(', ')}`,
                            a.arrangers?.length > 0 && `Arranged by ${a.arrangers.join(', ')}`,
                          ].filter(Boolean).join(' · ')}
                        </div>
                        {a.games && a.games.length > 0 && (
                          <div className="mt-1 d-flex flex-wrap gap-1">
                            {a.games.map((g) => (
                              <Badge key={g.id} bg="info" className="fw-normal" style={{ fontSize: '0.7rem' }}>{g.name}</Badge>
                            ))}
                          </div>
                        )}
                      </div>
                      {disabledReason ? (
                        <OverlayTrigger overlay={<Tooltip id={`add-disabled-${a.id}`}>{disabledReason}</Tooltip>}>
                          <span className="flex-shrink-0 ms-3 d-inline-block">{addButton}</span>
                        </OverlayTrigger>
                      ) : addButton}
                    </ListGroup.Item>
                    );
                  })}
                  {results.length === 0 && !isLoading && (
                    <ListGroup.Item className="text-muted px-0">No arrangements found.</ListGroup.Item>
                  )}
                </ListGroup>
              )}

              {data && data.totalPages > 1 && (
                <div className="d-flex justify-content-center align-items-center gap-2 mt-3">
                  <Button size="sm" variant="outline-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    Previous
                  </Button>
                  <span className="small text-muted">Page {page} of {data.totalPages}</span>
                  <Button size="sm" variant="outline-secondary" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
                    Next
                  </Button>
                </div>
              )}
            </div>
          </Col>
        </Row>
      </Modal.Body>
    </Modal>
  );
}
