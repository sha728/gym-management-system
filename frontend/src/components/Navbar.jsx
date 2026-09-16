import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function Navbar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const memberLinks = [
    { to: '/programs',    label: 'Programs' },
    { to: '/trainers',    label: 'Trainers' },
    { to: '/sessions',    label: 'Sessions' },
    { to: '/my-bookings', label: 'My Bookings' },
    { to: '/membership',  label: 'Membership' },
  ];

  const adminLinks = [
    { to: '/admin/dashboard',   label: 'Overview' },
    { to: '/admin/programs',    label: 'Programs' },
    { to: '/admin/trainers',    label: 'Trainers' },
    { to: '/admin/sessions',    label: 'Sessions' },
    { to: '/admin/memberships', label: 'Memberships' },
    { to: '/admin/members',     label: 'Members' },
    { to: '/admin/bookings',    label: 'Bookings' },
  ];

  const links = user?.role === 'Admin' ? adminLinks : memberLinks;

  return (
    <nav className="navbar navbar-expand-lg gym-navbar" aria-label="Main navigation">
      <div className="container">

        <Link
          className="navbar-brand gym-brand"
          to={user?.role === 'Admin' ? '/admin/dashboard' : '/programs'}
        >
          APEX GYM
        </Link>

        <button
          className="navbar-toggler border-0 shadow-none"
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
          <ul className="navbar-nav me-auto">
            {links.map(link => (
              <li className="nav-item" key={link.to}>
                <NavLink
                  className={({ isActive }) =>
                    `nav-link gym-nav-link${isActive ? ' active' : ''}`
                  }
                  to={link.to}
                >
                  {link.label}
                </NavLink>
              </li>
            ))}
          </ul>

          {user && (
            <div className="gym-nav-user">
              <div className="d-flex flex-column align-items-end">
                <span className="gym-user-email">{user.email}</span>
              </div>
              <span className="gym-role-chip">{user.role}</span>
              <button
                className="btn-gym-outline btn-sm"
                onClick={handleLogout}
                style={{ flexShrink: 0 }}
              >
                Sign Out
              </button>
            </div>
          )}
        </div>
      </div>
    </nav>
  );
}
