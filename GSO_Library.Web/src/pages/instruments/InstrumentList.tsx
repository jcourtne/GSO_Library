import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Col, Form, ListGroup, Row } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { instrumentsApi } from '../../api/instruments';
import { instrumentSortOrdersApi } from '../../api/instrumentSortOrders';
import DataTable from '../../components/common/DataTable';
import Pagination from '../../components/common/Pagination';
import ConfirmModal from '../../components/common/ConfirmModal';
import { useAuth } from '../../hooks/useAuth';
import type { Instrument, InstrumentSortOrder } from '../../types';

export default function InstrumentList() {
  const navigate = useNavigate();
  const { canEdit, canEditReferenceData } = useAuth();
  const queryClient = useQueryClient();

  // Alphabetical list state
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [sortBy, setSortBy] = useState('name');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [search, setSearch] = useState('');

  // Sort order state — null = alphabetical, number = specific sort order
  const [activeSortOrderId, setActiveSortOrderId] = useState<number | null>(null);
  const sortOrderInitialized = useRef(false);

  // Reorder state
  const [isReorderMode, setIsReorderMode] = useState(false);
  const [reorderSorted, setReorderSorted] = useState<Instrument[]>([]);
  const [reorderUnsorted, setReorderUnsorted] = useState<Instrument[]>([]);

  // Sort order management state
  const [deleteTarget, setDeleteTarget] = useState<Instrument | null>(null);
  const [deleteSortOrderTarget, setDeleteSortOrderTarget] = useState<InstrumentSortOrder | null>(null);
  const [editingSortOrderId, setEditingSortOrderId] = useState<number | null>(null);
  const [editingSortOrderName, setEditingSortOrderName] = useState('');
  const [showNewSortOrderForm, setShowNewSortOrderForm] = useState(false);
  const [newSortOrderName, setNewSortOrderName] = useState('');
  const [error, setError] = useState('');

  // Queries
  const sortOrders = useQuery({
    queryKey: ['instrument-sort-orders'],
    queryFn: instrumentSortOrdersApi.list,
  });

  const { data: paginatedData, isLoading: paginatedLoading } = useQuery({
    queryKey: ['instruments', { page, pageSize, sortBy, sortDirection, search }],
    queryFn: () => instrumentsApi.list({ page, pageSize, sortBy, sortDirection, search: search || undefined }),
    enabled: activeSortOrderId === null && sortOrders.isFetched,
  });

  const { data: sortOrderInstruments, isLoading: sortOrderLoading } = useQuery({
    queryKey: ['instrument-sort-order-instruments', activeSortOrderId],
    queryFn: () => instrumentSortOrdersApi.getInstruments(activeSortOrderId!),
    enabled: activeSortOrderId !== null,
  });

  // Set default sort order on first load
  useEffect(() => {
    if (!sortOrderInitialized.current && sortOrders.data !== undefined) {
      sortOrderInitialized.current = true;
      const defaultSo = sortOrders.data.find((so) => so.isDefault);
      if (defaultSo) setActiveSortOrderId(defaultSo.id);
    }
  }, [sortOrders.data]);

  // Mutations — instruments
  const deleteMutation = useMutation({
    mutationFn: (id: number) => instrumentsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instruments'] });
      queryClient.invalidateQueries({ queryKey: ['instrument-sort-order-instruments'] });
      setDeleteTarget(null);
    },
    onError: () => setError('Failed to delete instrument'),
  });

  // Mutations — sort orders
  const createSortOrderMutation = useMutation({
    mutationFn: (name: string) =>
      instrumentSortOrdersApi.create({ name, isDefault: false, instrumentIds: [] }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instrument-sort-orders'] });
      setShowNewSortOrderForm(false);
      setNewSortOrderName('');
    },
    onError: () => setError('Failed to create sort order'),
  });

  const renameSortOrderMutation = useMutation({
    mutationFn: ({ so, name }: { so: InstrumentSortOrder; name: string }) =>
      instrumentSortOrdersApi.update(so.id, {
        name,
        isDefault: so.isDefault,
        instrumentIds: so.instruments.map((i) => i.id),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instrument-sort-orders'] });
      setEditingSortOrderId(null);
    },
    onError: () => setError('Failed to rename sort order'),
  });

  const setDefaultMutation = useMutation({
    mutationFn: (so: InstrumentSortOrder) =>
      instrumentSortOrdersApi.update(so.id, {
        name: so.name,
        isDefault: true,
        instrumentIds: so.instruments.map((i) => i.id),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['instrument-sort-orders'] }),
    onError: () => setError('Failed to set default sort order'),
  });

  const deleteSortOrderMutation = useMutation({
    mutationFn: (id: number) => instrumentSortOrdersApi.delete(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: ['instrument-sort-orders'] });
      if (activeSortOrderId === id) setActiveSortOrderId(null);
      setDeleteSortOrderTarget(null);
    },
    onError: () => setError('Failed to delete sort order'),
  });

  const saveReorderMutation = useMutation({
    mutationFn: () => {
      const so = sortOrders.data?.find((s) => s.id === activeSortOrderId)!;
      return instrumentSortOrdersApi.update(activeSortOrderId!, {
        name: so.name,
        isDefault: so.isDefault,
        instrumentIds: reorderSorted.map((i) => i.id),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instrument-sort-orders'] });
      queryClient.invalidateQueries({ queryKey: ['instrument-sort-order-instruments', activeSortOrderId] });
      setIsReorderMode(false);
    },
    onError: () => setError('Failed to save order'),
  });

  const handleSort = (key: string) => {
    if (sortBy === key) setSortDirection((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortBy(key); setSortDirection('asc'); }
    setPage(1);
  };

  const handleEnterReorderMode = () => {
    const currentSo = sortOrders.data?.find((so) => so.id === activeSortOrderId);
    const sortedIds = new Set(currentSo?.instruments.map((i) => i.id) ?? []);
    const sorted = (currentSo?.instruments ?? []).slice();
    const unsorted = (sortOrderInstruments ?? [])
      .filter((i) => !sortedIds.has(i.id))
      .sort((a, b) => a.name.localeCompare(b.name));
    setReorderSorted(sorted);
    setReorderUnsorted(unsorted);
    setIsReorderMode(true);
  };

  const handleMoveUp = (index: number) => {
    if (index === 0) return;
    const next = [...reorderSorted];
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    setReorderSorted(next);
  };

  const handleMoveDown = (index: number) => {
    if (index === reorderSorted.length - 1) return;
    const next = [...reorderSorted];
    [next[index], next[index + 1]] = [next[index + 1], next[index]];
    setReorderSorted(next);
  };

  const handleAddToSort = (instrument: Instrument) => {
    setReorderSorted([...reorderSorted, instrument]);
    setReorderUnsorted(reorderUnsorted.filter((i) => i.id !== instrument.id));
  };

  const handleRemoveFromSort = (instrument: Instrument) => {
    setReorderSorted(reorderSorted.filter((i) => i.id !== instrument.id));
    setReorderUnsorted([...reorderUnsorted, instrument].sort((a, b) => a.name.localeCompare(b.name)));
  };

  const filteredSortOrderInstruments = (sortOrderInstruments ?? []).filter(
    (i) => !search || i.name.toLowerCase().includes(search.toLowerCase()),
  );

  const filteredReorderUnsorted = reorderUnsorted.filter(
    (i) => !search || i.name.toLowerCase().includes(search.toLowerCase()),
  );

  const actionColumn = canEditReferenceData() ? [{
    key: 'actions',
    label: '',
    render: (i: Instrument) => (
      <div className="d-flex gap-1" onClick={(e) => e.stopPropagation()}>
        <Link to={`/instruments/${i.id}/edit`} className="btn btn-sm btn-outline-primary">Edit</Link>
        <Button size="sm" variant="outline-danger" onClick={() => setDeleteTarget(i)}>Delete</Button>
      </div>
    ),
  }] : [];

  const tableColumns = [
    { key: 'name', label: 'Name', sortable: activeSortOrderId === null },
    { key: 'familyName', label: 'Family', render: (i: Instrument) => i.familyName ?? '—' },
    ...actionColumn,
  ];

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>Instruments</h2>
        {canEditReferenceData() && <Link to="/instruments/new" className="btn btn-primary">New Instrument</Link>}
      </div>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Row className="g-3">
        {/* Left panel */}
        <Col md={3} style={{ borderRight: '1px solid var(--bs-border-color)' }}>
          <div className="pe-2">
            <Form.Control
              size="sm"
              placeholder="Search by name..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            />

            <div className="mt-3">
              <Form.Label className="fw-semibold small">Sort Order</Form.Label>
              <Form.Select
                size="sm"
                value={activeSortOrderId ?? ''}
                onChange={(e) => {
                  setActiveSortOrderId(e.target.value ? Number(e.target.value) : null);
                  setIsReorderMode(false);
                }}
                className="mb-2"
              >
                <option value="">Alphabetical</option>
                {sortOrders.data?.map((so) => (
                  <option key={so.id} value={so.id}>{so.name}{so.isDefault ? ' ★' : ''}</option>
                ))}
              </Form.Select>

              {sortOrders.data?.map((so) => (
                <div key={so.id} className="d-flex align-items-center gap-1 mb-1 small">
                  <Button
                    variant={so.isDefault ? 'warning' : 'outline-secondary'}
                    size="sm"
                    title={so.isDefault ? 'Default' : 'Set as default'}
                    disabled={so.isDefault}
                    onClick={() => setDefaultMutation.mutate(so)}
                    style={{ padding: '0 5px', lineHeight: 1.5 }}
                  >
                    ★
                  </Button>
                  {editingSortOrderId === so.id ? (
                    <>
                      <Form.Control
                        size="sm"
                        value={editingSortOrderName}
                        onChange={(e) => setEditingSortOrderName(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') renameSortOrderMutation.mutate({ so, name: editingSortOrderName });
                          if (e.key === 'Escape') setEditingSortOrderId(null);
                        }}
                        style={{ flex: 1 }}
                        autoFocus
                      />
                      <Button size="sm" variant="outline-success" onClick={() => renameSortOrderMutation.mutate({ so, name: editingSortOrderName })} style={{ padding: '0 5px', lineHeight: 1.5 }}>✓</Button>
                      <Button size="sm" variant="outline-secondary" onClick={() => setEditingSortOrderId(null)} style={{ padding: '0 5px', lineHeight: 1.5 }}>✕</Button>
                    </>
                  ) : (
                    <>
                      <span className="flex-fill text-truncate" title={so.name}>{so.name}</span>
                      {canEdit() && (
                        <>
                          <Button
                            size="sm"
                            variant="outline-secondary"
                            title="Rename"
                            onClick={() => { setEditingSortOrderId(so.id); setEditingSortOrderName(so.name); }}
                            style={{ padding: '0 5px', lineHeight: 1.5 }}
                          >
                            ✎
                          </Button>
                          <Button
                            size="sm"
                            variant="outline-danger"
                            title="Delete"
                            onClick={() => setDeleteSortOrderTarget(so)}
                            style={{ padding: '0 5px', lineHeight: 1.5 }}
                          >
                            ✕
                          </Button>
                        </>
                      )}
                    </>
                  )}
                </div>
              ))}

              {canEdit() && (
                showNewSortOrderForm ? (
                  <div className="d-flex gap-1 mt-2">
                    <Form.Control
                      size="sm"
                      placeholder="Name..."
                      value={newSortOrderName}
                      onChange={(e) => setNewSortOrderName(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' && newSortOrderName.trim()) createSortOrderMutation.mutate(newSortOrderName.trim());
                        if (e.key === 'Escape') { setShowNewSortOrderForm(false); setNewSortOrderName(''); }
                      }}
                      autoFocus
                      style={{ flex: 1 }}
                    />
                    <Button size="sm" variant="outline-success" onClick={() => newSortOrderName.trim() && createSortOrderMutation.mutate(newSortOrderName.trim())} style={{ padding: '0 5px', lineHeight: 1.5 }}>✓</Button>
                    <Button size="sm" variant="outline-secondary" onClick={() => { setShowNewSortOrderForm(false); setNewSortOrderName(''); }} style={{ padding: '0 5px', lineHeight: 1.5 }}>✕</Button>
                  </div>
                ) : (
                  <Button size="sm" variant="outline-secondary" className="mt-2 w-100" onClick={() => setShowNewSortOrderForm(true)}>
                    + New Sort Order
                  </Button>
                )
              )}
            </div>
          </div>
        </Col>

        {/* Right panel */}
        <Col md={9}>
          {isReorderMode ? (
            <>
              <div className="d-flex gap-2 mb-3">
                <Button size="sm" onClick={() => saveReorderMutation.mutate()} disabled={saveReorderMutation.isPending}>
                  Save Order
                </Button>
                <Button size="sm" variant="secondary" onClick={() => setIsReorderMode(false)}>
                  Cancel
                </Button>
              </div>

              <div className="mb-3">
                <div className="fw-semibold small text-muted mb-1">In this order</div>
                {reorderSorted.length === 0 ? (
                  <p className="text-muted small">No instruments in this order yet.</p>
                ) : (
                  <ListGroup>
                    {reorderSorted.map((i, idx) => (
                      <ListGroup.Item key={i.id} className="d-flex align-items-center gap-2 py-1">
                        <div className="d-flex flex-column gap-0" style={{ lineHeight: 1 }}>
                          <Button variant="outline-secondary" size="sm" onClick={() => handleMoveUp(idx)} disabled={idx === 0} style={{ padding: '0 5px', lineHeight: 1.3 }}>▲</Button>
                          <Button variant="outline-secondary" size="sm" onClick={() => handleMoveDown(idx)} disabled={idx === reorderSorted.length - 1} style={{ padding: '0 5px', lineHeight: 1.3 }}>▼</Button>
                        </div>
                        <span className="flex-fill">{i.name}</span>
                        <Button variant="outline-danger" size="sm" onClick={() => handleRemoveFromSort(i)}>Remove</Button>
                      </ListGroup.Item>
                    ))}
                  </ListGroup>
                )}
              </div>

              <div>
                <div className="fw-semibold small text-muted mb-1">Not in this order</div>
                {filteredReorderUnsorted.length === 0 ? (
                  <p className="text-muted small">{search ? 'No matches.' : 'All instruments are in this order.'}</p>
                ) : (
                  <ListGroup>
                    {filteredReorderUnsorted.map((i) => (
                      <ListGroup.Item key={i.id} className="d-flex align-items-center gap-2 py-1">
                        <span className="flex-fill">{i.name}</span>
                        <Button variant="outline-primary" size="sm" onClick={() => handleAddToSort(i)}>Add</Button>
                      </ListGroup.Item>
                    ))}
                  </ListGroup>
                )}
              </div>
            </>
          ) : (
            <>
              {activeSortOrderId !== null && canEdit() && (
                <div className="mb-2">
                  <Button size="sm" variant="outline-secondary" onClick={handleEnterReorderMode}>
                    Reorder
                  </Button>
                </div>
              )}
              <DataTable
                columns={tableColumns}
                data={activeSortOrderId !== null ? filteredSortOrderInstruments : (paginatedData?.items ?? [])}
                isLoading={activeSortOrderId !== null ? sortOrderLoading : paginatedLoading}
                sortBy={activeSortOrderId === null ? sortBy : undefined}
                sortDirection={activeSortOrderId === null ? sortDirection : undefined}
                onSort={activeSortOrderId === null ? handleSort : undefined}
                onRowClick={(i) => navigate(`/instruments/${i.id}/edit`)}
              />
              {activeSortOrderId === null && paginatedData && paginatedData.totalPages > 0 && (
                <Pagination
                  page={paginatedData.page}
                  totalPages={paginatedData.totalPages}
                  pageSize={pageSize}
                  onPageChange={setPage}
                  onPageSizeChange={(s) => { setPageSize(s); setPage(1); }}
                />
              )}
            </>
          )}
        </Col>
      </Row>

      <ConfirmModal
        show={!!deleteTarget}
        title="Delete Instrument"
        message={`Delete "${deleteTarget?.name}"?`}
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
      <ConfirmModal
        show={!!deleteSortOrderTarget}
        title="Delete Sort Order"
        message={`Delete sort order "${deleteSortOrderTarget?.name}"?`}
        onConfirm={() => deleteSortOrderTarget && deleteSortOrderMutation.mutate(deleteSortOrderTarget.id)}
        onCancel={() => setDeleteSortOrderTarget(null)}
      />
    </>
  );
}
