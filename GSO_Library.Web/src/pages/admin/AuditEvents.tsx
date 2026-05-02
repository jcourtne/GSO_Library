import { useState } from 'react';
import { Alert, Button, Col, Form, Row, Spinner, Table } from 'react-bootstrap';
import { useQuery } from '@tanstack/react-query';
import { auditEventsApi } from '../../api/auditEvents';
import { AUDIT_EVENT_TYPES } from '../../types';
import FilterPanelSection from '../../components/common/FilterPanel';

function toDatetimeLocalValue(date: Date): string {
  return date.toISOString().slice(0, 16);
}

const DEFAULT_FROM = () => toDatetimeLocalValue(new Date(Date.now() - 7 * 24 * 60 * 60 * 1000));
const DEFAULT_TO = () => toDatetimeLocalValue(new Date());

const EVENT_TYPE_OPTIONS = AUDIT_EVENT_TYPES.map((t) => ({ value: t, label: t }));

export default function AuditEvents() {
  const [eventTypes, setEventTypes] = useState<string[]>([]);
  const [usernames, setUsernames] = useState<string[]>([]);
  const [from, setFrom] = useState(DEFAULT_FROM);
  const [to, setTo] = useState(DEFAULT_TO);

  const { data, isLoading, error } = useQuery({
    queryKey: ['audit-events', { eventTypes, usernames, from, to }],
    queryFn: () =>
      auditEventsApi.list({
        eventTypes: eventTypes.length ? eventTypes : undefined,
        usernames: usernames.length ? usernames : undefined,
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to).toISOString() : undefined,
      }),
  });

  const { data: usernameOptions } = useQuery({
    queryKey: ['audit-event-usernames'],
    queryFn: auditEventsApi.usernames,
  });

  const hasFilters = eventTypes.length > 0 || usernames.length > 0 || from !== DEFAULT_FROM() || to !== DEFAULT_TO();

  const clearFilters = () => {
    setEventTypes([]);
    setUsernames([]);
    setFrom(DEFAULT_FROM());
    setTo(DEFAULT_TO());
  };

  return (
    <>
      <h2 className="mb-3">Audit Log</h2>

      <Row className="g-3">
        <Col md={3} style={{ borderRight: '1px solid var(--bs-border-color)' }}>
          <div className="pe-2">
            <FilterPanelSection
              label="Username"
              options={(usernameOptions ?? []).map((u) => ({ value: u, label: u }))}
              selected={usernames}
              onChange={(v) => setUsernames(v as string[])}
            />

            <FilterPanelSection
              label="Event Type"
              options={EVENT_TYPE_OPTIONS}
              selected={eventTypes}
              onChange={(v) => setEventTypes(v as string[])}
            />

            {hasFilters && (
              <Button variant="outline-secondary" size="sm" className="w-100 mt-1" onClick={clearFilters}>
                Clear all filters
              </Button>
            )}
          </div>
        </Col>

        <Col md={9}>
          <div className="d-flex justify-content-end align-items-end gap-2 mb-3">
            <Form.Group>
              <Form.Label className="small text-muted mb-1">From</Form.Label>
              <Form.Control
                size="sm"
                type="datetime-local"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
              />
            </Form.Group>
            <Form.Group>
              <Form.Label className="small text-muted mb-1">To</Form.Label>
              <Form.Control
                size="sm"
                type="datetime-local"
                value={to}
                onChange={(e) => setTo(e.target.value)}
              />
            </Form.Group>
          </div>

          {isLoading && <Spinner animation="border" />}
          {error && <Alert variant="danger">Failed to load audit events.</Alert>}
          {data && (
            <Table striped hover responsive>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Event Type</th>
                  <th>Username</th>
                  <th>Target Username</th>
                  <th>Detail</th>
                  <th>Time</th>
                </tr>
              </thead>
              <tbody>
                {data.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="text-center text-muted">No events found.</td>
                  </tr>
                ) : (
                  data.map((e) => (
                    <tr key={e.id}>
                      <td>{e.id}</td>
                      <td>{e.eventType}</td>
                      <td>{e.username ?? '-'}</td>
                      <td>{e.targetUsername ?? '-'}</td>
                      <td>{e.detail ?? '-'}</td>
                      <td style={{ whiteSpace: 'nowrap' }}>{new Date(e.createdAt).toLocaleString()}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </Table>
          )}
        </Col>
      </Row>
    </>
  );
}
