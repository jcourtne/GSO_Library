import { Container, Nav, Navbar, NavDropdown } from 'react-bootstrap';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

export default function AppNavbar() {
  const { isAuthenticated, username, isAdmin, canEdit, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <Navbar bg="dark" variant="dark" expand="lg" sticky="top">
      <Container>
        <Navbar.Brand as={Link} to="/">GSO Library</Navbar.Brand>
        <Navbar.Toggle aria-controls="main-nav" />
        <Navbar.Collapse id="main-nav">
          {isAuthenticated && (
            <>
              <Nav className="me-auto">
                <Nav.Link as={Link} to="/arrangements">Arrangements</Nav.Link>
                <Nav.Link as={Link} to="/games">Games</Nav.Link>
                <Nav.Link as={Link} to="/series">Series</Nav.Link>
                <Nav.Link as={Link} to="/instruments">Instruments</Nav.Link>
                <Nav.Link as={Link} to="/performances">Performances</Nav.Link>
                {isAdmin() && (
                  <Nav.Link as={Link} to="/admin/users">Users</Nav.Link>
                )}
              </Nav>
              <Nav>
                <NavDropdown title={username || 'Account'} align="end">
                  {canEdit() && (
                    <>
                      <NavDropdown.Item as={Link} to="/arrangements/new">
                        New Arrangement
                      </NavDropdown.Item>
                      <NavDropdown.Divider />
                    </>
                  )}
                  <NavDropdown.Item onClick={handleLogout}>Logout</NavDropdown.Item>
                </NavDropdown>
              </Nav>
            </>
          )}
        </Navbar.Collapse>
      </Container>
    </Navbar>
  );
}
