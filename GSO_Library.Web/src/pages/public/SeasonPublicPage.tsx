import { Fragment, useEffect, useState } from 'react';
import { Alert, Badge, Button, Card, Col, Container, Form, ListGroup, Row, Spinner, Table } from 'react-bootstrap';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { publicApi } from '../../api/public';
import type { DownloadSection } from '../../types/public';

function formatDateRange(startDate?: string, endDate?: string) {
  if (!startDate && !endDate) return null;
  const fmt = (d: string) => new Date(d).toLocaleDateString();
  if (startDate && endDate) return `${fmt(startDate)} – ${fmt(endDate)}`;
  if (startDate) return `From ${fmt(startDate)}`;
  return `Until ${fmt(endDate!)}`;
}

type RenderItem =
  | { type: 'section'; section: DownloadSection }
  | { type: 'family'; familyName: string; familyId: number | null; sections: DownloadSection[] };

function buildRenderItems(sections: DownloadSection[]): RenderItem[] {
  const items: RenderItem[] = [];
  const familyMap = new Map<string, RenderItem & { type: 'family' }>();
  for (const section of sections) {
    if (section.familyName) {
      const existing = familyMap.get(section.familyName);
      if (existing) {
        existing.sections.push(section);
      } else {
        const item: RenderItem & { type: 'family' } = { type: 'family', familyName: section.familyName, familyId: section.familyId ?? null, sections: [section] };
        familyMap.set(section.familyName, item);
        items.push(item);
      }
    } else {
      items.push({ type: 'section', section });
    }
  }
  return items;
}

