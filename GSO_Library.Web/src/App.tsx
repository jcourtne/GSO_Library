import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './contexts/AuthContext';
import AppLayout from './components/Layout/AppLayout';
import ProtectedRoute from './components/Layout/ProtectedRoute';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import ArrangementList from './pages/arrangements/ArrangementList';
import ArrangementDetail from './pages/arrangements/ArrangementDetail';
import ArrangementForm from './pages/arrangements/ArrangementForm';
import SeriesList from './pages/series/SeriesList';
import SeriesForm from './pages/series/SeriesForm';
import GameList from './pages/games/GameList';
import GameForm from './pages/games/GameForm';
import InstrumentList from './pages/instruments/InstrumentList';
import InstrumentForm from './pages/instruments/InstrumentForm';
import PerformanceList from './pages/performances/PerformanceList';
import PerformanceDetail from './pages/performances/PerformanceDetail';
import PerformanceForm from './pages/performances/PerformanceForm';
import EnsembleList from './pages/ensembles/EnsembleList';
import EnsembleDetail from './pages/ensembles/EnsembleDetail';
import EnsembleForm from './pages/ensembles/EnsembleForm';
import SeasonList from './pages/seasons/SeasonList';
import SeasonDetail from './pages/seasons/SeasonDetail';
import SeasonForm from './pages/seasons/SeasonForm';
import UserList from './pages/admin/UserList';
import UserDetail from './pages/admin/UserDetail';
import RegisterUser from './pages/admin/RegisterUser';
import AuditEvents from './pages/admin/AuditEvents';
import ChangePassword from './pages/ChangePassword';
import SeasonPublicPage from './pages/public/SeasonPublicPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

const librarianRoles = ['Admin', 'Librarian'];
const submitterRoles = ['Admin', 'Librarian', 'Submitter'];
const adminRoles = ['Admin'];

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/share/seasons/:token" element={<SeasonPublicPage />} />

            <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
              <Route index element={<Dashboard />} />

              {/* Arrangements */}
              <Route path="arrangements" element={<ArrangementList />} />
              <Route path="arrangements/:id" element={<ArrangementDetail />} />
              <Route path="arrangements/new" element={<ProtectedRoute requiredRoles={submitterRoles}><ArrangementForm /></ProtectedRoute>} />
              <Route path="arrangements/:id/edit" element={<ProtectedRoute requiredRoles={submitterRoles}><ArrangementForm /></ProtectedRoute>} />

              {/* Series */}
              <Route path="series" element={<SeriesList />} />
              <Route path="series/new" element={<ProtectedRoute requiredRoles={librarianRoles}><SeriesForm /></ProtectedRoute>} />
              <Route path="series/:id/edit" element={<ProtectedRoute requiredRoles={librarianRoles}><SeriesForm /></ProtectedRoute>} />

              {/* Games */}
              <Route path="games" element={<GameList />} />
              <Route path="games/new" element={<ProtectedRoute requiredRoles={librarianRoles}><GameForm /></ProtectedRoute>} />
              <Route path="games/:id/edit" element={<ProtectedRoute requiredRoles={librarianRoles}><GameForm /></ProtectedRoute>} />

              {/* Instruments */}
              <Route path="instruments" element={<InstrumentList />} />
              <Route path="instruments/new" element={<ProtectedRoute requiredRoles={librarianRoles}><InstrumentForm /></ProtectedRoute>} />
              <Route path="instruments/:id/edit" element={<ProtectedRoute requiredRoles={librarianRoles}><InstrumentForm /></ProtectedRoute>} />

              {/* Performances */}
              <Route path="performances" element={<PerformanceList />} />
              <Route path="performances/:id" element={<PerformanceDetail />} />
              <Route path="performances/new" element={<ProtectedRoute requiredRoles={librarianRoles}><PerformanceForm /></ProtectedRoute>} />
              <Route path="performances/:id/edit" element={<ProtectedRoute requiredRoles={librarianRoles}><PerformanceForm /></ProtectedRoute>} />

              {/* Ensembles */}
              <Route path="ensembles" element={<EnsembleList />} />
              <Route path="ensembles/:id" element={<EnsembleDetail />} />
              <Route path="ensembles/new" element={<ProtectedRoute requiredRoles={librarianRoles}><EnsembleForm /></ProtectedRoute>} />
              <Route path="ensembles/:id/edit" element={<ProtectedRoute requiredRoles={librarianRoles}><EnsembleForm /></ProtectedRoute>} />

              {/* Seasons */}
              <Route path="seasons" element={<SeasonList />} />
              <Route path="seasons/:id" element={<SeasonDetail />} />
              <Route path="seasons/new" element={<ProtectedRoute requiredRoles={librarianRoles}><SeasonForm /></ProtectedRoute>} />
              <Route path="seasons/:id/edit" element={<ProtectedRoute requiredRoles={librarianRoles}><SeasonForm /></ProtectedRoute>} />

              {/* Account */}
              <Route path="change-password" element={<ChangePassword />} />

              {/* Admin */}
              <Route path="admin/users" element={<ProtectedRoute requiredRoles={adminRoles}><UserList /></ProtectedRoute>} />
              <Route path="admin/users/new" element={<ProtectedRoute requiredRoles={adminRoles}><RegisterUser /></ProtectedRoute>} />
              <Route path="admin/users/:id" element={<ProtectedRoute requiredRoles={adminRoles}><UserDetail /></ProtectedRoute>} />
              <Route path="admin/audit-events" element={<ProtectedRoute requiredRoles={adminRoles}><AuditEvents /></ProtectedRoute>} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}
