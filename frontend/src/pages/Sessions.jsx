import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import { sessionsApi, programsApi, trainersApi, bookingsApi } from '../api/client';
import Layout from '../components/Layout';

function formatDate(iso) {
  return new Date(iso).toLocaleString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

function SessionModal({ session, programs, trainers, onClose, onSave }) {
  const empty = {
    fitnessProgramId: programs[0]?.fitnessProgramId || '',
    trainerId: trainers[0]?.trainerId || '',
    startTime: '',
    endTime: '',
    capacity: 10,
    isActive: true,
  };
  const [form, setForm] = useState(session
    ? {
        fitnessProgramId: session.fitnessProgram.fitnessProgramId,
        trainerId: session.trainer.trainerId,
        startTime: new Date(session.startTime).toISOString().slice(0, 16),
        endTime: new Date(session.endTime).toISOString().slice(0, 16),
        capacity: session.capacity,
        isActive: session.isActive,
      }
    : empty
  );
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const toUtc = (localStr) => new Date(localStr).toISOString();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSaving(true);
    try {
      const payload = { ...form, startTime: toUtc(form.startTime), endTime: toUtc(form.endTime) };
      if (session) {
        await sessionsApi.update(session.sessionId, payload);
      } else {
        await sessionsApi.create(payload);
      }
      onSave();
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="gym-modal-backdrop">
      <div className="gym-modal gym-modal-lg">
        <div className="gym-modal-header">
          <span className="gym-modal-title">{session ? 'Edit Session' : 'New Session'}</span>
          <button className="gym-modal-close" onClick={onClose} aria-label="Close">&#x2715;</button>
        </div>
        {error && <div className="gym-alert-error mb-3">{error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="row g-3">
            <div className="col-md-6">
              <label className="gym-label">Program</label>
              <select className="gym-input" value={form.fitnessProgramId}
                onChange={e => setForm({ ...form, fitnessProgramId: e.target.value })} required>
                {programs.map(p => (
                  <option key={p.fitnessProgramId} value={p.fitnessProgramId}>{p.name}</option>
                ))}
              </select>
            </div>
            <div className="col-md-6">
              <label className="gym-label">Trainer</label>
              <select className="gym-input" value={form.trainerId}
                onChange={e => setForm({ ...form, trainerId: e.target.value })} required>
                {trainers.map(t => (
                  <option key={t.trainerId} value={t.trainerId}>{t.name}</option>
                ))}
              </select>
            </div>
            <div className="col-md-6">
              <label className="gym-label">Start Time (local)</label>
              <input className="gym-input" type="datetime-local" value={form.startTime}
                onChange={e => setForm({ ...form, startTime: e.target.value })} required />
            </div>
            <div className="col-md-6">
              <label className="gym-label">End Time (local)</label>
              <input className="gym-input" type="datetime-local" value={form.endTime}
                onChange={e => setForm({ ...form, endTime: e.target.value })} required />
            </div>
            <div className="col-md-6">
              <label className="gym-label">Capacity</label>
              <input className="gym-input" type="number" min={1} value={form.capacity}
                onChange={e => setForm({ ...form, capacity: Number(e.target.value) })} required />
            </div>
            {session && (
              <div className="col-md-6 d-flex align-items-end pb-2 gap-2">
                <input type="checkbox" id="sessActive" className="form-check-input mt-0"
                  checked={form.isActive}
                  onChange={e => setForm({ ...form, isActive: e.target.checked })} />
                <label htmlFor="sessActive" className="gym-label mb-0">Active</label>
              </div>
            )}
          </div>
          <div className="d-flex gap-2 justify-content-end pt-3 mt-1">
            <button type="button" className="btn-gym-ghost btn-sm" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn-gym btn-sm" disabled={saving}>
              {saving ? 'Saving...' : 'Save Session'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default function Sessions() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  const [sessions, setSessions]         = useState([]);
  const [programs, setPrograms]         = useState([]);
  const [trainers, setTrainers]         = useState([]);
  const [loading, setLoading]           = useState(true);
  const [error, setError]               = useState('');
  const [filterProgram, setFilterProgram] = useState('');
  const [filterTrainer, setFilterTrainer] = useState('');
  const [modal, setModal]               = useState(null);
  const [bookingId, setBookingId]       = useState(null);
  const [bookingMsg, setBookingMsg]     = useState({ text: '', ok: false });

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [s, p, t] = await Promise.all([
        sessionsApi.getAll({ includeInactive: isAdmin }),
        programsApi.getAll(isAdmin),
        trainersApi.getAll(isAdmin),
      ]);
      setSessions(s); setPrograms(p); setTrainers(t);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadAll = async () => {
      setLoading(true);
      setError('');
      try {
        const [s, p, t] = await Promise.all([
          sessionsApi.getAll({ includeInactive: isAdmin }),
          programsApi.getAll(isAdmin),
          trainersApi.getAll(isAdmin),
        ]);
        setSessions(s); setPrograms(p); setTrainers(t);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    loadAll();
  }, [isAdmin]);

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

  const handleDelete = async (id) => {
    if (!confirm('Deactivate this session? All confirmed bookings will be cancelled.')) return;
    try { await sessionsApi.delete(id); load(); }
    catch (err) { alert(err.message); }
  };

  const filtered = sessions.filter(s => {
    if (filterProgram && s.fitnessProgram.fitnessProgramId !== filterProgram) return false;
    if (filterTrainer && s.trainer.trainerId !== filterTrainer) return false;
    return true;
  });

  return (
    <Layout>
      <div className="gym-page-header d-flex align-items-center justify-content-between">
        <div>
          <h1 className="gym-page-title">Sessions</h1>
          {!loading && (
            <p className="gym-page-subtitle">{filtered.length} session{filtered.length !== 1 ? 's' : ''}</p>
          )}
        </div>
        {isAdmin && (
          <button className="btn-gym" onClick={() => setModal('create')}>+ New Session</button>
        )}
      </div>

      <div className="gym-filter-bar">
        <select className="gym-input" value={filterProgram}
          onChange={e => setFilterProgram(e.target.value)}
          style={{ maxWidth: 200 }}>
          <option value="">All Programs</option>
          {programs.map(p => (
            <option key={p.fitnessProgramId} value={p.fitnessProgramId}>{p.name}</option>
          ))}
        </select>
        <select className="gym-input" value={filterTrainer}
          onChange={e => setFilterTrainer(e.target.value)}
          style={{ maxWidth: 200 }}>
          <option value="">All Trainers</option>
          {trainers.map(t => (
            <option key={t.trainerId} value={t.trainerId}>{t.name}</option>
          ))}
        </select>
        {(filterProgram || filterTrainer) && (
          <button className="btn-gym-ghost btn-sm"
            onClick={() => { setFilterProgram(''); setFilterTrainer(''); }}>
            Clear filters
          </button>
        )}
      </div>

      {bookingMsg.text && (
        <div className={`mb-3 ${bookingMsg.ok ? 'gym-alert-success' : 'gym-alert-error'}`}>
          {bookingMsg.text}
        </div>
      )}
      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading sessions</div>
      ) : filtered.length === 0 ? (
        <div className="gym-empty">No sessions found.</div>
      ) : (
        <div className="table-responsive">
          <table className="table table-hover align-middle">
            <thead>
              <tr>
                <th>Program</th>
                <th>Trainer</th>
                <th>Start</th>
                <th>End</th>
                <th>Availability</th>
                {isAdmin && <th>Status</th>}
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(s => (
                <tr key={s.sessionId} style={!s.isActive ? { opacity: 0.45 } : {}}>
                  <td className="fw-600">{s.fitnessProgram.name}</td>
                  <td className="text-muted">{s.trainer.name}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>{formatDate(s.startTime)}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>{formatDate(s.endTime)}</td>
                  <td>
                    {s.availableSlots === 0
                      ? <span className="gym-badge gym-badge-full">Full</span>
                      : <span className="gym-badge gym-badge-open">{s.availableSlots} / {s.capacity} open</span>
                    }
                  </td>
                  {isAdmin && (
                    <td>
                      {s.isActive
                        ? <span className="gym-badge gym-badge-active">Active</span>
                        : <span className="gym-badge gym-badge-inactive">Inactive</span>
                      }
                    </td>
                  )}
                  <td>
                    {isAdmin ? (
                      <div className="d-flex gap-2">
                        <button className="btn-gym-outline btn-sm"
                          onClick={() => setModal(s)}>Edit</button>
                        {s.isActive && (
                          <button className="btn-gym-danger btn-sm"
                            onClick={() => handleDelete(s.sessionId)}>Delete</button>
                        )}
                      </div>
                    ) : (
                      s.isActive && s.availableSlots > 0 && new Date(s.startTime) > new Date() ? (
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
                      )
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modal && (
        <SessionModal
          session={modal === 'create' ? null : modal}
          programs={programs}
          trainers={trainers}
          onClose={() => setModal(null)}
          onSave={() => { setModal(null); load(); }}
        />
      )}
    </Layout>
  );
}
