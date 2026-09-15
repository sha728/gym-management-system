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
      const payload = {
        ...form,
        startTime: toUtc(form.startTime),
        endTime: toUtc(form.endTime),
      };
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
          <h5>{session ? 'Edit Session' : 'New Session'}</h5>
          <button className="gym-modal-close" onClick={onClose}>&#x2715;</button>
        </div>
        {error && <div className="gym-alert-error">{error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="row g-3">
            <div className="col-md-6">
              <label className="gym-label">Program</label>
              <select className="gym-input" value={form.fitnessProgramId} onChange={e => setForm({ ...form, fitnessProgramId: e.target.value })} required>
                {programs.map(p => <option key={p.fitnessProgramId} value={p.fitnessProgramId}>{p.name}</option>)}
              </select>
            </div>
            <div className="col-md-6">
              <label className="gym-label">Trainer</label>
              <select className="gym-input" value={form.trainerId} onChange={e => setForm({ ...form, trainerId: e.target.value })} required>
                {trainers.map(t => <option key={t.trainerId} value={t.trainerId}>{t.name}</option>)}
              </select>
            </div>
            <div className="col-md-6">
              <label className="gym-label">Start Time (local)</label>
              <input className="gym-input" type="datetime-local" value={form.startTime} onChange={e => setForm({ ...form, startTime: e.target.value })} required />
            </div>
            <div className="col-md-6">
              <label className="gym-label">End Time (local)</label>
              <input className="gym-input" type="datetime-local" value={form.endTime} onChange={e => setForm({ ...form, endTime: e.target.value })} required />
            </div>
            <div className="col-md-6">
              <label className="gym-label">Capacity</label>
              <input className="gym-input" type="number" min={1} value={form.capacity} onChange={e => setForm({ ...form, capacity: Number(e.target.value) })} required />
            </div>
            {session && (
              <div className="col-md-6 d-flex align-items-end pb-1 gap-2">
                <input type="checkbox" id="sessActive" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />
                <label htmlFor="sessActive" className="gym-label mb-0">Active</label>
              </div>
            )}
          </div>
          <div className="d-flex gap-2 justify-content-end mt-4">
            <button type="button" className="btn btn-gym-outline" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn btn-gym" disabled={saving}>{saving ? 'Saving...' : 'Save'}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default function Sessions() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  const [sessions, setSessions] = useState([]);
  const [programs, setPrograms] = useState([]);
  const [trainers, setTrainers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [filterProgram, setFilterProgram] = useState('');
  const [filterTrainer, setFilterTrainer] = useState('');
  const [modal, setModal] = useState(null);
  const [bookingId, setBookingId] = useState(null);
  const [bookingMsg, setBookingMsg] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [s, p, t] = await Promise.all([
        sessionsApi.getAll({ includeInactive: isAdmin }),
        programsApi.getAll(isAdmin),
        trainersApi.getAll(isAdmin),
      ]);
      setSessions(s);
      setPrograms(p);
      setTrainers(t);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleBook = async (sessionId) => {
    setBookingId(sessionId);
    setBookingMsg('');
    try {
      await bookingsApi.create(sessionId);
      setBookingMsg('Booking confirmed.');
      load();
    } catch (err) {
      setBookingMsg(err.message);
    } finally {
      setBookingId(null);
    }
  };

  const handleDelete = async (id) => {
    if (!confirm('Deactivate this session? All confirmed bookings will be cancelled.')) return;
    try {
      await sessionsApi.delete(id);
      load();
    } catch (err) {
      alert(err.message);
    }
  };

  const filtered = sessions.filter(s => {
    if (filterProgram && s.fitnessProgram.fitnessProgramId !== filterProgram) return false;
    if (filterTrainer && s.trainer.trainerId !== filterTrainer) return false;
    return true;
  });

  return (
    <Layout>
      <div className="d-flex align-items-center justify-content-between mb-4">
        <h1 className="gym-page-title">Sessions</h1>
        {isAdmin && (
          <button className="btn btn-gym" onClick={() => setModal('create')}>
            + New Session
          </button>
        )}
      </div>

      {/* Filters */}
      <div className="row g-2 mb-4">
        <div className="col-auto">
          <select className="gym-input" value={filterProgram} onChange={e => setFilterProgram(e.target.value)} style={{ minWidth: 180 }}>
            <option value="">All Programs</option>
            {programs.map(p => <option key={p.fitnessProgramId} value={p.fitnessProgramId}>{p.name}</option>)}
          </select>
        </div>
        <div className="col-auto">
          <select className="gym-input" value={filterTrainer} onChange={e => setFilterTrainer(e.target.value)} style={{ minWidth: 180 }}>
            <option value="">All Trainers</option>
            {trainers.map(t => <option key={t.trainerId} value={t.trainerId}>{t.name}</option>)}
          </select>
        </div>
        {(filterProgram || filterTrainer) && (
          <div className="col-auto">
            <button className="btn btn-gym-outline btn-sm" onClick={() => { setFilterProgram(''); setFilterTrainer(''); }}>Clear</button>
          </div>
        )}
      </div>

      {bookingMsg && (
        <div className={`mb-3 ${bookingMsg === 'Booking confirmed.' ? 'gym-alert-success' : 'gym-alert-error'}`}>
          {bookingMsg}
        </div>
      )}
      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading...</div>
      ) : filtered.length === 0 ? (
        <p className="text-muted">No sessions found.</p>
      ) : (
        <div className="table-responsive">
          <table className="gym-table">
            <thead>
              <tr>
                <th>Program</th>
                <th>Trainer</th>
                <th>Start</th>
                <th>End</th>
                <th>Slots</th>
                {isAdmin && <th>Status</th>}
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(s => (
                <tr key={s.sessionId} className={!s.isActive ? 'gym-row-inactive' : ''}>
                  <td>{s.fitnessProgram.name}</td>
                  <td>{s.trainer.name}</td>
                  <td>{formatDate(s.startTime)}</td>
                  <td>{formatDate(s.endTime)}</td>
                  <td>
                    <span className={s.availableSlots === 0 ? 'gym-badge-full' : 'gym-badge-open'}>
                      {s.availableSlots} / {s.capacity}
                    </span>
                  </td>
                  {isAdmin && (
                    <td>{s.isActive ? <span className="gym-badge-active">Active</span> : <span className="gym-badge-inactive">Inactive</span>}</td>
                  )}
                  <td>
                    {isAdmin ? (
                      <div className="d-flex gap-2">
                        <button className="btn btn-gym-outline btn-sm" onClick={() => setModal(s)}>Edit</button>
                        {s.isActive && <button className="btn btn-gym-danger btn-sm" onClick={() => handleDelete(s.sessionId)}>Delete</button>}
                      </div>
                    ) : (
                      s.isActive && s.availableSlots > 0 && new Date(s.startTime) > new Date() ? (
                        <button
                          className="btn btn-gym btn-sm"
                          disabled={bookingId === s.sessionId}
                          onClick={() => handleBook(s.sessionId)}
                        >
                          {bookingId === s.sessionId ? '...' : 'Book'}
                        </button>
                      ) : (
                        <span className="text-muted small">
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
