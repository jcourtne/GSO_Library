import { useEffect, useState } from 'react';
import { Alert, Badge, Button, Card, Col, Form, ListGroup, Modal, Row, Spinner } from 'react-bootstrap';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { arrangementsApi } from '../../api/arrangements';
import { authApi } from '../../api/auth';
import { ensemblesApi } from '../../api/ensembles';
import { gamesApi } from '../../api/games';
import { instrumentsApi } from '../../api/instruments';
import { instrumentSortOrdersApi } from '../../api/instrumentSortOrders';
import { useAuth } from '../../hooks/useAuth';
import FileSection from '../../components/arrangements/FileSection';
import RenderedScoreGrid from '../../components/arrangements/RenderedScoreGrid';
import QuickCreateGameModal from '../../components/common/QuickCreateGameModal';
import QuickCreateInstrumentModal from '../../components/common/QuickCreateInstrumentModal';
import { categorizeFiles, NOTATION_ACCEPT, PLAYBACK_ACCEPT } from '../../utils/fileCategories';
import { useDragAutoScroll } from '../../hooks/useDragAutoScroll';
import type { ArrangementRequest } from '../../types';

export default function ArrangementForm() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  useDragAutoScroll();
  const { canEdit } = useAuth();
  const [error, setError] = useState('');

  const [form, setForm] = useState<ArrangementRequest>({
    name: '',
    description: '',
    arrangers: [],
    composers: [],
    durationSeconds: undefined,
    year: undefined,
  });
  const [composerInput, setComposerInput] = useState('');
  const [arrangerInput, setArrangerInput] = useState('');

  // Load existing arrangement for edit mode
  const { data: existing, isLoading: loadingExisting } = useQuery({
    queryKey: ['arrangement', id],
    queryFn: () => arrangementsApi.get(Number(id)),
    enabled: isEdit,
  });

  // Load files for edit mode
  const { data: files } = useQuery({
    queryKey: ['arrangement-files', id],
    queryFn: () => arrangementsApi.listFiles(Number(id)),
    enabled: isEdit,
  });

  // Track linked entity IDs
  const [linkedGameIds, setLinkedGameIds] = useState<Set<number>>(new Set());
  const [linkedInstrumentIds, setLinkedInstrumentIds] = useState<Set<number>>(new Set());
  const [linkedEnsembleIds, setLinkedEnsembleIds] = useState<Set<number>>(new Set());

  // Picker modal state
  const [showGamePicker, setShowGamePicker] = useState(false);
  const [gameSearch, setGameSearch] = useState('');
  const [showInstrumentPicker, setShowInstrumentPicker] = useState(false);
  const [instrumentSearch, setInstrumentSearch] = useState('');
  const [pickerSortOrderId, setPickerSortOrderId] = useState<number | null>(null);
  const [showEnsemblePicker, setShowEnsemblePicker] = useState(false);
  const [ensembleSearch, setEnsembleSearch] = useState('');

  // Load reference data for linking
  const allGames = useQuery({ queryKey: ['games-all'], queryFn: () => gamesApi.list({ page: 1, pageSize: 100 }) });
  const allInstruments = useQuery({ queryKey: ['instruments-all'], queryFn: () => instrumentsApi.list({ page: 1, pageSize: 100, sortBy: 'name' }) });
  const sortOrders = useQuery({ queryKey: ['instrument-sort-orders'], queryFn: instrumentSortOrdersApi.list });
  // Admin/Librarian can pick from all ensembles; Submitters can only pick their own
  const allEnsembles = useQuery({ queryKey: ['ensembles-all'], queryFn: () => ensemblesApi.list({ page: 1, pageSize: 100 }), enabled: canEdit() });
  const myEnsembles = useQuery({ queryKey: ['my-ensembles'], queryFn: authApi.getMyEnsembles });
  const pickerSortOrderInstruments = useQuery({
    queryKey: ['instrument-sort-order-instruments', pickerSortOrderId],
    queryFn: () => instrumentSortOrdersApi.getInstruments(pickerSortOrderId!),
    enabled: pickerSortOrderId !== null,
  });

  // Quick-create modal state
  const [showCreateGame, setShowCreateGame] = useState(false);
  const [showCreateInstrument, setShowCreateInstrument] = useState(false);

  useEffect(() => {
    if (existing) {
      setForm({
        name: existing.name,
        description: existing.description || '',
        arrangers: existing.arrangers || [],
        composers: existing.composers || [],
        durationSeconds: existing.durationSeconds,
        year: existing.year,
      });
      setLinkedGameIds(new Set(existing.games?.map((g) => g.id) || []));
      setLinkedInstrumentIds(new Set(existing.instruments?.map((i) => i.id) || []));
      setLinkedEnsembleIds(new Set(existing.ensembles?.map((e) => e.id) || []));
    }
  }, [existing]);

  useEffect(() => {
    if (!isEdit && myEnsembles.data) {
      setLinkedEnsembleIds(new Set(myEnsembles.data.map((e) => e.id)));
    }
  }, [isEdit, myEnsembles.data]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      const flushedForm = {
        ...form,
        composers: composerInput.trim()
          ? [...(form.composers || []), composerInput.trim()]
          : form.composers,
        arrangers: arrangerInput.trim()
          ? [...(form.arrangers || []), arrangerInput.trim()]
          : form.arrangers,
      };
      let arrangementId: number;
      if (isEdit) {
        await arrangementsApi.update(Number(id), flushedForm);
        arrangementId = Number(id);
      } else {
        const created = await arrangementsApi.create(flushedForm);
        arrangementId = created.id;
      }

      // Sync relationships in edit mode
      if (isEdit && existing) {
        const oldGameIds = new Set(existing.games?.map((g) => g.id) || []);
        const oldInstrumentIds = new Set(existing.instruments?.map((i) => i.id) || []);
        const oldEnsembleIds = new Set(existing.ensembles?.map((e) => e.id) || []);

        // Games
        for (const gid of linkedGameIds) {
          if (!oldGameIds.has(gid)) await arrangementsApi.addGame(arrangementId, gid);
        }
        for (const gid of oldGameIds) {
          if (!linkedGameIds.has(gid)) await arrangementsApi.removeGame(arrangementId, gid);
        }
        // Instruments
        for (const iid of linkedInstrumentIds) {
          if (!oldInstrumentIds.has(iid)) await arrangementsApi.addInstrument(arrangementId, iid);
        }
        for (const iid of oldInstrumentIds) {
          if (!linkedInstrumentIds.has(iid)) await arrangementsApi.removeInstrument(arrangementId, iid);
        }
        // Ensembles
        for (const eid of linkedEnsembleIds) {
          if (!oldEnsembleIds.has(eid)) await arrangementsApi.addEnsemble(arrangementId, eid);
        }
        for (const eid of oldEnsembleIds) {
          if (!linkedEnsembleIds.has(eid)) await arrangementsApi.removeEnsemble(arrangementId, eid);
        }
      } else if (!isEdit) {
        // New arrangement - add all relationships; backend already auto-linked user's ensembles so ignore 400s
        for (const gid of linkedGameIds) await arrangementsApi.addGame(arrangementId, gid);
        for (const iid of linkedInstrumentIds) await arrangementsApi.addInstrument(arrangementId, iid);
        for (const eid of linkedEnsembleIds) await arrangementsApi.addEnsemble(arrangementId, eid).catch(() => {});
      }

      return arrangementId;
    },
    onSuccess: (arrangementId) => {
      setComposerInput('');
      setArrangerInput('');
      queryClient.invalidateQueries({ queryKey: ['arrangements'] });
      queryClient.invalidateQueries({ queryKey: ['arrangement', id] });
      navigate(isEdit ? `/arrangements/${arrangementId}` : `/arrangements/${arrangementId}/edit`);
    },
    onError: () => setError('Failed to save arrangement'),
  });

  if (isEdit && loadingExisting) return <Spinner animation="border" />;

  return (
    <>
      <h2>{isEdit ? 'Edit' : 'New'} Arrangement</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }}>
        <Row className="g-4">
          <Col md={8}>
            <Card className="mb-3">
              <Card.Body>
                <Form.Group className="mb-3">
                  <Form.Label>Name *</Form.Label>
                  <Form.Control
                    value={form.name}
                    onChange={(e) => setForm({ ...form, name: e.target.value })}
                    required
                  />
                </Form.Group>
                <Form.Group className="mb-3">
                  <Form.Label>Description</Form.Label>
                  <Form.Control
                    as="textarea"
                    rows={3}
                    value={form.description || ''}
                    onChange={(e) => setForm({ ...form, description: e.target.value || undefined })}
                  />
                </Form.Group>
                <Row>
                  <Col md={6}>
                    <Form.Group className="mb-3">
                      <Form.Label>Composers</Form.Label>
                      <div className="d-flex flex-wrap gap-1 mb-1">
                        {form.composers?.map((c, i) => (
                          <Badge key={i} bg="secondary" className="d-flex align-items-center gap-1">
                            {c}
                            <span
                              role="button"
                              style={{ cursor: 'pointer', fontSize: '1.1em', lineHeight: 1 }}
                              onClick={() => setForm({ ...form, composers: form.composers?.filter((_, j) => j !== i) })}
                            >
                              &times;
                            </span>
                          </Badge>
                        ))}
                      </div>
                      <Form.Control
                        value={composerInput}
                        onChange={(e) => setComposerInput(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' && composerInput.trim()) {
                            e.preventDefault();
                            setForm({ ...form, composers: [...(form.composers || []), composerInput.trim()] });
                            setComposerInput('');
                          }
                        }}
                        placeholder="Type a name and press Enter"
                      />
                    </Form.Group>
                  </Col>
                  <Col md={6}>
                    <Form.Group className="mb-3">
                      <Form.Label>Arrangers</Form.Label>
                      <div className="d-flex flex-wrap gap-1 mb-1">
                        {form.arrangers?.map((a, i) => (
                          <Badge key={i} bg="secondary" className="d-flex align-items-center gap-1">
                            {a}
                            <span
                              role="button"
                              style={{ cursor: 'pointer', fontSize: '1.1em', lineHeight: 1 }}
                              onClick={() => setForm({ ...form, arrangers: form.arrangers?.filter((_, j) => j !== i) })}
                            >
                              &times;
                            </span>
                          </Badge>
                        ))}
                      </div>
                      <Form.Control
                        value={arrangerInput}
                        onChange={(e) => setArrangerInput(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' && arrangerInput.trim()) {
                            e.preventDefault();
                            setForm({ ...form, arrangers: [...(form.arrangers || []), arrangerInput.trim()] });
                            setArrangerInput('');
                          }
                        }}
                        placeholder="Type a name and press Enter"
                      />
                    </Form.Group>
                  </Col>
                </Row>
                <Row>
                  <Col md={6}>
                    <Form.Group className="mb-3">
                      <Form.Label>Duration (seconds)</Form.Label>
                      <Form.Control
                        type="number"
                        value={form.durationSeconds ?? ''}
                        onChange={(e) => setForm({ ...form, durationSeconds: e.target.value ? Number(e.target.value) : undefined })}
                      />
                    </Form.Group>
                  </Col>
                  <Col md={6}>
                    <Form.Group className="mb-3">
                      <Form.Label>Year</Form.Label>
                      <Form.Control
                        type="number"
                        value={form.year ?? ''}
                        onChange={(e) => setForm({ ...form, year: e.target.value ? Number(e.target.value) : undefined })}
                      />
                    </Form.Group>
                  </Col>
                </Row>
              </Card.Body>
            </Card>
          </Col>

          <Col md={4}>
            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Games</Card.Title>
                {linkedGameIds.size > 0 ? (
                  <ListGroup variant="flush" className="mb-2">
                    {allGames.data?.items
                      .filter((g) => linkedGameIds.has(g.id))
                      .map((g) => (
                        <ListGroup.Item key={g.id} className="d-flex justify-content-between align-items-center px-0">
                          {g.name}
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => {
                              const next = new Set(linkedGameIds);
                              next.delete(g.id);
                              setLinkedGameIds(next);
                            }}
                          >
                            Remove
                          </Button>
                        </ListGroup.Item>
                      ))}
                  </ListGroup>
                ) : (
                  <p className="text-muted mb-2">No games selected</p>
                )}
                <Button variant="outline-primary" size="sm" onClick={() => { setGameSearch(''); setShowGamePicker(true); }}>
                  Add
                </Button>
              </Card.Body>
            </Card>

            <Modal show={showGamePicker} onHide={() => setShowGamePicker(false)}>
              <Modal.Header closeButton>
                <Modal.Title>Add Games</Modal.Title>
              </Modal.Header>
              <Modal.Body>
                <Form.Control
                  placeholder="Search games..."
                  value={gameSearch}
                  onChange={(e) => setGameSearch(e.target.value)}
                  className="mb-3"
                  autoFocus
                />
                <ListGroup style={{ maxHeight: '300px', overflowY: 'auto' }}>
                  {allGames.data?.items
                    .filter((g) => !linkedGameIds.has(g.id) && g.name.toLowerCase().includes(gameSearch.toLowerCase()))
                    .map((g) => (
                      <ListGroup.Item
                        key={g.id}
                        action
                        onClick={() => {
                          const next = new Set(linkedGameIds);
                          next.add(g.id);
                          setLinkedGameIds(next);
                        }}
                      >
                        {g.name}
                      </ListGroup.Item>
                    ))}
                </ListGroup>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="outline-success" onClick={() => setShowCreateGame(true)}>
                  Create New
                </Button>
                <Button variant="secondary" onClick={() => setShowGamePicker(false)}>
                  Done
                </Button>
              </Modal.Footer>
            </Modal>

            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Instruments</Card.Title>
                {linkedInstrumentIds.size > 0 ? (
                  <ListGroup variant="flush" className="mb-2" style={{ maxHeight: '200px', overflowY: 'auto' }}>
                    {allInstruments.data?.items
                      .filter((i) => linkedInstrumentIds.has(i.id))
                      .map((i) => (
                        <ListGroup.Item key={i.id} className="d-flex justify-content-between align-items-center px-0">
                          {i.name}
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => {
                              const next = new Set(linkedInstrumentIds);
                              next.delete(i.id);
                              setLinkedInstrumentIds(next);
                            }}
                          >
                            Remove
                          </Button>
                        </ListGroup.Item>
                      ))}
                  </ListGroup>
                ) : (
                  <p className="text-muted mb-2">No instruments selected</p>
                )}
                <Button variant="outline-primary" size="sm" onClick={() => {
                  const defaultSo = sortOrders.data?.find((so) => so.isDefault);
                  setPickerSortOrderId(defaultSo?.id ?? null);
                  setInstrumentSearch('');
                  setShowInstrumentPicker(true);
                }}>
                  Add
                </Button>
              </Card.Body>
            </Card>

            <Modal show={showInstrumentPicker} onHide={() => setShowInstrumentPicker(false)}>
              <Modal.Header closeButton>
                <Modal.Title>Add Instruments</Modal.Title>
              </Modal.Header>
              <Modal.Body>
                {sortOrders.data && sortOrders.data.length > 0 && (
                  <Form.Select
                    size="sm"
                    value={pickerSortOrderId ?? ''}
                    onChange={(e) => setPickerSortOrderId(e.target.value ? Number(e.target.value) : null)}
                    className="mb-2"
                  >
                    <option value="">Alphabetical</option>
                    {sortOrders.data.map((so) => (
                      <option key={so.id} value={so.id}>{so.name}{so.isDefault ? ' ★' : ''}</option>
                    ))}
                  </Form.Select>
                )}
                <Form.Control
                  placeholder="Search instruments..."
                  value={instrumentSearch}
                  onChange={(e) => setInstrumentSearch(e.target.value)}
                  className="mb-3"
                  autoFocus
                />
                <ListGroup style={{ maxHeight: '300px', overflowY: 'auto' }}>
                  {(pickerSortOrderId !== null ? (pickerSortOrderInstruments.data ?? []) : (allInstruments.data?.items ?? []))
                    .filter((i) => !linkedInstrumentIds.has(i.id) && i.name.toLowerCase().includes(instrumentSearch.toLowerCase()))
                    .map((i) => (
                      <ListGroup.Item
                        key={i.id}
                        action
                        onClick={() => {
                          const next = new Set(linkedInstrumentIds);
                          next.add(i.id);
                          setLinkedInstrumentIds(next);
                        }}
                      >
                        {i.name}
                      </ListGroup.Item>
                    ))}
                </ListGroup>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="outline-success" onClick={() => setShowCreateInstrument(true)}>
                  Create New
                </Button>
                <Button variant="secondary" onClick={() => setShowInstrumentPicker(false)}>
                  Done
                </Button>
              </Modal.Footer>
            </Modal>

            <Card className="mb-3">
              <Card.Body>
                <Card.Title>Ensembles</Card.Title>
                {linkedEnsembleIds.size > 0 ? (
                  <ListGroup variant="flush" className="mb-2">
                    {(canEdit() ? (allEnsembles.data?.items ?? []) : (myEnsembles.data ?? []))
                      .filter((e) => linkedEnsembleIds.has(e.id))
                      .map((e) => (
                        <ListGroup.Item key={e.id} className="d-flex justify-content-between align-items-center px-0">
                          {e.name}
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => {
                              const next = new Set(linkedEnsembleIds);
                              next.delete(e.id);
                              setLinkedEnsembleIds(next);
                            }}
                          >
                            Remove
                          </Button>
                        </ListGroup.Item>
                      ))}
                  </ListGroup>
                ) : (
                  <p className="text-muted mb-2">No ensembles selected</p>
                )}
                <Button variant="outline-primary" size="sm" onClick={() => { setEnsembleSearch(''); setShowEnsemblePicker(true); }}>
                  Add
                </Button>
              </Card.Body>
            </Card>

            <Modal show={showEnsemblePicker} onHide={() => setShowEnsemblePicker(false)}>
              <Modal.Header closeButton>
                <Modal.Title>Add Ensembles</Modal.Title>
              </Modal.Header>
              <Modal.Body>
                <Form.Control
                  placeholder="Search ensembles..."
                  value={ensembleSearch}
                  onChange={(e) => setEnsembleSearch(e.target.value)}
                  className="mb-3"
                  autoFocus
                />
                <ListGroup style={{ maxHeight: '300px', overflowY: 'auto' }}>
                  {(canEdit() ? (allEnsembles.data?.items ?? []) : (myEnsembles.data ?? []))
                    .filter((e) => !linkedEnsembleIds.has(e.id) && e.name.toLowerCase().includes(ensembleSearch.toLowerCase()))
                    .map((e) => (
                      <ListGroup.Item
                        key={e.id}
                        action
                        onClick={() => {
                          const next = new Set(linkedEnsembleIds);
                          next.add(e.id);
                          setLinkedEnsembleIds(next);
                        }}
                      >
                        {e.name}
                      </ListGroup.Item>
                    ))}
                </ListGroup>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onClick={() => setShowEnsemblePicker(false)}>
                  Done
                </Button>
              </Modal.Footer>
            </Modal>

          </Col>
        </Row>

        <div className="mt-3">
          <Button type="submit" disabled={saveMutation.isPending}>
            {saveMutation.isPending ? <Spinner size="sm" animation="border" /> : (isEdit ? 'Save Changes' : 'Create Arrangement')}
          </Button>
          <Button variant="secondary" className="ms-2" onClick={() => navigate(isEdit ? `/arrangements/${id}` : '/arrangements')}>Cancel</Button>
        </div>
      </Form>

      <QuickCreateGameModal
        show={showCreateGame}
        onHide={() => setShowCreateGame(false)}
        onCreated={(game) => {
          const next = new Set(linkedGameIds);
          next.add(game.id);
          setLinkedGameIds(next);
        }}
      />
      <QuickCreateInstrumentModal
        show={showCreateInstrument}
        onHide={() => setShowCreateInstrument(false)}
        onCreated={(instrument) => {
          const next = new Set(linkedInstrumentIds);
          next.add(instrument.id);
          setLinkedInstrumentIds(next);
        }}
      />
      {isEdit && files && existing && (() => {
        const categorized = categorizeFiles(files);
        return (
          <div className="mt-4">
            <h4>Files</h4>
            <FileSection title="Notation Files" files={categorized.notationFiles} arrangementId={Number(id)} editable accept={NOTATION_ACCEPT} />
            <RenderedScoreGrid arrangement={existing} files={files} editable canDownload />
            <FileSection title="Playback Files" files={categorized.playbackFiles} arrangementId={Number(id)} editable accept={PLAYBACK_ACCEPT} />
          </div>
        );
      })()}
    </>
  );
}
