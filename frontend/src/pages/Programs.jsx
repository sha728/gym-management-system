import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { programsApi } from '../api/client';
import Layout from '../components/Layout';

function ProgramModal({ program, onClose, onSave }) {
  const [form, setForm] = useState(
    program || { name: '', description: '', durationInMinutes: 60, isActive: true }
  );
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSaving(true);
    try {
      if (program) {
        await programsApi.update(program.fitnessProgramId, form);
      } else {
        await programsApi.create(form);
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
          <span className="gym-modal-title">{program ? 'Edit Program' : 'New Program'}</span>
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
              placeholder="e.g. Cardio Blast"
              required
            />
          </div>
          <div className="mb-3">
            <label className="gym-label">Description</label>
            <textarea
              className="gym-input"
              rows={3}
              value={form.description}
              onChange={e => setForm({ ...form, description: e.target.value })}
              placeholder="Brief description of the program"
            />
          </div>
          <div className="mb-3">
            <label className="gym-label">Duration (minutes)</label>
            <input
              className="gym-input"
              type="number"
              min={1}
              value={form.durationInMinutes}
              onChange={e => setForm({ ...form, durationInMinutes: Number(e.target.value) })}
              required
            />
          </div>
          {program && (
            <div className="mb-4 d-flex align-items-center gap-2">
              <input
                type="checkbox"
                id="progActive"
                className="form-check-input mt-0"
                checked={form.isActive}
                onChange={e => setForm({ ...form, isActive: e.target.checked })}
              />
              <label htmlFor="progActive" className="gym-label mb-0">Active</label>
            </div>
          )}
          <div className="d-flex gap-2 justify-content-end pt-2">
            <button type="button" className="btn-gym-ghost btn-sm" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn-gym btn-sm" disabled={saving}>
              {saving ? 'Saving...' : 'Save Program'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default function Programs() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  const [programs, setPrograms] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [modal, setModal] = useState(null);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      setPrograms(await programsApi.getAll(isAdmin));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadPrograms = async () => {
      setLoading(true);
      setError('');
      try {
        setPrograms(await programsApi.getAll(isAdmin));
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    
    loadPrograms();
  }, [isAdmin]);

  const handleDelete = async (id) => {
    if (!confirm('Deactivate this program?')) return;
    try { await programsApi.delete(id); load(); }
    catch (err) { alert(err.message); }
  };

  const filtered = programs.filter(p =>
    p.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <Layout>
      <div className="gym-page-header d-flex align-items-center justify-content-between">
        <div>
          <h1 className="gym-page-title">Fitness Programs</h1>
          {!loading && (
            <p className="gym-page-subtitle">{filtered.length} program{filtered.length !== 1 ? 's' : ''}</p>
          )}
        </div>
        {isAdmin && (
          <button className="btn-gym" onClick={() => setModal('create')}>+ New Program</button>
        )}
      </div>

      <div className="gym-filter-bar">
        <div className="gym-search">
          <span className="gym-search-icon">&#9906;</span>
          <input
            className="gym-input"
            placeholder="Search programs..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      {error && <div className="gym-alert-error mb-4">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading programs</div>
      ) : filtered.length === 0 ? (
        <div className="gym-empty">No programs found.</div>
      ) : (
        <div className="row g-3">
          {filtered.map(p => (
            <div key={p.fitnessProgramId} className="col-12 col-md-6 col-xl-4">
              <div className={`gym-card h-100${!p.isActive ? ' gym-card-inactive' : ''}`}>
                <div className="gym-card-body">
                  <div className="d-flex justify-content-between align-items-start mb-3">
                    <h5 className="gym-card-title">{p.name}</h5>
                    {!p.isActive
                      ? <span className="gym-badge gym-badge-inactive">Inactive</span>
                      : <span className="gym-badge gym-badge-active">Active</span>
                    }
                  </div>
                  <p className="gym-card-text mb-3">
                    {p.description || 'No description provided.'}
                  </p>
                  <span className="gym-card-meta">{p.durationInMinutes} min</span>
                </div>
                <div className="gym-card-footer d-flex gap-2">
                  <Link
                    to={`/programs/${p.fitnessProgramId}`}
                    className="btn-gym-outline btn-sm flex-fill"
                  >
                    View Details
                  </Link>
                  {isAdmin && (
                    <>
                      <button
                        className="btn-gym-outline btn-sm flex-fill"
                        onClick={() => setModal(p)}
                      >
                        Edit
                      </button>
                      {p.isActive && (
                        <button
                          className="btn-gym-danger btn-sm flex-fill"
                          onClick={() => handleDelete(p.fitnessProgramId)}
                        >
                          Deactivate
                        </button>
                      )}
                    </>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {modal && (
        <ProgramModal
          program={modal === 'create' ? null : modal}
          onClose={() => setModal(null)}
          onSave={() => { setModal(null); load(); }}
        />
      )}
    </Layout>
  );
}