export default function SeasonPublicPage() {
  const { token } = useParams<{ token: string }>();
  const [enteredPassword, setEnteredPassword] = useState('');
  const [submittedPassword, setSubmittedPassword] = useState<string | undefined>(undefined);
  const [downloadError, setDownloadError] = useState('');
  const [preparingKey, setPreparingKey] = useState<string | null>(null);
  const [selectedArrangementId, setSelectedArrangementId] = useState<number | null>(null);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['public-season', token, submittedPassword],
    queryFn: () => publicApi.getSeason(token!, submittedPassword),
    enabled: !!token,
    retry: false,
    // Each fetch counts as a page access server-side, so refetch only when the visitor
    // actually navigates here again (new mount), not on window refocus/reconnect/interval.
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  // Drop the selection if the arrangement is no longer part of the season (e.g. after a refetch).
  useEffect(() => {
    if (data && selectedArrangementId != null && !data.arrangements.some((a) => a.id === selectedArrangementId)) {
      setSelectedArrangementId(null);
    }
  }, [data, selectedArrangementId]);

  const keyFor = (section: DownloadSection | null, arrId: number | null) =>
    `${arrId ?? 'season'}::${section?.label ?? '__all__'}`;

  const handleDownload = async (section: DownloadSection | null, arrangementId: number | null) => {
    const key = keyFor(section, arrangementId);
    setPreparingKey(key);
    setDownloadError('');
    try {
      await publicApi.prepareDownload(token!, section, submittedPassword, arrangementId);
      await publicApi.downloadZip(token!, section, submittedPassword, arrangementId);
    } catch {
      setDownloadError('Download failed. Please try again.');
    } finally {
      setPreparingKey(null);
    }
  };

  if (isLoading) {
    return (
      <Container className="d-flex justify-content-center align-items-center" style={{ minHeight: '100vh' }}>
        <Spinner animation="border" />
      </Container>
    );
  }

  if (isError || !data) {
    return (
      <Container className="d-flex justify-content-center align-items-center" style={{ minHeight: '100vh' }}>
        <Alert variant="warning" className="text-center">
          <Alert.Heading>Link not found</Alert.Heading>
          <p className="mb-0">This link has expired or is invalid.</p>
        </Alert>
      </Container>
    );
  }

  if (data.requiresPassword) {
    return (
      <Container className="d-flex justify-content-center align-items-center" style={{ minHeight: '100vh' }}>
        <Card style={{ width: '100%', maxWidth: '400px' }}>
          <Card.Body>
            <Card.Title className="mb-3">{data.name}</Card.Title>
            <p className="text-muted">This page is password protected.</p>
            <Form onSubmit={(e) => { e.preventDefault(); setSubmittedPassword(enteredPassword); }}>
              <Form.Group className="mb-3">
                <Form.Control
                  type="password"
                  placeholder="Enter password"
                  value={enteredPassword}
                  onChange={(e) => setEnteredPassword(e.target.value)}
                  autoFocus
                />
              </Form.Group>
              <Button type="submit" variant="primary" className="w-100">View</Button>
            </Form>
            {submittedPassword !== undefined && (
              <Alert variant="danger" className="mt-3 mb-0">Incorrect password.</Alert>
            )}
          </Card.Body>
        </Card>
      </Container>
    );
  }

  const dateRange = formatDateRange(data.startDate, data.endDate);
  const sel = selectedArrangementId;
  const selectedArrangement = sel == null ? undefined : data.arrangements.find((a) => a.id === sel);
  const effectiveCount = (s: DownloadSection) =>
    sel == null ? s.fileCount : (s.arrangementFileCounts?.[sel] ?? 0);
  const effectiveLastUpdated = (s: DownloadSection) =>
    sel == null ? s.lastUpdated : s.arrangementLastUpdated?.[sel];
  const visibleSections = sel == null
    ? data.downloadSections
    : data.downloadSections.filter((s) => effectiveCount(s) > 0);
  const renderItems = buildRenderItems(visibleSections);
  const arrangementTotal = sel == null
    ? 0
    : data.downloadSections.reduce((sum, s) => sum + effectiveCount(s), 0);

  const renderSectionRow = (s: DownloadSection, key: string, indent: boolean) => (
    <tr key={key}>
      <td className={indent ? 'ps-3' : undefined}>{s.label}</td>
      <td><Badge bg="secondary">{effectiveCount(s)}</Badge></td>
      <td className="text-muted small">
        {(() => {
          const d = effectiveLastUpdated(s);
          return d ? new Date(d).toLocaleDateString() : '—';
        })()}
      </td>
      <td>
        <Button size="sm" variant="outline-primary" onClick={() => handleDownload(s, sel)} disabled={preparingKey !== null}>
          {preparingKey === keyFor(s, sel)
            ? <Spinner size="sm" animation="border" />
            : 'Download'}
        </Button>
      </td>
    </tr>
  );

  return (
    <Container className="py-4">
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h2">{data.name}</h1>
          {data.ensembleName && <p className="text-muted mb-1">{data.ensembleName}</p>}
          {dateRange && <p className="text-muted mb-0">{dateRange}</p>}
        </div>
        <Button variant="primary" onClick={() => handleDownload(null, null)} disabled={preparingKey !== null}>
          {preparingKey === keyFor(null, null)
            ? <><Spinner size="sm" animation="border" className="me-2" />Preparing…</>
            : 'Download All Files'}
        </Button>
      </div>

      {downloadError && (
        <Alert variant="danger" dismissible onClose={() => setDownloadError('')}>
          {downloadError}
        </Alert>
      )}

      <Row className="g-4">
        <Col md={5}>
          <Card>
            <Card.Body>
              <Card.Title>Arrangements</Card.Title>
              {data.arrangements.length > 0 ? (
                <ListGroup variant="flush">
                  {data.arrangements.map((a) => (
                    <ListGroup.Item
                      key={a.id}
                      action
                      active={sel === a.id}
                      onClick={() => setSelectedArrangementId((prev) => (prev === a.id ? null : a.id))}
                      className="px-3"
                    >
                      <div className="fw-semibold">{a.name}</div>
                      {a.games.length > 0 && (
                        <div className={sel === a.id ? 'small' : 'text-muted small'}>
                          {a.games.slice(0, 3).join(', ')}{a.games.length > 3 ? ', …' : ''}
                        </div>
                      )}
                      <div className={sel === a.id ? 'small' : 'text-muted small'}>
                        {[
                          a.composers.length > 0 && `Composed by ${a.composers.join(', ')}`,
                          a.arrangers.length > 0 && `Arranged by ${a.arrangers.join(', ')}`,
                        ].filter(Boolean).join(' · ')}
                      </div>
                    </ListGroup.Item>
                  ))}
                </ListGroup>
              ) : (
                <p className="text-muted mb-0">No arrangements in this season.</p>
              )}
            </Card.Body>
          </Card>
        </Col>

        <Col md={7}>
          {data.downloadSections.length === 0 ? (
            <Alert variant="info">No downloadable files are available for this season.</Alert>
          ) : (
            <Card>
              <Card.Body>
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <Card.Title className="mb-0">Parts</Card.Title>
                  {sel != null && (
                    <div className="small">
                      <span className="text-muted me-2">{selectedArrangement?.name}</span>
                      <Button variant="link" size="sm" className="p-0" onClick={() => setSelectedArrangementId(null)}>
                        Show all parts
                      </Button>
                    </div>
                  )}
                </div>

                {sel != null && (
                  <Button
                    variant="primary"
                    size="sm"
                    className="mb-3"
                    disabled={preparingKey !== null || arrangementTotal === 0}
                    onClick={() => handleDownload(null, sel)}
                  >
                    {preparingKey === keyFor(null, sel)
                      ? <><Spinner size="sm" animation="border" className="me-2" />Preparing…</>
                      : `Download all parts for this arrangement (${arrangementTotal})`}
                  </Button>
                )}

                {sel != null && arrangementTotal === 0 ? (
                  <Alert variant="info" className="mb-0">No downloadable files for this arrangement.</Alert>
                ) : (
                  <Table size="sm" className="mb-0">
                    <thead>
                      <tr>
                        <th>Part</th>
                        <th>Files</th>
                        <th>Last Updated</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {renderItems.map((item, i) => {
                        if (item.type === 'section') {
                          return renderSectionRow(item.section, String(i), false);
                        }

                        // Family group. A family download filters by familyId server-side, so only
                        // offer the group-level button when we actually have one — a null familyId
                        // (a generic Percussion/Voice section with no instrument in that family)
                        // would otherwise fall through to downloading the entire season. Those
                        // groups hold a single generic row, which keeps its own Download button.
                        const familySection: DownloadSection | null = item.familyId != null
                          ? { label: item.familyName, familyId: item.familyId, fileCount: 0 }
                          : null;
                        return (
                          <Fragment key={i}>
                            <tr style={{ background: '#dee2e6' }}>
                              <td colSpan={familySection ? 3 : 4} className="fw-semibold py-2" style={{ fontSize: '1rem', color: '#212529' }}>
                                {item.familyName}
                              </td>
                              {familySection && (
                                <td className="py-2">
                                  <Button
                                    size="sm"
                                    variant="outline-secondary"
                                    onClick={() => handleDownload(familySection, sel)}
                                    disabled={preparingKey !== null}
                                  >
                                    {preparingKey === keyFor(familySection, sel)
                                      ? <Spinner size="sm" animation="border" />
                                      : 'Download'}
                                  </Button>
                                </td>
                              )}
                            </tr>
                            {item.sections.map((s, j) => renderSectionRow(s, `${i}-${j}`, true))}
                          </Fragment>
                        );
                      })}
                    </tbody>
                  </Table>
                )}
              </Card.Body>
            </Card>
          )}
        </Col>
      </Row>
    </Container>
  );
}
