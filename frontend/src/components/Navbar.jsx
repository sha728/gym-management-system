import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function Navbar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <nav className="navbar navbar-expand-lg navbar-dark gym-navbar">
      <div className="container">
        <Link className="navbar-brand gym-brand" to={user?.role === 'Admin' ? '/admin/dashboard' : '/programs'}>
          APEX GYM
        </Link>

        <button
          className="navbar-toggler border-0"
          type="button"
          data-bs-toggle="collapse"
          data-bs-target="#gymNav"
          aria-controls="gymNav"
          aria-expanded="false"
          aria-label="Toggle navigation"
        >
          <span className="navbar-toggler-icon" />
        </button>

        <div className="collapse navbar-collapse" id="gymNav">
          {user?.role === 'Member' && (
            <ul className="navbar-nav me-auto gap-1">
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/programs">Programs</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/trainers">Trainers</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/sessions">Sessions</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/my-bookings">My Bookings</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/membership">Membership</NavLink>
              </li>
            </ul>
          )}

          {user?.role === 'Admin' && (
            <ul className="navbar-nav me-auto gap-1">
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/admin/dashboard">Overview</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/admin/programs">Programs</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/admin/trainers">Trainers</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/admin/sessions">Sessions</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/admin/memberships">Memberships</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/admin/members">Members</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link gym-nav-link" to="/admin/bookings">Bookings</NavLink>
              </li>
            </ul>
          )}

          {user && (
            <div className="d-flex align-items-center gap-3 ms-auto">
              <span className="gym-user-label">
                {user.email}
                <span className="gym-role-badge ms-2">{user.role.toUpperCase()}</span>
              </span>
              <button className="btn btn-gym-outline btn-sm" onClick={handleLogout}>
                Sign Out
              </button>
            </div>
          )}
        </div>
      </div>
    </nav>
  );
}
