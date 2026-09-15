import { useState, useEffect } from 'react';
import { usersApi } from '../../api/client';
import Layout from '../../components/Layout';

function formatDate(iso) {
  return new Date(iso).toLocaleDateString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

const PAGE_SIZE = 20;

export default function AdminMembers() {
  const [members, setMembers] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = async (p = 1) => {
    setLoading(true);
    setError('');
    try {
      const data = await usersApi.getMembers({ page: p, pageSize: PAGE_SIZE });
      setMembers(data.items);
      setTotalCount(data.totalCount);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(page); }, [page]);

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  return (
    <Layout>
      <div className="mb-4">
        <h1 className="gym-page-title">Members</h1>
        <p className="text-muted">{totalCount} registered members</p>
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading...</div>
      ) : members.length === 0 ? (
        <p className="text-muted">No members found.</p>
      ) : (
        <>
          <div className="table-responsive">
            <table className="gym-table">
              <thead>
                <tr>
                  <th>Email</th>
                  <th>Joined</th>
                </tr>
              </thead>
              <tbody>
                {members.map(m => (
                  <tr key={m.userId}>
                    <td>{m.email}</td>
                    <td>{formatDate(m.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="d-flex gap-2 align-items-center mt-3">
              <button className="btn btn-gym-outline btn-sm" disabled={page === 1} onClick={() => setPage(p => p - 1)}>
                Prev
              </button>
              <span className="text-muted small">Page {page} of {totalPages}</span>
              <button className="btn btn-gym-outline btn-sm" disabled={page === totalPages} onClick={() => setPage(p => p + 1)}>
                Next
              </button>
            </div>
          )}
        </>
      )}
    </Layout>
  );
}
