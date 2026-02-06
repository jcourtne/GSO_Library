import { useState } from 'react';
import { Alert, Badge, Button, Table, Spinner } from 'react-bootstrap';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { authApi } from '../../api/auth';
import ConfirmModal from '../../components/common/ConfirmModal';
import type { UserResponse } from '../../types';

export default function UserList() {
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [toggleTarget, setToggleTarget] = useState<UserResponse | null>(null);

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => authApi.getUsers(),
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

  if (isLoading) return <Spinner animation="border" />;

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2>User Management</h2>
        <Link to="/admin/users/new" className="btn btn-primary">Register User</Link>
      </div>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}

      <Table striped hover responsive>
        <thead>
          <tr>
            <th>Username</th>
            <th>Email</th>
            <th>Name</th>
            <th>Roles</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {users?.map((u) => (
            <tr key={u.id}>
              <td>{u.userName}</td>
              <td>{u.email}</td>
              <td>{[u.firstName, u.lastName].filter(Boolean).join(' ') || '-'}</td>
              <td>
                <div className="d-flex gap-1 flex-wrap">
                  {u.roles.map((r) => (
                    <Badge key={r} bg={r === 'Admin' ? 'danger' : r === 'Editor' ? 'warning' : 'secondary'}>
                      {r}
                    </Badge>
                  ))}
                </div>
              </td>
              <td>
                <Badge bg={u.isDisabled ? 'danger' : 'success'}>
                  {u.isDisabled ? 'Disabled' : 'Active'}
                </Badge>
              </td>
              <td>
                <div className="d-flex gap-1">
                  <Link to={`/admin/users/${u.id}`} className="btn btn-sm btn-outline-primary">
                    Manage
                  </Link>
                  <Button
                    size="sm"
                    variant={u.isDisabled ? 'outline-success' : 'outline-danger'}
                    onClick={() => setToggleTarget(u)}
                  >
                    {u.isDisabled ? 'Enable' : 'Disable'}
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </Table>

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
