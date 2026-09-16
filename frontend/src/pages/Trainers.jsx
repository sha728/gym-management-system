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

export default function Trainers() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  const [trainers, setTrainers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [modal, setModal] = useState(null);

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

  useEffect(() => { load(); }, []);

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
    </Layout>
  );
}
