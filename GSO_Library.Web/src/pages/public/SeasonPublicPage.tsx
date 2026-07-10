import { Fragment, useState } from 'react';
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
  for (const section of sections) {
    if (section.familyName) {
      const last = items[items.length - 1];
      if (last?.type === 'family' && last.familyName === section.familyName) {
        last.sections.push(section);
      } else {
        items.push({ type: 'family', familyName: section.familyName, familyId: section.familyId ?? null, sections: [section] });
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

  const { data, isLoading, isError } = useQuery({
    queryKey: ['public-season', token, submittedPassword],
    queryFn: () => publicApi.getSeason(token!, submittedPassword),
    enabled: !!token,
    retry: false,
  });

  const sectionKey = (section: DownloadSection | null) => section?.label ?? '__all__';

  const handleDownload = async (section: DownloadSection | null) => {
    const key = sectionKey(section);
    setPreparingKey(key);
    setDownloadError('');
    try {
      await publicApi.prepareDownload(token!, section, submittedPassword);
      await publicApi.downloadZip(token!, section, submittedPassword);
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
  const renderItems = buildRenderItems(data.downloadSections);

  return (
    <Container className="py-4">
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h2">{data.name}</h1>
          {data.ensembleName && <p className="text-muted mb-1">{data.ensembleName}</p>}
          {dateRange && <p className="text-muted mb-0">{dateRange}</p>}
        </div>
        <Button variant="primary" onClick={() => handleDownload(null)} disabled={preparingKey !== null}>
          {preparingKey === '__all__'
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
                  {data.arrangements.map((a, i) => (
                    <ListGroup.Item key={i} className="px-0">
                      <div className="fw-semibold">{a.name}</div>
                      <div className="text-muted small">
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
          {data.downloadSections.length > 0 ? (
            <Card>
              <Card.Body>
                <Card.Title>Parts</Card.Title>
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
                        const s = item.section;
                        return (
                          <tr key={i}>
                            <td>{s.label}</td>
                            <td><Badge bg="secondary">{s.fileCount}</Badge></td>
                            <td className="text-muted small">
                              {s.lastUpdated ? new Date(s.lastUpdated).toLocaleDateString() : '—'}
                            </td>
                            <td>
                              <Button size="sm" variant="outline-primary" onClick={() => handleDownload(s)} disabled={preparingKey !== null}>
                                {preparingKey === sectionKey(s)
                                  ? <Spinner size="sm" animation="border" />
                                  : 'Download'}
                              </Button>
                            </td>
                          </tr>
                        );
                      }

                      // Family group
                      return (
                        <Fragment key={i}>
                          <tr style={{ background: '#dee2e6' }}>
                            <td colSpan={3} className="fw-semibold py-2" style={{ fontSize: '1rem', color: '#212529' }}>
                              {item.familyName}
                            </td>
                            <td className="py-2">
                              <Button
                                size="sm"
                                variant="outline-secondary"
                                onClick={() => handleDownload({ label: item.familyName, familyId: item.familyId, fileCount: 0 })}
                                disabled={preparingKey !== null}
                              >
                                {preparingKey === item.familyName
                                  ? <Spinner size="sm" animation="border" />
                                  : 'Download'}
                              </Button>
                            </td>
                          </tr>
                          {item.sections.map((s, j) => (
                            <tr key={`${i}-${j}`}>
                              <td className="ps-3">{s.label}</td>
                              <td><Badge bg="secondary">{s.fileCount}</Badge></td>
                              <td className="text-muted small">
                                {s.lastUpdated ? new Date(s.lastUpdated).toLocaleDateString() : '—'}
                              </td>
                              <td>
                                <Button size="sm" variant="outline-primary" onClick={() => handleDownload(s)} disabled={preparingKey !== null}>
                                  {preparingKey === sectionKey(s)
                                    ? <Spinner size="sm" animation="border" />
                                    : 'Download'}
                                </Button>
                              </td>
                            </tr>
                          ))}
                        </Fragment>
                      );
                    })}
                  </tbody>
                </Table>
              </Card.Body>
            </Card>
          ) : (
            <Alert variant="info">No downloadable files are available for this season.</Alert>
          )}
        </Col>
      </Row>
    </Container>
  );
}
