import { useState } from 'react';
import { Alert, Badge, Button, Card, Col, Form, Row, Spinner } from 'react-bootstrap';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { authApi } from '../../api/auth';

const ALL_ROLES = ['Admin', 'Editor', 'User'];

export default function UserDetail() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [selectedRole, setSelectedRole] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  const { data: user, isLoading } = useQuery({
    queryKey: ['user', id],
    queryFn: () => authApi.getUser(id!),
    enabled: !!id,
  });

  const grantMutation = useMutation({
    mutationFn: (role: string) => authApi.grantRole({ userId: id!, role }),
    onSuccess: (resp) => {
      queryClient.invalidateQueries({ queryKey: ['user', id] });
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setSuccess(resp.message);
      setSelectedRole('');
    },
    onError: () => setError('Failed to grant role'),
  });

  const removeMutation = useMutation({
    mutationFn: (role: string) => authApi.removeRole({ userId: id!, role }),
    onSuccess: (resp) => {
      queryClient.invalidateQueries({ queryKey: ['user', id] });
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setSuccess(resp.message);
    },
    onError: () => setError('Failed to remove role'),
  });

  const resetPasswordMutation = useMutation({
    mutationFn: () => authApi.resetPassword(id!, { newPassword }),
    onSuccess: (resp) => {
      setSuccess(resp.message);
      setNewPassword('');
      setConfirmPassword('');
    },
    onError: () => setError('Failed to reset password'),
  });

  const toggleMutation = useMutation({
    mutationFn: () => user!.isDisabled ? authApi.enableUser(id!) : authApi.disableUser(id!),
    onSuccess: (resp) => {
      queryClient.invalidateQueries({ queryKey: ['user', id] });
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setSuccess(resp.message);
    },
    onError: () => setError('Failed to update status'),
  });

  if (isLoading) return <Spinner animation="border" />;
  if (!user) return <Alert variant="danger">User not found</Alert>;

  const availableRoles = ALL_ROLES.filter((r) => !user.roles.includes(r));

  return (
    <>
      <h2>Manage User: {user.userName}</h2>
      {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert variant="success" dismissible onClose={() => setSuccess('')}>{success}</Alert>}

      <Row className="g-4">
        <Col md={6}>
          <Card>
            <Card.Body>
              <Card.Title>User Info</Card.Title>
              <p><strong>Username:</strong> {user.userName}</p>
              <p><strong>Email:</strong> {user.email}</p>
              <p><strong>Name:</strong> {[user.firstName, user.lastName].filter(Boolean).join(' ') || '-'}</p>
              <p>
                <strong>Status:</strong>{' '}
                <Badge bg={user.isDisabled ? 'danger' : 'success'}>
                  {user.isDisabled ? 'Disabled' : 'Active'}
                </Badge>
              </p>
              <Button
                variant={user.isDisabled ? 'success' : 'danger'}
                onClick={() => toggleMutation.mutate()}
                disabled={toggleMutation.isPending}
              >
                {user.isDisabled ? 'Enable Account' : 'Disable Account'}
              </Button>
            </Card.Body>
          </Card>
        </Col>

        <Col md={6}>
          <Card className="mb-4">
            <Card.Body>
              <Card.Title>Reset Password</Card.Title>
              <Form onSubmit={(e) => {
                e.preventDefault();
                if (newPassword && newPassword === confirmPassword) {
                  resetPasswordMutation.mutate();
                }
              }}>
                <Form.Group className="mb-2">
                  <Form.Label>New Password</Form.Label>
                  <Form.Control
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    required
                  />
                </Form.Group>
                <Form.Group className="mb-3">
                  <Form.Label>Confirm Password</Form.Label>
                  <Form.Control
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    isInvalid={!!confirmPassword && confirmPassword !== newPassword}
                    required
                  />
                  <Form.Control.Feedback type="invalid">
                    Passwords do not match
                  </Form.Control.Feedback>
                </Form.Group>
                <Button
                  type="submit"
                  variant="warning"
                  disabled={!newPassword || newPassword !== confirmPassword || resetPasswordMutation.isPending}
                >
                  Reset Password
                </Button>
              </Form>
            </Card.Body>
          </Card>

          <Card>
            <Card.Body>
              <Card.Title>Roles</Card.Title>
              <div className="d-flex flex-wrap gap-2 mb-3">
                {user.roles.map((r) => (
                  <Badge key={r} bg={r === 'Admin' ? 'danger' : r === 'Editor' ? 'warning' : 'secondary'} className="d-flex align-items-center gap-1 fs-6">
                    {r}
                    <Button
                      size="sm"
                      variant="link"
                      className="text-white p-0 ms-1"
                      onClick={() => removeMutation.mutate(r)}
                      title={`Remove ${r} role`}
                    >
                      &times;
                    </Button>
                  </Badge>
                ))}
              </div>

              {availableRoles.length > 0 && (
                <Form className="d-flex gap-2" onSubmit={(e) => { e.preventDefault(); if (selectedRole) grantMutation.mutate(selectedRole); }}>
                  <Form.Select size="sm" value={selectedRole} onChange={(e) => setSelectedRole(e.target.value)}>
                    <option value="">Add role...</option>
                    {availableRoles.map((r) => (
                      <option key={r} value={r}>{r}</option>
                    ))}
                  </Form.Select>
                  <Button size="sm" type="submit" disabled={!selectedRole || grantMutation.isPending}>
                    Add
                  </Button>
                </Form>
              )}
            </Card.Body>
          </Card>
        </Col>
      </Row>
    </>
  );
}
