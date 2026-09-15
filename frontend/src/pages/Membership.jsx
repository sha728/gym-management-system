import { useState, useEffect } from 'react';
import { plansApi, membershipsApi } from '../api/client';
import Layout from '../components/Layout';

function formatDate(iso) {
  if (!iso) return '--';
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
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

export default function Membership() {
  const [memberships, setMemberships] = useState([]);
  const [plans, setPlans] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [requesting, setRequesting] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState('');
  const [requestMsg, setRequestMsg] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [m, p] = await Promise.all([
        membershipsApi.my(),
        plansApi.getAll(),
      ]);
      setMemberships(m);
      setPlans(p);
      if (p.length > 0) setSelectedPlan(p[0].membershipPlanId);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const hasActive = memberships.some(m => m.status === 'Active');
  const hasPending = memberships.some(m => m.status === 'Pending');
  const canRequest = !hasActive && !hasPending;

  const handleRequest = async (e) => {
    e.preventDefault();
    setRequestMsg('');
    setRequesting(true);
    try {
      await membershipsApi.request(selectedPlan);
      setRequestMsg('Membership request submitted. Pending admin review.');
      load();
    } catch (err) {
      setRequestMsg(err.message);
    } finally {
      setRequesting(false);
    }
  };

  const activeMembership = memberships.find(m => m.status === 'Active');

  return (
    <Layout>
      <div className="mb-4">
        <h1 className="gym-page-title">Membership</h1>
      </div>

      {error && <div className="gym-alert-error mb-3">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading...</div>
      ) : (
        <>
          {/* Active membership summary */}
          {activeMembership && (
            <div className="gym-card mb-4" style={{ borderLeft: '3px solid var(--accent-gold)' }}>
              <div className="gym-card-body">
                <div className="d-flex justify-content-between align-items-start">
                  <div>
                    <h5 className="gym-card-title mb-1">{activeMembership.planName}</h5>
                    <p className="gym-card-meta mb-0">
                      Active until {formatDate(activeMembership.endDate)}
                    </p>
                  </div>
                  {statusBadge('Active')}
                </div>
                <div className="row mt-3 g-2">
                  <div className="col-auto">
                    <span className="gym-stat-label">Started</span>
                    <span className="gym-stat-value">{formatDate(activeMembership.startDate)}</span>
                  </div>
                  <div className="col-auto">
                    <span className="gym-stat-label">Duration</span>
                    <span className="gym-stat-value">{activeMembership.durationInDays} days</span>
                  </div>
                  <div className="col-auto">
                    <span className="gym-stat-label">Price</span>
                    <span className="gym-stat-value">${activeMembership.price}</span>
                  </div>
                </div>
                {activeMembership.benefits && (
                  <p className="gym-card-text mt-2">{activeMembership.benefits}</p>
                )}
              </div>
            </div>
          )}

          {/* Request form */}
          {canRequest && plans.length > 0 && (
            <div className="gym-card mb-4">
              <div className="gym-card-body">
                <h5 className="gym-card-title mb-3">Request a Membership</h5>
                {requestMsg && (
                  <div className={`mb-3 ${requestMsg.includes('submitted') ? 'gym-alert-success' : 'gym-alert-error'}`}>
                    {requestMsg}
                  </div>
                )}
                <form onSubmit={handleRequest}>
                  <div className="row g-3 align-items-end">
                    <div className="col-md-6">
                      <label className="gym-label">Select Plan</label>
                      <select className="gym-input" value={selectedPlan} onChange={e => setSelectedPlan(e.target.value)}>
                        {plans.map(p => (
                          <option key={p.membershipPlanId} value={p.membershipPlanId}>
                            {p.name} — ${p.price} / {p.durationInDays} days
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="col-md-auto">
                      <button type="submit" className="btn btn-gym" disabled={requesting}>
                        {requesting ? 'Submitting...' : 'Submit Request'}
                      </button>
                    </div>
                  </div>
                </form>

                {/* Plan details */}
                {selectedPlan && (() => {
                  const p = plans.find(x => x.membershipPlanId === selectedPlan);
                  return p ? (
                    <div className="mt-3 pt-3" style={{ borderTop: '1px solid var(--border-color)' }}>
                      <p className="gym-card-text">{p.benefits}</p>
                    </div>
                  ) : null;
                })()}
              </div>
            </div>
          )}

          {hasPending && !hasActive && (
            <div className="gym-alert-info mb-4">
              Your membership request is pending admin review.
            </div>
          )}

          {/* History */}
          {memberships.length > 0 && (
            <>
              <h5 className="gym-section-title mb-3">History</h5>
              <div className="table-responsive">
                <table className="gym-table">
                  <thead>
                    <tr>
                      <th>Plan</th>
                      <th>Requested</th>
                      <th>Start</th>
                      <th>End</th>
                      <th>Status</th>
                      <th>Note</th>
                    </tr>
                  </thead>
                  <tbody>
                    {memberships.map(m => (
                      <tr key={m.membershipId}>
                        <td>{m.planName}</td>
                        <td>{formatDate(m.requestedAt)}</td>
                        <td>{formatDate(m.startDate)}</td>
                        <td>{formatDate(m.endDate)}</td>
                        <td>{statusBadge(m.status)}</td>
                        <td className="text-muted small">{m.reviewNote || '--'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
    </Layout>
  );
}
