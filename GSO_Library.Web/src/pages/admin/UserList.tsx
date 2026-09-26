import { useMemo, useState } from 'react';
import { Alert, Badge, Button, Col, Form, Row, Table, Spinner } from 'react-bootstrap';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { authApi } from '../../api/auth';
import { ensemblesApi } from '../../api/ensembles';
import ConfirmModal from '../../components/common/ConfirmModal';
import FilterPanelSection from '../../components/common/FilterPanel';
import type { UserResponse } from '../../types';

type SortKey = 'userName' | 'email' | 'name' | 'lastLogin';
type SortDir = 'asc' | 'desc';

const ALL_ROLES = ['Admin', 'Librarian', 'Submitter', 'Downloader', 'User', 'Ensemble Librarian', 'Ensemble Downloader'];

function roleBadgeBg(r: string) {
  if (r === 'Admin') return 'danger';
  if (r === 'Librarian' || r === 'Ensemble Librarian') return 'warning';
  if (r === 'Submitter') return 'info';
  if (r === 'Downloader' || r === 'Ensemble Downloader') return 'primary';
  return 'secondary';
}

function sortUsers(users: UserResponse[], key: SortKey, dir: SortDir) {
  return [...users].sort((a, b) => {
    if (key === 'lastLogin') {
      const at = a.lastLoginAt ? new Date(a.lastLoginAt).getTime() : 0;
      const bt = b.lastLoginAt ? new Date(b.lastLoginAt).getTime() : 0;
      return dir === 'asc' ? at - bt : bt - at;
    }
    let av = '';
    let bv = '';
    if (key === 'userName') { av = a.userName ?? ''; bv = b.userName ?? ''; }
    else if (key === 'email') { av = a.email ?? ''; bv = b.email ?? ''; }
    else if (key === 'name') {
      av = [a.firstName, a.lastName].filter(Boolean).join(' ');
      bv = [b.firstName, b.lastName].filter(Boolean).join(' ');
    }
    const cmp = av.localeCompare(bv);
    return dir === 'asc' ? cmp : -cmp;
  });
}

function SortTh({ label, sortKey, current, dir, onSort }: {
  label: string;
  sortKey: SortKey;
  current: SortKey;
  dir: SortDir;
  onSort: (key: SortKey) => void;
}) {
  const active = current === sortKey;
  return (
    <th style={{ cursor: 'pointer', userSelect: 'none', whiteSpace: 'nowrap' }} onClick={() => onSort(sortKey)}>
      {label}{' '}
      <span className="text-muted" style={{ fontSize: '0.75em' }}>
        {active ? (dir === 'asc' ? '▲' : '▼') : '▲▼'}
      </span>
    </th>
  );
}

