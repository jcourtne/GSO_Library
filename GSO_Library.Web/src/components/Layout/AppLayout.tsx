import { Container } from 'react-bootstrap';
import { Outlet } from 'react-router-dom';
import AppNavbar from './AppNavbar';

export default function AppLayout() {
  return (
    <div className="d-flex flex-column min-vh-100">
      <AppNavbar />
      <Container className="flex-grow-1 py-4">
        <Outlet />
      </Container>
      <footer className="bg-dark text-light text-center py-3 mt-auto">
        <small>GSO Library &copy; {new Date().getFullYear()}</small>
      </footer>
    </div>
  );
}
