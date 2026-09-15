import { useState, useEffect } from 'react';
import { membershipsApi, plansApi } from '../../api/client';
import Layout from '../../components/Layout';

function formatDate(iso) {
  if (!iso) return '--';
  return new Date(iso).toLocaleDateString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

function statusBadge(status) {
  const map = {
    Active: 'gym-badge-active',
    Pending: 'gym-badge-pending',
    Expired: 'gym-badge-inactive',
    Rejected: 'gym-badge-full',
  };
  return <span className={map[status] || 'gym-badge-inactive'}>{status}</span>;
}

function ReviewModal({ membership, onClose, onSave }) {
  const [approve, setApprove] = useState(true);
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await membershipsApi.review(membership.membershipId, { approve, reviewNote: note });
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
          <h5>Review Membership Request</h5>
          <button className="gym-modal-close" onClick={onClose}>&#x2715;</button>
        </div>
        <div className="mb-3">
          <p className="text-muted mb-1">Member: <strong className="text-white">{membership.memberEmail}</strong></p>
          <p className="text-muted mb-0">Plan: <strong className="text-white">{membership.planName}</strong></p>
        </div>
        {error && <div className="gym-alert-error mb-3">{error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="gym-label">Decision</label>
            <div className="d-flex gap-3">
              <label className="d-flex align-items-center gap-2" style={{ cursor: 'pointer' }}>
                <input type="radio" name="decision" checked={approve} onChange={() => setApprove(true)} />
                <span>Approve</span>
              </label>
              <label className="d-flex align-items-center gap-2" style={{ cursor: 'pointer' }}>
                <input type="radio" name="decision" checked={!approve} onChange={() => setApprove(false)} />
                <span>Reject</span>
              </label>
            </div>
          </div>
          <div className="mb-3">
            <label className="gym-label">Note (optional)</label>
            <textarea className="gym-input" rows={2} value={note} onChange={e => setNote(e.target.value)} />
          </div>
          <div className="d-flex gap-2 justify-content-end">
            <button type="button" className="btn btn-gym-outline" onClick={onClose}>Cancel</button>
            <button
              type="submit"
              className={`btn ${approve ? 'btn-gym' : 'btn-gym-danger'}`}
              disabled={saving}
            >
              {saving ? 'Saving...' : approve ? 'Approve' : 'Reject'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function PlanModal({ plan, onClose, onSave }) {
  const [form, setForm] = useState(
    plan || { name: '', price: '', durationInDays: 30, benefits: '', isActive: true }
  );
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSaving(true);
    try {
      const payload = { ...form, price: Number(form.price), durationInDays: Number(form.durationInDays) };
      if (plan) {
        await plansApi.update(plan.membershipPlanId, payload);
      } else {
        await plansApi.create(payload);
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
          <h5>{plan ? 'Edit Plan' : 'New Plan'}</h5>
          <button className="gym-modal-close" onClick={onClose}>&#x2715;</button>
        </div>
        {error && <div className="gym-alert-error">{error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="gym-label">Plan Name</label>
            <input className="gym-input" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} required />
          </div>
          <div className="row g-3 mb-3">
            <div className="col-6">
              <label className="gym-label">Price ($)</label>
              <input className="gym-input" type="number" min={0} step="0.01" value={form.price} onChange={e => setForm({ ...form, price: e.target.value })} required />
            </div>
            <div className="col-6">
              <label className="gym-label">Duration (days)</label>
              <input className="gym-input" type="number" min={1} value={form.durationInDays} onChange={e => setForm({ ...form, durationInDays: e.target.value })} required />
            </div>
          </div>
          <div className="mb-3">
            <label className="gym-label">Benefits</label>
            <textarea className="gym-input" rows={2} value={form.benefits} onChange={e => setForm({ ...form, benefits: e.target.value })} required />
          </div>
          {plan && (
            <div className="mb-3 d-flex align-items-center gap-2">
              <input type="checkbox" id="planActive" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />
              <label htmlFor="planActive" className="gym-label mb-0">Active</label>
            </div>
          )}
          <div className="d-flex gap-2 justify-content-end mt-3">
            <button type="button" className="btn btn-gym-outline" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn btn-gym" disabled={saving}>{saving ? 'Saving...' : 'Save'}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

const PAGE_SIZE = 20;

export default function AdminMemberships() {
  const [memberships, setMemberships] = useState([]);
  const [plans, setPlans] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('Pending');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reviewModal, setReviewModal] = useState(null);
  const [planModal, setPlanModal] = useState(null);
  const [tab, setTab] = useState('requests'); // requests | plans

  const loadMemberships = async (p = 1, status = statusFilter) => {
    setLoading(true);
    setError('');
    try {
      const data = await membershipsApi.getAll({ page: p, pageSize: PAGE_SIZE, status: status || undefined });
      setMemberships(data.items);
      setTotalCount(data.totalCount);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const loadPlans = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await plansApi.getAll(true);
      setPlans(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (tab === 'requests') {
      setPage(1);
      loadMemberships(1, statusFilter);
    } else {
      loadPlans();
    }
  }, [tab, statusFilter]);

  useEffect(() => {
    if (tab === 'requests') loadMemberships(page);
  }, [page]);

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  return (
    <Layout>
      <div className="mb-4">
        <h1 className="gym-page-title">Memberships</h1>
      </div>

      {/* Tabs */}
      <div className="d-flex gap-2 mb-4">
        <button className={`btn btn-sm ${tab === 'requests' ? 'btn-gym' : 'btn-gym-outline'}`} onClick={() => setTab('requests')}>
          Requests
        </button>
        <button className={`btn btn-sm ${tab === 'plans' ? 'btn-gym' : 'btn-gym-outline'}`} onClick={() => setTab('plans')}>
          Plans
        </button>
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {tab === 'requests' && (
        <>
          <div className="d-flex gap-2 mb-4 align-items-center">
            <select
              className="gym-input"
              style={{ maxWidth: 180 }}
              value={statusFilter}
              onChange={e => setStatusFilter(e.target.value)}
            >
              <option value="">All</option>
              <option value="Pending">Pending</option>
              <option value="Active">Active</option>
              <option value="Expired">Expired</option>
              <option value="Rejected">Rejected</option>
            </select>
            <span className="text-muted small">{totalCount} result{totalCount !== 1 ? 's' : ''}</span>
          </div>

          {loading ? (
            <div className="gym-loading">Loading...</div>
          ) : memberships.length === 0 ? (
            <p className="text-muted">No membership requests found.</p>
          ) : (
            <>
              <div className="table-responsive">
                <table className="gym-table">
                  <thead>
                    <tr>
                      <th>Member</th>
                      <th>Plan</th>
                      <th>Requested</th>
                      <th>Start</th>
                      <th>End</th>
                      <th>Status</th>
                      <th>Note</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {memberships.map(m => (
                      <tr key={m.membershipId}>
                        <td>{m.memberEmail}</td>
                        <td>{m.planName}</td>
                        <td>{formatDate(m.requestedAt)}</td>
                        <td>{formatDate(m.startDate)}</td>
                        <td>{formatDate(m.endDate)}</td>
                        <td>{statusBadge(m.status)}</td>
                        <td className="text-muted small">{m.reviewNote || '--'}</td>
                        <td>
                          {m.status === 'Pending' && (
                            <button
                              className="btn btn-gym-outline btn-sm"
                              onClick={() => setReviewModal(m)}
                            >
                              Review
                            </button>
                          )}
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
        </>
      )}

      {tab === 'plans' && (
        <>
          <div className="d-flex justify-content-end mb-3">
            <button className="btn btn-gym" onClick={() => setPlanModal('create')}>+ New Plan</button>
          </div>

          {loading ? (
            <div className="gym-loading">Loading...</div>
          ) : plans.length === 0 ? (
            <p className="text-muted">No plans found.</p>
          ) : (
            <div className="row g-3">
              {plans.map(p => (
                <div key={p.membershipPlanId} className="col-12 col-md-6 col-lg-4">
                  <div className={`gym-card h-100${!p.isActive ? ' gym-card-inactive' : ''}`}>
                    <div className="gym-card-body">
                      <div className="d-flex justify-content-between align-items-start mb-2">
                        <h5 className="gym-card-title">{p.name}</h5>
                        {!p.isActive && <span className="gym-badge-inactive">Inactive</span>}
                      </div>
                      <p className="gym-card-meta mb-1">${p.price} / {p.durationInDays} days</p>
                      <p className="gym-card-text">{p.benefits}</p>
                    </div>
                    <div className="gym-card-footer d-flex gap-2">
                      <button className="btn btn-gym-outline btn-sm flex-fill" onClick={() => setPlanModal(p)}>Edit</button>
                      {p.isActive && (
                        <button
                          className="btn btn-gym-danger btn-sm flex-fill"
                          onClick={async () => {
                            if (!confirm('Deactivate this plan?')) return;
                            try { await plansApi.delete(p.membershipPlanId); loadPlans(); }
                            catch (err) { alert(err.message); }
                          }}
                        >
                          Deactivate
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {reviewModal && (
        <ReviewModal
          membership={reviewModal}
          onClose={() => setReviewModal(null)}
          onSave={() => { setReviewModal(null); loadMemberships(page); }}
        />
      )}

      {planModal && (
        <PlanModal
          plan={planModal === 'create' ? null : planModal}
          onClose={() => setPlanModal(null)}
          onSave={() => { setPlanModal(null); loadPlans(); }}
        />
      )}
    </Layout>
  );
}
