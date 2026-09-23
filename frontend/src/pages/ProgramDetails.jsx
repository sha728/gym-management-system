import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { programsApi, sessionsApi, bookingsApi } from '../api/client';
import Layout from '../components/Layout';

function formatDate(iso) {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

export default function ProgramDetails() {
  const { id } = useParams();

  const [program, setProgram] = useState(null);
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [bookingId, setBookingId] = useState(null);
  const [bookingMsg, setBookingMsg] = useState({ text: '', ok: false });

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [p, s] = await Promise.all([
        programsApi.getById(id),
        sessionsApi.getAll({ programId: id }),
      ]);
      setProgram(p);
      setSessions(s);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadDetails = async () => {
      setLoading(true);
      setError('');
      try {
        const [p, s] = await Promise.all([
          programsApi.getById(id),
          sessionsApi.getAll({ programId: id }),
        ]);
        setProgram(p);
        setSessions(s);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    loadDetails();
  }, [id]);

  const handleBook = async (sessionId) => {
    setBookingId(sessionId);
    setBookingMsg({ text: '', ok: false });
    try {
      await bookingsApi.create(sessionId);
      setBookingMsg({ text: 'Booking confirmed.', ok: true });
      load();
    } catch (err) {
      setBookingMsg({ text: err.message, ok: false });
    } finally {
      setBookingId(null);
    }
  };

  if (loading) {
    return (
      <Layout>
        <div className="gym-loading">Loading program</div>
      </Layout>
    );
  }

  if (error || !program) {
    return (
      <Layout>
        <div className="gym-alert-error mb-3">{error || 'Program not found.'}</div>
        <Link to="/programs" className="btn-gym-ghost btn-sm">&larr; Back to Programs</Link>
      </Layout>
    );
  }

  return (
    <Layout>
      <Link to="/programs" className="btn-gym-ghost btn-sm mb-3">&larr; Back to Programs</Link>

      <div className="gym-page-header">
        <div className="d-flex align-items-center gap-2 mb-1">
          <h1 className="gym-page-title mb-0">{program.name}</h1>
          {!program.isActive && <span className="gym-badge gym-badge-inactive">Inactive</span>}
        </div>
        <p className="gym-page-subtitle">{program.durationInMinutes} min per session</p>
      </div>

      <div className="gym-card mb-4">
        <div className="gym-card-body">
          <p className="gym-card-text mb-0">
            {program.description || 'No description provided.'}
          </p>
        </div>
      </div>

      <h2 className="gym-page-title" style={{ fontSize: '1.25rem' }}>Upcoming Sessions</h2>

      {bookingMsg.text && (
        <div className={`mb-3 ${bookingMsg.ok ? 'gym-alert-success' : 'gym-alert-error'}`}>
          {bookingMsg.text}
        </div>
      )}

      {sessions.length === 0 ? (
        <div className="gym-empty">No sessions scheduled for this program yet.</div>
      ) : (
        <div className="table-responsive">
          <table className="table table-hover align-middle">
            <thead>
              <tr>
                <th>Trainer</th>
                <th>Start</th>
                <th>End</th>
                <th>Availability</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {sessions.map(s => (
                <tr key={s.sessionId}>
                  <td className="text-muted">{s.trainer.name}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>{formatDate(s.startTime)}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>{formatDate(s.endTime)}</td>
                  <td>
                    {s.availableSlots === 0
                      ? <span className="gym-badge gym-badge-full">Full</span>
                      : <span className="gym-badge gym-badge-open">{s.availableSlots} / {s.capacity} open</span>
                    }
                  </td>
                  <td>
                    {s.isActive && s.availableSlots > 0 && new Date(s.startTime) > new Date() ? (
                      <button
                        className="btn-gym btn-sm"
                        disabled={bookingId === s.sessionId}
                        onClick={() => handleBook(s.sessionId)}
                      >
                        {bookingId === s.sessionId ? 'Booking...' : 'Book'}
                      </button>
                    ) : (
                      <span className="text-muted" style={{ fontSize: '0.78rem' }}>
                        {s.availableSlots === 0 ? 'Full' : 'Unavailable'}
                      </span>
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
