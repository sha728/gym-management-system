import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import { trainersApi } from '../api/client';
import Layout from '../components/Layout';

function TrainerModal({ trainer, onClose, onSave }) {
  const [form, setForm] = useState(
    trainer || { name: '', specialization: '', bio: '', isActive: true }
  );
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSaving(true);
    try {
      if (trainer) {
        await trainersApi.update(trainer.trainerId, form);
      } else {
        await trainersApi.create(form);
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
      <div className="gym-modal">
        <div className="gym-modal-header">
          <span className="gym-modal-title">{trainer ? 'Edit Trainer' : 'New Trainer'}</span>
          <button className="gym-modal-close" onClick={onClose} aria-label="Close">&#x2715;</button>
        </div>

        {error && <div className="gym-alert-error mb-3">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="gym-label">Name</label>
            <input
              className="gym-input"
              value={form.name}
              onChange={e => setForm({ ...form, name: e.target.value })}
              placeholder="e.g. Alex Johnson"
              required
            />
          </div>
          <div className="mb-3">
            <label className="gym-label">Specialization</label>
            <input
              className="gym-input"
              value={form.specialization}
              onChange={e => setForm({ ...form, specialization: e.target.value })}
              placeholder="e.g. Strength & Conditioning"
              required
            />
          </div>
          <div className="mb-3">
            <label className="gym-label">Bio</label>
            <textarea
              className="gym-input"
              rows={3}
              value={form.bio || ''}
              onChange={e => setForm({ ...form, bio: e.target.value })}
              placeholder="Short background about the trainer"
            />
          </div>
          {trainer && (
            <div className="mb-4 d-flex align-items-center gap-2">
              <input
                type="checkbox"
                id="trainerActive"
                className="form-check-input mt-0"
                checked={form.isActive}
                onChange={e => setForm({ ...form, isActive: e.target.checked })}
              />
              <label htmlFor="trainerActive" className="gym-label mb-0">Active</label>
            </div>
          )}
          <div className="d-flex gap-2 justify-content-end pt-2">
            <button type="button" className="btn-gym-ghost btn-sm" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn-gym btn-sm" disabled={saving}>
              {saving ? 'Saving...' : 'Save Trainer'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

// The backend stores availability windows and session times as UTC day-of-week + time-of-day, so a
// window entered in the admin's local time has to be converted before it's compared against sessions.
function localDayTimeToUtc(dayOfWeek, hhmm) {
  const [hours, minutes] = hhmm.split(':').map(Number);
  // Jan 1 2023 was a Sunday, so day (1 + dayOfWeek) lands on the matching weekday in local time.
  const local = new Date(2023, 0, 1 + dayOfWeek, hours, minutes, 0);
  return {
    dayOfWeek: local.getUTCDay(),
    time: `${String(local.getUTCHours()).padStart(2, '0')}:${String(local.getUTCMinutes()).padStart(2, '0')}:00`,
  };
}

function utcDayTimeToLocal(dayOfWeek, hhmmss) {
  const [hours, minutes] = hhmmss.split(':').map(Number);
  const utc = new Date(Date.UTC(2023, 0, 1 + dayOfWeek, hours, minutes, 0));
  return {
    dayName: DAY_NAMES[utc.getDay()],
    time: `${String(utc.getHours()).padStart(2, '0')}:${String(utc.getMinutes()).padStart(2, '0')}`,
  };
}

function AvailabilityModal({ trainer, onClose }) {
  const [windows, setWindows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ dayOfWeek: '1', startTime: '09:00', endTime: '17:00' });

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setWindows(await trainersApi.getAvailability(trainer.trainerId));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadWindows = async () => {
      setLoading(true);
      setError('');
      try {
        setWindows(await trainersApi.getAvailability(trainer.trainerId));
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    loadWindows();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleAdd = async (e) => {
    e.preventDefault();
    setError('');
    setSaving(true);
    try {
      const start = localDayTimeToUtc(Number(form.dayOfWeek), form.startTime);
      const end = localDayTimeToUtc(Number(form.dayOfWeek), form.endTime);

      if (start.dayOfWeek !== end.dayOfWeek) {
        setError('This window crosses a UTC day boundary in your timezone. Pick a start/end time that stays within the same UTC day.');
        return;
      }

      await trainersApi.addAvailability(trainer.trainerId, {
        dayOfWeek: start.dayOfWeek,
        startTime: start.time,
        endTime: end.time,
      });
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id) => {
    if (!confirm('Remove this availability window?')) return;
    try {
      await trainersApi.deleteAvailability(trainer.trainerId, id);
      load();
    } catch (err) {
      alert(err.message);
    }
  };

  return (
    <div className="gym-modal-backdrop">
      <div className="gym-modal">
        <div className="gym-modal-header">
          <span className="gym-modal-title">{trainer.name} — Availability</span>
          <button className="gym-modal-close" onClick={onClose} aria-label="Close">&#x2715;</button>
        </div>

        <p className="gym-page-subtitle mb-3">Times below are shown in your local timezone.</p>

        {error && <div className="gym-alert-error mb-3">{error}</div>}

        {loading ? (
          <div className="gym-loading">Loading availability</div>
        ) : windows.length === 0 ? (
          <div className="gym-empty">No availability windows yet.</div>
        ) : (
          <div className="table-responsive mb-3">
            <table className="table table-hover align-middle">
              <thead>
                <tr>
                  <th>Day</th>
                  <th>Start</th>
                  <th>End</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {windows.map(w => {
                  const start = utcDayTimeToLocal(w.dayOfWeek, w.startTime);
                  const end = utcDayTimeToLocal(w.dayOfWeek, w.endTime);
                  return (
                    <tr key={w.trainerAvailabilityId}>
                      <td>{start.dayName}</td>
                      <td>{start.time}</td>
                      <td>{end.time}</td>
                      <td>
                        <button
                          className="btn-gym-danger btn-sm"
                          onClick={() => handleDelete(w.trainerAvailabilityId)}
                        >
                          Remove
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        <form onSubmit={handleAdd}>
          <div className="row g-2 align-items-end">
            <div className="col-4">
              <label className="gym-label">Day</label>
              <select
                className="gym-input"
                value={form.dayOfWeek}
                onChange={e => setForm({ ...form, dayOfWeek: e.target.value })}
              >
                {DAY_NAMES.map((name, index) => (
                  <option key={name} value={index}>{name}</option>
                ))}
              </select>
            </div>
            <div className="col-3">
              <label className="gym-label">Start</label>
              <input
                type="time"
                className="gym-input"
                value={form.startTime}
                onChange={e => setForm({ ...form, startTime: e.target.value })}
                required
              />
            </div>
            <div className="col-3">
              <label className="gym-label">End</label>
              <input
                type="time"
                className="gym-input"
                value={form.endTime}
                onChange={e => setForm({ ...form, endTime: e.target.value })}
                required
              />
            </div>
            <div className="col-2">
              <button type="submit" className="btn-gym btn-sm w-100" disabled={saving}>
                {saving ? 'Adding...' : 'Add'}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

export default function Trainers() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  const [trainers, setTrainers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [modal, setModal] = useState(null);
  const [availabilityTrainer, setAvailabilityTrainer] = useState(null);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setTrainers(await trainersApi.getAll(isAdmin));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadTrainers = async () => {
      setLoading(true);
      setError('');
      try {
        setTrainers(await trainersApi.getAll(isAdmin));
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    
    loadTrainers();
  }, [isAdmin]);

  const handleDelete = async (id) => {
    if (!confirm('Deactivate this trainer?')) return;
    try { await trainersApi.delete(id); load(); }
    catch (err) { alert(err.message); }
  };

  const filtered = trainers.filter(t =>
    t.name.toLowerCase().includes(search.toLowerCase()) ||
    t.specialization.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <Layout>
      <div className="gym-page-header d-flex align-items-center justify-content-between">
        <div>
          <h1 className="gym-page-title">Trainers</h1>
          {!loading && (
            <p className="gym-page-subtitle">{filtered.length} trainer{filtered.length !== 1 ? 's' : ''}</p>
          )}
        </div>
        {isAdmin && (
          <button className="btn-gym" onClick={() => setModal('create')}>+ New Trainer</button>
        )}
      </div>

      <div className="gym-filter-bar">
        <div className="gym-search">
          <span className="gym-search-icon">&#9906;</span>
          <input
            className="gym-input"
            placeholder="Search by name or specialization..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      {error && <div className="gym-alert-error mb-4">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading trainers</div>
      ) : filtered.length === 0 ? (
        <div className="gym-empty">No trainers found.</div>
      ) : (
        <div className="row g-3">
          {filtered.map(t => (
            <div key={t.trainerId} className="col-12 col-md-6 col-xl-4">
              <div className={`gym-card h-100${!t.isActive ? ' gym-card-inactive' : ''}`}>
                <div className="gym-card-body">
                  <div className="d-flex justify-content-between align-items-start mb-2">
                    <h5 className="gym-card-title">{t.name}</h5>
                    {!t.isActive
                      ? <span className="gym-badge gym-badge-inactive">Inactive</span>
                      : <span className="gym-badge gym-badge-active">Active</span>
                    }
                  </div>
                  <p className="gym-card-meta mb-3">{t.specialization}</p>
                  {t.bio && <p className="gym-card-text">{t.bio}</p>}
                </div>
                {isAdmin && (
                  <div className="gym-card-footer d-flex gap-2">
                    <button
                      className="btn-gym-outline btn-sm flex-fill"
                      onClick={() => setModal(t)}
                    >
                      Edit
                    </button>
                    <button
                      className="btn-gym-outline btn-sm flex-fill"
                      onClick={() => setAvailabilityTrainer(t)}
                    >
                      Availability
                    </button>
                    {t.isActive && (
                      <button
                        className="btn-gym-danger btn-sm flex-fill"
                        onClick={() => handleDelete(t.trainerId)}
                      >
                        Deactivate
                      </button>
                    )}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {modal && (
        <TrainerModal
          trainer={modal === 'create' ? null : modal}
          onClose={() => setModal(null)}
          onSave={() => { setModal(null); load(); }}
        />
      )}

      {availabilityTrainer && (
        <AvailabilityModal
          trainer={availabilityTrainer}
          onClose={() => setAvailabilityTrainer(null)}
        />
      )}
    </Layout>
  );
}
