import { useState } from 'react';
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

export default function SeasonPublicPage() {
  const { token } = useParams<{ token: string }>();
  const [enteredPassword, setEnteredPassword] = useState('');
  const [submittedPassword, setSubmittedPassword] = useState<string | undefined>(undefined);
  const [downloadError, setDownloadError] = useState('');

  const { data, isLoading, isError } = useQuery({
    queryKey: ['public-season', token, submittedPassword],
    queryFn: () => publicApi.getSeason(token!, submittedPassword),
    enabled: !!token,
    retry: false,
  });

  const handleDownload = async (section: DownloadSection | null) => {
    setDownloadError('');
    try {
      await publicApi.downloadZip(token!, section, submittedPassword);
    } catch {
      setDownloadError('Download failed. Please try again.');
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

  return (
    <Container className="py-4">
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <h1 className="h2">{data.name}</h1>
          {data.ensembleName && <p className="text-muted mb-1">{data.ensembleName}</p>}
          {dateRange && <p className="text-muted mb-0">{dateRange}</p>}
        </div>
        <Button variant="primary" onClick={() => handleDownload(null)}>
          Download All Files
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
                    {data.downloadSections.map((section, i) => (
                      <tr key={i}>
                        <td>{section.label}</td>
                        <td>
                          <Badge bg="secondary">{section.fileCount}</Badge>
                        </td>
                        <td className="text-muted small">
                          {section.lastUpdated
                            ? new Date(section.lastUpdated).toLocaleDateString()
                            : '—'}
                        </td>
                        <td>
                          <Button
                            size="sm"
                            variant="outline-primary"
                            onClick={() => handleDownload(section)}
                          >
                            Download
                          </Button>
                        </td>
                      </tr>
                    ))}
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
