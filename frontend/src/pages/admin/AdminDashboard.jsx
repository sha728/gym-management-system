import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { usersApi, bookingsApi, membershipsApi, sessionsApi } from '../../api/client';
import Layout from '../../components/Layout';

function StatCard({ label, value, to, hint }) {
  return (
    <div className="col-6 col-xl-3">
      <Link to={to} className="text-decoration-none d-block">
        <div className="gym-stat-card">
          <div className="gym-stat-value">{value ?? '--'}</div>
          <div className="gym-stat-label">{label}</div>
          {hint && (
            <div style={{ fontSize: '0.65rem', color: 'var(--text-muted)', marginTop: '0.5rem' }}>
              {hint}
            </div>
          )}
        </div>
      </Link>
    </div>
  );
}

const QUICK_LINKS = [
  { label: 'Manage Programs',    to: '/admin/programs' },
  { label: 'Manage Trainers',    to: '/admin/trainers' },
  { label: 'Manage Sessions',    to: '/admin/sessions' },
  { label: 'Review Memberships', to: '/admin/memberships' },
  { label: 'View Members',       to: '/admin/members' },
  { label: 'View Bookings',      to: '/admin/bookings' },
];

export default function AdminDashboard() {
  const [stats, setStats]   = useState({});
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function load() {
      try {
        const [members, bookings, memberships, sessions] = await Promise.allSettled([
          usersApi.getMembers({ pageSize: 1 }),
          bookingsApi.getAll({ pageSize: 1 }),
          membershipsApi.getAll({ status: 'Pending', pageSize: 1 }),
          sessionsApi.getAll({ includeInactive: false }),
        ]);
        setStats({
          members:            members.status     === 'fulfilled' ? members.value.totalCount     : '?',
          bookings:           bookings.status    === 'fulfilled' ? bookings.value.totalCount    : '?',
          pendingMemberships: memberships.status === 'fulfilled' ? memberships.value.totalCount : '?',
          activeSessions:     sessions.status    === 'fulfilled' ? sessions.value.length        : '?',
        });
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  return (
    <Layout>
      <div className="gym-page-header">
        <h1 className="gym-page-title">Overview</h1>
        <p className="gym-page-subtitle">Gym at a glance.</p>
      </div>

      {loading ? (
        <div className="gym-loading">Loading stats</div>
      ) : (
        <div className="row g-3 mb-5">
          <StatCard label="Total Members"       value={stats.members}            to="/admin/members"     hint="Registered accounts" />
          <StatCard label="Total Bookings"      value={stats.bookings}           to="/admin/bookings"    hint="All time" />
          <StatCard label="Pending Memberships" value={stats.pendingMemberships} to="/admin/memberships" hint="Awaiting review" />
          <StatCard label="Active Sessions"     value={stats.activeSessions}     to="/admin/sessions"    hint="Upcoming" />
        </div>
      )}

      <p className="gym-section-label">Quick Access</p>
      <div className="row g-2">
        {QUICK_LINKS.map(link => (
          <div key={link.to} className="col-6 col-md-4 col-lg-3">
            <Link to={link.to} className="gym-quick-link">{link.label}</Link>
          </div>
        ))}
      </div>
    </Layout>
  );
}
