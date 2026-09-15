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

  const load = async (p = 1, status = statusFilter) => {
    setLoading(true);
    setError('');
    try {
      const data = await bookingsApi.getAll({ page: p, pageSize: PAGE_SIZE, status: status || undefined });
      setBookings(data.items);
      setTotalCount(data.totalCount);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setPage(1);
    load(1, statusFilter);
  }, [statusFilter]);

  useEffect(() => { load(page); }, [page]);

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  return (
    <Layout>
      <div className="d-flex align-items-center justify-content-between mb-4">
        <div>
          <h1 className="gym-page-title">Bookings</h1>
          <p className="text-muted">{totalCount} total</p>
        </div>
        <select
          className="gym-input"
          style={{ maxWidth: 160 }}
          value={statusFilter}
          onChange={e => setStatusFilter(e.target.value)}
        >
          <option value="">All Statuses</option>
          <option value="Confirmed">Confirmed</option>
          <option value="Cancelled">Cancelled</option>
        </select>
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading...</div>
      ) : bookings.length === 0 ? (
        <p className="text-muted">No bookings found.</p>
      ) : (
        <>
          <div className="table-responsive">
            <table className="gym-table">
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
                    <td>{formatDate(b.startTime)}</td>
                    <td>{formatDate(b.endTime)}</td>
                    <td>{formatDate(b.bookedAt)}</td>
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
            <div className="d-flex gap-2 align-items-center mt-3">
              <button className="btn btn-gym-outline btn-sm" disabled={page === 1} onClick={() => setPage(p => p - 1)}>Prev</button>
              <span className="text-muted small">Page {page} of {totalPages}</span>
              <button className="btn btn-gym-outline btn-sm" disabled={page === totalPages} onClick={() => setPage(p => p + 1)}>Next</button>
            </div>
          )}
        </>
      )}
    </Layout>
  );
}