export default function UserList() {
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [toggleTarget, setToggleTarget] = useState<UserResponse | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>('userName');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [roleFilter, setRoleFilter] = useState<string[]>([]);
  const [ensembleFilter, setEnsembleFilter] = useState<number[]>([]);

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => authApi.getUsers(),
  });

  const ensembles = useQuery({
    queryKey: ['ensembles-all'],
    queryFn: () => ensemblesApi.getAll(),
  });

  const toggleMutation = useMutation({
    mutationFn: (user: UserResponse) =>
      user.isDisabled ? authApi.enableUser(user.id) : authApi.disableUser(user.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setToggleTarget(null);
    },
    onError: () => setError('Failed to update user status'),
  });

  function handleSort(key: SortKey) {
    if (key === sortKey) setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortKey(key); setSortDir('asc'); }
  }

  const { filteredActive, filteredDisabled } = useMemo(() => {
    const lower = search.toLowerCase();
    const matches = (u: UserResponse) =>
      (!search || u.userName?.toLowerCase().includes(lower)) &&
      (roleFilter.length === 0 || u.roles.some((r) => roleFilter.includes(r))) &&
      (ensembleFilter.length === 0 || u.ensembles.some((e) => ensembleFilter.includes(e.id)));
    const active = users?.filter((u) => !u.isDisabled && matches(u)) ?? [];
    const disabled = users?.filter((u) => u.isDisabled && matches(u)) ?? [];
    return {
      filteredActive: sortUsers(active, sortKey, sortDir),
      filteredDisabled: sortUsers(disabled, sortKey, sortDir),
    };
  }, [users, search, roleFilter, ensembleFilter, sortKey, sortDir]);

  const sortThProps = { current: sortKey, dir: sortDir, onSort: handleSort };
  const hasFilters = search || roleFilter.length > 0 || ensembleFilter.length > 0;
  const clearAllFilters = () => {
    setSearch('');
    setRoleFilter([]);
    setEnsembleFilter([]);
  };

  if (isLoading) return <Spinner animation="border" />;

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>User Management</h2>
        <Link to="/admin/users/new" className="btn btn-primary">Register User</Link>
      </div>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Row className="g-3">
        <Col md={3} style={{ borderRight: '1px solid var(--bs-border-color)' }}>
          <div className="pe-2">
            <Form.Control
              size="sm"
              placeholder="Search by username..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="mb-3"
            />

            <FilterPanelSection
              label="Roles"
              options={ALL_ROLES.map((r) => ({ value: r, label: r }))}
              selected={roleFilter}
              onChange={(v) => setRoleFilter(v as string[])}
            />
            <FilterPanelSection
              label="Ensembles"
              options={ensembles.data?.map((e) => ({ value: e.id, label: e.name })) ?? []}
              selected={ensembleFilter}
              onChange={(v) => setEnsembleFilter(v as number[])}
            />

            {hasFilters && (
              <Button variant="outline-secondary" size="sm" className="w-100 mt-1" onClick={clearAllFilters}>
                Clear all filters
              </Button>
            )}
          </div>
        </Col>

        <Col md={9}>
      <Table striped hover responsive>
        <thead>
          <tr>
            <SortTh label="Username" sortKey="userName" {...sortThProps} />
            <SortTh label="Email" sortKey="email" {...sortThProps} />
            <SortTh label="Name" sortKey="name" {...sortThProps} />
            <th>Roles</th>
            <th>Ensembles</th>
            <SortTh label="Last Login" sortKey="lastLogin" {...sortThProps} />
            <th></th>
          </tr>
        </thead>
        <tbody>
          {filteredActive.map((u) => (
            <tr key={u.id}>
              <td>{u.userName}</td>
              <td>{u.email}</td>
              <td>{[u.firstName, u.lastName].filter(Boolean).join(' ') || '-'}</td>
              <td>
                <div className="d-flex gap-1 flex-wrap">
                  {u.roles.map((r) => (
                    <Badge key={r} bg={roleBadgeBg(r)}>{r}</Badge>
                  ))}
                </div>
              </td>
              <td>
                <div className="d-flex gap-1 flex-wrap">
                  {u.ensembles.map((e) => (
                    <Badge key={e.id} bg="secondary">{e.name}</Badge>
                  ))}
                </div>
              </td>
              <td className="text-nowrap">{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : 'Never'}</td>
              <td>
                <div className="d-flex gap-1">
                  <Link to={`/admin/users/${u.id}`} className="btn btn-sm btn-outline-primary">
                    Manage
                  </Link>
                  <Button size="sm" variant="outline-danger" onClick={() => setToggleTarget(u)}>
                    Disable
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </Table>

      {filteredDisabled.length > 0 && (
        <>
          <h5 className="mt-4 text-muted">Disabled Users</h5>
          <Table striped hover responsive className="mt-2">
            <thead>
              <tr>
                <SortTh label="Username" sortKey="userName" {...sortThProps} />
                <SortTh label="Email" sortKey="email" {...sortThProps} />
                <SortTh label="Name" sortKey="name" {...sortThProps} />
                <th>Roles</th>
                <th>Ensembles</th>
                <SortTh label="Last Login" sortKey="lastLogin" {...sortThProps} />
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filteredDisabled.map((u) => (
                <tr key={u.id} className="text-muted">
                  <td>{u.userName}</td>
                  <td>{u.email}</td>
                  <td>{[u.firstName, u.lastName].filter(Boolean).join(' ') || '-'}</td>
                  <td>
                    <div className="d-flex gap-1 flex-wrap">
                      {u.roles.map((r) => (
                        <Badge key={r} bg="secondary">{r}</Badge>
                      ))}
                    </div>
                  </td>
                  <td>
                    <div className="d-flex gap-1 flex-wrap">
                      {u.ensembles.map((e) => (
                        <Badge key={e.id} bg="secondary">{e.name}</Badge>
                      ))}
                    </div>
                  </td>
                  <td className="text-nowrap">{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : 'Never'}</td>
                  <td>
                    <div className="d-flex gap-1">
                      <Link to={`/admin/users/${u.id}`} className="btn btn-sm btn-outline-primary">
                        Manage
                      </Link>
                      <Button size="sm" variant="outline-success" onClick={() => setToggleTarget(u)}>
                        Enable
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        </>
      )}
        </Col>
      </Row>

      <ConfirmModal
        show={!!toggleTarget}
        title={toggleTarget?.isDisabled ? 'Enable User' : 'Disable User'}
        message={`Are you sure you want to ${toggleTarget?.isDisabled ? 'enable' : 'disable'} "${toggleTarget?.userName}"?`}
        confirmLabel={toggleTarget?.isDisabled ? 'Enable' : 'Disable'}
        confirmVariant={toggleTarget?.isDisabled ? 'success' : 'danger'}
        onConfirm={() => toggleTarget && toggleMutation.mutate(toggleTarget)}
        onCancel={() => setToggleTarget(null)}
      />
    </>
  );
}
