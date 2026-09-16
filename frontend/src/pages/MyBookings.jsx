import { useState, useEffect } from 'react';
import { bookingsApi } from '../api/client';
import Layout from '../components/Layout';

function formatDate(iso) {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

const FILTERS = ['all', 'upcoming', 'past', 'cancelled'];

export default function MyBookings() {
  const [bookings, setBookings]     = useState([]);
  const [loading, setLoading]       = useState(true);
  const [error, setError]           = useState('');
  const [filter, setFilter]         = useState('upcoming');
  const [cancelling, setCancelling] = useState(null);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setBookings(await bookingsApi.myBookings());
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleCancel = async (id) => {
    if (!confirm('Cancel this booking?')) return;
    setCancelling(id);
    try { await bookingsApi.cancel(id); load(); }
    catch (err) { alert(err.message); }
    finally { setCancelling(null); }
  };

  const now = new Date();

  const filtered = bookings.filter(b => {
    if (filter === 'upcoming')  return b.status === 'Confirmed' && new Date(b.startTime) > now;
    if (filter === 'past')      return b.status === 'Confirmed' && new Date(b.startTime) <= now;
    if (filter === 'cancelled') return b.status === 'Cancelled';
    return true;
  });

  const counts = {
    all:       bookings.length,
    upcoming:  bookings.filter(b => b.status === 'Confirmed' && new Date(b.startTime) > now).length,
    past:      bookings.filter(b => b.status === 'Confirmed' && new Date(b.startTime) <= now).length,
    cancelled: bookings.filter(b => b.status === 'Cancelled').length,
  };

  return (
    <Layout>
      <div className="gym-page-header">
        <h1 className="gym-page-title">My Bookings</h1>
        {!loading && (
          <p className="gym-page-subtitle">{counts.upcoming} upcoming</p>
        )}
      </div>

      <div className="d-flex gap-2 mb-4 flex-wrap">
        {FILTERS.map(f => (
          <button
            key={f}
            className={filter === f ? 'btn-gym btn-sm' : 'btn-gym-ghost btn-sm'}
            onClick={() => setFilter(f)}
          >
            {f.charAt(0).toUpperCase() + f.slice(1)}
            <span style={{
              marginLeft: '6px',
              fontSize: '0.6rem',
              background: filter === f ? 'rgba(0,0,0,0.2)' : 'rgba(255,255,255,0.06)',
              padding: '1px 6px',
              borderRadius: '2px',
            }}>
              {counts[f]}
            </span>
          </button>
        ))}
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading bookings</div>
      ) : filtered.length === 0 ? (
        <div className="gym-empty">No {filter === 'all' ? '' : filter} bookings found.</div>
      ) : (
        <div className="table-responsive">
          <table className="table table-hover align-middle">
            <thead>
              <tr>
                <th>Program</th>
                <th>Trainer</th>
                <th>Session Start</th>
                <th>Session End</th>
                <th>Booked On</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(b => (
                <tr key={b.bookingId}>
                  <td className="fw-600">{b.programTitle}</td>
                  <td className="text-muted">{b.trainerName}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>{formatDate(b.startTime)}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>{formatDate(b.endTime)}</td>
                  <td style={{ whiteSpace: 'nowrap', color: 'var(--text-muted)', fontSize: '0.8rem' }}>
                    {formatDate(b.bookedAt)}
                  </td>
                  <td>
                    {b.status === 'Confirmed'
                      ? <span className="gym-badge gym-badge-active">Confirmed</span>
                      : <span className="gym-badge gym-badge-inactive">Cancelled</span>
                    }
                  </td>
                  <td>
                    {b.status === 'Confirmed' && new Date(b.startTime) > now && (
                      <button
                        className="btn-gym-danger btn-sm"
                        disabled={cancelling === b.bookingId}
                        onClick={() => handleCancel(b.bookingId)}
                      >
                        {cancelling === b.bookingId ? '...' : 'Cancel'}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Layout>
  );
}
