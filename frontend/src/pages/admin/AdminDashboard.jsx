import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { usersApi, bookingsApi, membershipsApi, sessionsApi } from '../../api/client';
import Layout from '../../components/Layout';

function StatCard({ label, value, to }) {
  return (
    <div className="col-6 col-lg-3">
      <Link to={to} className="text-decoration-none">
        <div className="gym-stat-card">
          <div className="gym-stat-card-value">{value ?? '--'}</div>
          <div className="gym-stat-card-label">{label}</div>
        </div>
      </Link>
    </div>
  );
}

export default function AdminDashboard() {
  const [stats, setStats] = useState({});
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
          members: members.status === 'fulfilled' ? members.value.totalCount : '?',
          bookings: bookings.status === 'fulfilled' ? bookings.value.totalCount : '?',
          pendingMemberships: memberships.status === 'fulfilled' ? memberships.value.totalCount : '?',
          activeSessions: sessions.status === 'fulfilled' ? sessions.value.length : '?',
        });
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  return (
    <Layout>
      <div className="mb-4">
        <h1 className="gym-page-title">Overview</h1>
        <p className="text-muted">Gym at a glance.</p>
      </div>

      {loading ? (
        <div className="gym-loading">Loading...</div>
      ) : (
        <div className="row g-3 mb-5">
          <StatCard label="Total Members" value={stats.members} to="/admin/members" />
          <StatCard label="Total Bookings" value={stats.bookings} to="/admin/bookings" />
          <StatCard label="Pending Memberships" value={stats.pendingMemberships} to="/admin/memberships" />
          <StatCard label="Active Sessions" value={stats.activeSessions} to="/admin/sessions" />
        </div>
      )}

      <h5 className="gym-section-title mb-3">Quick Links</h5>
      <div className="row g-3">
        {[
          { label: 'Manage Programs', to: '/admin/programs' },
          { label: 'Manage Trainers', to: '/admin/trainers' },
          { label: 'Manage Sessions', to: '/admin/sessions' },
          { label: 'Review Memberships', to: '/admin/memberships' },
          { label: 'View Members', to: '/admin/members' },
          { label: 'View Bookings', to: '/admin/bookings' },
        ].map(link => (
          <div key={link.to} className="col-6 col-md-4 col-lg-3">
            <Link to={link.to} className="gym-quick-link">
              {link.label}
            </Link>
          </div>
        ))}
      </div>
    </Layout>
  );
}
