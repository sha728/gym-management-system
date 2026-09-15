import { useState, useEffect } from 'react';
import { bookingsApi } from '../api/client';
import Layout from '../components/Layout';

function formatDate(iso) {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

function statusBadge(status) {
  return status === 'Confirmed'
    ? <span className="gym-badge-active">Confirmed</span>
    : <span className="gym-badge-inactive">Cancelled</span>;
}

export default function MyBookings() {
  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [filter, setFilter] = useState('all'); // all | upcoming | past | cancelled
  const [cancelling, setCancelling] = useState(null);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await bookingsApi.myBookings();
      setBookings(data);
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
    try {
      await bookingsApi.cancel(id);
      load();
    } catch (err) {
      alert(err.message);
    } finally {
      setCancelling(null);
    }
  };

  const now = new Date();
  const filtered = bookings.filter(b => {
    if (filter === 'upcoming') return b.status === 'Confirmed' && new Date(b.startTime) > now;
    if (filter === 'past') return b.status === 'Confirmed' && new Date(b.startTime) <= now;
    if (filter === 'cancelled') return b.status === 'Cancelled';
    return true;
  });

  return (
    <Layout>
      <div className="mb-4">
        <h1 className="gym-page-title">My Bookings</h1>
      </div>

      {/* Filter tabs */}
      <div className="d-flex gap-2 mb-4 flex-wrap">
        {['all', 'upcoming', 'past', 'cancelled'].map(f => (
          <button
            key={f}
            className={`btn btn-sm ${filter === f ? 'btn-gym' : 'btn-gym-outline'}`}
            onClick={() => setFilter(f)}
          >
            {f.charAt(0).toUpperCase() + f.slice(1)}
          </button>
        ))}
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading...</div>
      ) : filtered.length === 0 ? (
        <p className="text-muted">No bookings found.</p>
      ) : (
        <div className="table-responsive">
          <table className="gym-table">
            <thead>
              <tr>
                <th>Program</th>
                <th>Trainer</th>
                <th>Start</th>
                <th>End</th>
                <th>Booked On</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(b => (
                <tr key={b.bookingId}>
                  <td>{b.programTitle}</td>
                  <td>{b.trainerName}</td>
                  <td>{formatDate(b.startTime)}</td>
                  <td>{formatDate(b.endTime)}</td>
                  <td>{formatDate(b.bookedAt)}</td>
                  <td>{statusBadge(b.status)}</td>
                  <td>
                    {b.status === 'Confirmed' && new Date(b.startTime) > now && (
                      <button
                        className="btn btn-gym-danger btn-sm"
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
