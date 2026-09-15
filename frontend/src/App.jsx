import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';

// Public
import Login from './pages/Login';
import Register from './pages/Register';

// Member pages
import Programs from './pages/Programs';
import Trainers from './pages/Trainers';
import Sessions from './pages/Sessions';
import MyBookings from './pages/MyBookings';
import Membership from './pages/Membership';

// Admin pages
import AdminDashboard from './pages/admin/AdminDashboard';
import AdminMembers from './pages/admin/AdminMembers';
import AdminBookings from './pages/admin/AdminBookings';
import AdminMemberships from './pages/admin/AdminMemberships';

export default function App() {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          {/* Public */}
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />

          {/* Member */}
          <Route path="/programs"    element={<ProtectedRoute role="Member"><Programs /></ProtectedRoute>} />
          <Route path="/trainers"    element={<ProtectedRoute role="Member"><Trainers /></ProtectedRoute>} />
          <Route path="/sessions"    element={<ProtectedRoute role="Member"><Sessions /></ProtectedRoute>} />
          <Route path="/my-bookings" element={<ProtectedRoute role="Member"><MyBookings /></ProtectedRoute>} />
          <Route path="/membership"  element={<ProtectedRoute role="Member"><Membership /></ProtectedRoute>} />

          {/* Admin — Programs, Trainers, Sessions reuse the same shared pages (admin-aware) */}
          <Route path="/admin/dashboard"   element={<ProtectedRoute role="Admin"><AdminDashboard /></ProtectedRoute>} />
          <Route path="/admin/programs"    element={<ProtectedRoute role="Admin"><Programs /></ProtectedRoute>} />
          <Route path="/admin/trainers"    element={<ProtectedRoute role="Admin"><Trainers /></ProtectedRoute>} />
          <Route path="/admin/sessions"    element={<ProtectedRoute role="Admin"><Sessions /></ProtectedRoute>} />
          <Route path="/admin/members"     element={<ProtectedRoute role="Admin"><AdminMembers /></ProtectedRoute>} />
          <Route path="/admin/bookings"    element={<ProtectedRoute role="Admin"><AdminBookings /></ProtectedRoute>} />
          <Route path="/admin/memberships" element={<ProtectedRoute role="Admin"><AdminMemberships /></ProtectedRoute>} />

          {/* Default */}
          <Route path="/" element={<Navigate to="/login" replace />} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </Router>
    </AuthProvider>
  );
}
