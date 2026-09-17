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
  const [members, setMembers]       = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage]             = useState(1);
  const [loading, setLoading]       = useState(true);
  const [error, setError]           = useState('');

  useEffect(() => {
    const loadMembers = async () => {
      setLoading(true);
      setError('');
      try {
        const data = await usersApi.getMembers({ page, pageSize: PAGE_SIZE });
        setMembers(data.items);
        setTotalCount(data.totalCount);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    
    loadMembers();
  }, [page]);

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  return (
    <Layout>
      <div className="gym-page-header">
        <h1 className="gym-page-title">Members</h1>
        <p className="gym-page-subtitle">{totalCount} registered member{totalCount !== 1 ? 's' : ''}</p>
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading members</div>
      ) : members.length === 0 ? (
        <div className="gym-empty">No members found.</div>
      ) : (
        <>
          <div className="table-responsive">
            <table className="table table-hover align-middle">
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
                    <td style={{ color: 'var(--text-muted)', fontSize: '0.83rem' }}>
                      {formatDate(m.createdAt)}
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
