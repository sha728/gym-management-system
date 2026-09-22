import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

/**
 * Wraps a route so only authenticated users (and optionally a specific role) can access it.
 * Unauthenticated users are sent to /login.
 * Wrong-role users are sent to their own home.
 */
export default function ProtectedRoute({ children, role }) {
  const { user } = useAuth();

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (role && user.role !== role) {
    return <Navigate to={user.role === 'Admin' ? '/admin/dashboard' : '/programs'} replace />;
  }

  return children;
}
