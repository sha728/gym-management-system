import { useState, useEffect } from 'react';
import { bookingsApi } from '../../api/client';
import Layout from '../../components/Layout';

function formatDate(iso) {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

const PAGE_SIZE = 20;

export default function AdminBookings() {
  const [bookings, setBookings] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const loadBookings = async () => {
      setLoading(true);
      setError('');
      try {
        const data = await bookingsApi.getAll({ page, pageSize: PAGE_SIZE, status: statusFilter || undefined });
        setBookings(data.items);
        setTotalCount(data.totalCount);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    
    loadBookings();
  }, [page, statusFilter]);

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  return (
    <Layout>
      <div className="gym-page-header">
        <h1 className="gym-page-title">Bookings</h1>
        <p className="gym-page-subtitle">{totalCount} total booking{totalCount !== 1 ? 's' : ''}</p>
      </div>

      <div className="gym-filter-bar">
        <select
          className="gym-input"
          style={{ maxWidth: 160 }}
          value={statusFilter}
          onChange={e => {
            setStatusFilter(e.target.value);
            setPage(1);
          }}
        >
          <option value="">All Statuses</option>
          <option value="Confirmed">Confirmed</option>
          <option value="Cancelled">Cancelled</option>
        </select>
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading bookings</div>
      ) : bookings.length === 0 ? (
        <div className="gym-empty">No bookings found.</div>
      ) : (
        <>
          <div className="table-responsive">
            <table className="table table-hover align-middle">
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Program</th>
                  <th>Trainer</th>
                  <th>Session Start</th>
                  <th>Session End</th>
                  <th>Booked On</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {bookings.map(b => (
                  <tr key={b.bookingId}>
                    <td>{b.memberEmail}</td>
                    <td>{b.programTitle}</td>
                    <td>{b.trainerName}</td>
                    <td style={{ color: 'var(--text-muted)', fontSize: '0.83rem' }}>
                      {formatDate(b.startTime)}
                    </td>
                    <td style={{ color: 'var(--text-muted)', fontSize: '0.83rem' }}>
                      {formatDate(b.endTime)}
                    </td>
                    <td style={{ color: 'var(--text-muted)', fontSize: '0.83rem' }}>
                      {formatDate(b.bookedAt)}
                    </td>
                    <td>
                      {b.status === 'Confirmed'
                        ? <span className="gym-badge-active">Confirmed</span>
                        : <span className="gym-badge-inactive">Cancelled</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="gym-pagination">
              <button
                className="btn-gym-ghost btn-sm"
                disabled={page === 1}
                onClick={() => setPage(p => p - 1)}
              >
                Prev
              </button>
              <span className="gym-page-info">Page {page} of {totalPages}</span>
              <button
                className="btn-gym-ghost btn-sm"
                disabled={page === totalPages}
                onClick={() => setPage(p => p + 1)}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </Layout>
  );
}
