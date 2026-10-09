import { Col, Container, Nav, Navbar, NavDropdown, Row } from 'react-bootstrap';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

export default function AppNavbar() {
  const { isAuthenticated, username, isAdmin, canEdit, isEnsembleLibrarian, logout } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  const isActive = (...prefixes: string[]) => prefixes.some((p) => pathname.startsWith(p));

  // Outlined nav buttons. NavDropdown puts className on its wrapper, and its inner toggle already has
  // .nav-link padding at lg+ (0.5rem), so it gets less wrapper padding there to match Nav.Link's px-3.
  const linkButton = 'border border-secondary rounded px-3';
  const dropdownButton = 'border border-secondary rounded px-3 px-lg-2';

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <Navbar bg="dark" variant="dark" expand="lg" sticky="top">
      {/* Same container and 3/9 grid as the page content (AppLayout + ArrangementList),
          so the brand lines up with the filter column and the links with the data column. */}
      <Container fluid style={{ maxWidth: '1600px' }}>
        <Row className="gx-3 flex-grow-1 align-items-center">
          <Col lg={3} className="d-flex align-items-center justify-content-between">
            <Navbar.Brand as={Link} to="/">GSO Library</Navbar.Brand>
            <Navbar.Toggle aria-controls="main-nav" />
          </Col>
          <Col lg={9}>
            <Navbar.Collapse id="main-nav">
              {isAuthenticated && (
                <>
                  <Nav className="me-auto gap-2">
                    <Nav.Link as={Link} to="/arrangements" className={linkButton}>Arrangements</Nav.Link>
                    <NavDropdown
                      title="Arrangement Fields"
                      id="nav-arrangement-fields"
                      className={dropdownButton}
                      active={isActive('/games', '/series', '/instruments')}
                    >
                      <NavDropdown.Item as={Link} to="/games">Games</NavDropdown.Item>
                      <NavDropdown.Item as={Link} to="/series">Series</NavDropdown.Item>
                      <NavDropdown.Item as={Link} to="/instruments">Instruments</NavDropdown.Item>
                    </NavDropdown>
                    <NavDropdown
                      title="Orchestra"
                      id="nav-orchestra"
                      className={dropdownButton}
                      active={isActive('/ensembles', '/performances', '/seasons')}
                    >
                      <NavDropdown.Item as={Link} to="/ensembles">Ensembles</NavDropdown.Item>
                      <NavDropdown.Item as={Link} to="/performances">Performances</NavDropdown.Item>
                      <NavDropdown.Item as={Link} to="/seasons">Seasons</NavDropdown.Item>
                    </NavDropdown>
                    {isAdmin() && (
                      <NavDropdown title="Admin" id="nav-admin" className={dropdownButton} active={isActive('/admin')}>
                        <NavDropdown.Item as={Link} to="/admin/users">Users</NavDropdown.Item>
                        <NavDropdown.Item as={Link} to="/admin/audit-events">Audit Log</NavDropdown.Item>
                      </NavDropdown>
                    )}
                  </Nav>
                  <Nav className="mt-2 mt-lg-0">
                    <NavDropdown title={username || 'Account'} align="end" className={dropdownButton}>
                      {(canEdit() || isEnsembleLibrarian()) && (
                        <>
                          <NavDropdown.Item as={Link} to="/arrangements/new">
                            New Arrangement
                          </NavDropdown.Item>
                          <NavDropdown.Divider />
                        </>
                      )}
                      <NavDropdown.Item as={Link} to="/change-password">Change Password</NavDropdown.Item>
                      <NavDropdown.Divider />
                      <NavDropdown.Item onClick={handleLogout}>Logout</NavDropdown.Item>
                    </NavDropdown>
                  </Nav>
                </>
              )}
            </Navbar.Collapse>
          </Col>
        </Row>
      </Container>
    </Navbar>
  );
}
