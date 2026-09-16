import { useState, useEffect } from 'react';
import { plansApi, membershipsApi } from '../api/client';
import Layout from '../components/Layout';

function formatDate(iso) {
  if (!iso) return '--';
  return new Date(iso).toLocaleDateString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

function StatusBadge({ status }) {
  const map = {
    Active:   'gym-badge-active',
    Pending:  'gym-badge-pending',
    Expired:  'gym-badge-inactive',
    Rejected: 'gym-badge-full',
  };
  return <span className={`gym-badge ${map[status] || 'gym-badge-inactive'}`}>{status}</span>;
}

export default function Membership() {
  const [memberships, setMemberships] = useState([]);
  const [plans, setPlans]             = useState([]);
  const [loading, setLoading]         = useState(true);
  const [error, setError]             = useState('');
  const [requesting, setRequesting]   = useState(false);
  const [selectedPlan, setSelectedPlan] = useState('');
  const [requestMsg, setRequestMsg]   = useState({ text: '', ok: false });

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [m, p] = await Promise.all([membershipsApi.my(), plansApi.getAll()]);
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

  const hasActive  = memberships.some(m => m.status === 'Active');
  const hasPending = memberships.some(m => m.status === 'Pending');
  const canRequest = !hasActive && !hasPending;

  const handleRequest = async (e) => {
    e.preventDefault();
    setRequestMsg({ text: '', ok: false });
    setRequesting(true);
    try {
      await membershipsApi.request(selectedPlan);
      setRequestMsg({ text: 'Request submitted. Pending admin review.', ok: true });
      load();
    } catch (err) {
      setRequestMsg({ text: err.message, ok: false });
    } finally {
      setRequesting(false);
    }
  };

  const activeMembership  = memberships.find(m => m.status === 'Active');
  const selectedPlanData  = plans.find(p => p.membershipPlanId === selectedPlan);

  return (
    <Layout>
      <div className="gym-page-header">
        <h1 className="gym-page-title">Membership</h1>
        <p className="gym-page-subtitle">
          {hasActive ? 'Your membership is active.' : hasPending ? 'Your request is under review.' : 'No active membership.'}
        </p>
      </div>

      {error && <div className="gym-alert-error mb-4">{error}</div>}

      {loading ? (
        <div className="gym-loading">Loading membership</div>
      ) : (
        <div className="row g-4">
          <div className="col-lg-7">

            {activeMembership && (
              <div className="gym-card gym-card-gold mb-4">
                <div className="gym-card-body">
                  <div className="d-flex justify-content-between align-items-start mb-3">
                    <div>
                      <p className="gym-card-meta mb-1">Active Membership</p>
                      <h4 className="gym-card-title" style={{ fontSize: '1.15rem' }}>
                        {activeMembership.planName}
                      </h4>
                    </div>
                    <StatusBadge status="Active" />
                  </div>

                  <div className="gym-detail-grid">
                    <div className="gym-detail-item">
                      <span className="gym-detail-label">Started</span>
                      <span className="gym-detail-value">{formatDate(activeMembership.startDate)}</span>
                    </div>
                    <div className="gym-detail-item">
                      <span className="gym-detail-label">Expires</span>
                      <span className="gym-detail-value">{formatDate(activeMembership.endDate)}</span>
                    </div>
                    <div className="gym-detail-item">
                      <span className="gym-detail-label">Duration</span>
                      <span className="gym-detail-value">{activeMembership.durationInDays} days</span>
                    </div>
                    <div className="gym-detail-item">
                      <span className="gym-detail-label">Price</span>
                      <span className="gym-detail-value">${activeMembership.price}</span>
                    </div>
                  </div>

                  {activeMembership.benefits && (
                    <p className="gym-card-text mt-3">{activeMembership.benefits}</p>
                  )}
                </div>
              </div>
            )}

            {hasPending && !hasActive && (
              <div className="gym-alert-info mb-4">
                Your membership request is pending admin review. You will be notified once it is processed.
              </div>
            )}

            {canRequest && plans.length > 0 && (
              <div className="gym-card">
                <div className="gym-card-body">
                  <h5 className="gym-card-title mb-1">Request a Membership</h5>
                  <p className="gym-card-text mb-4">Select a plan and submit your request for admin approval.</p>

                  {requestMsg.text && (
                    <div className={`mb-3 ${requestMsg.ok ? 'gym-alert-success' : 'gym-alert-error'}`}>
                      {requestMsg.text}
                    </div>
                  )}

                  <form onSubmit={handleRequest}>
                    <div className="mb-3">
                      <label className="gym-label">Choose Plan</label>
                      <select
                        className="gym-input"
                        value={selectedPlan}
                        onChange={e => setSelectedPlan(e.target.value)}
                      >
                        {plans.map(p => (
                          <option key={p.membershipPlanId} value={p.membershipPlanId}>
                            {p.name} — ${p.price} / {p.durationInDays} days
                          </option>
                        ))}
                      </select>
                    </div>

                    {selectedPlanData && (
                      <div className="p-3 mb-3" style={{
                        background: 'rgba(197,160,89,0.05)',
                        border: '1px solid rgba(197,160,89,0.15)',
                        borderRadius: 'var(--radius)',
                      }}>
                        <p className="gym-card-text mb-0">{selectedPlanData.benefits}</p>
                      </div>
                    )}

                    <button type="submit" className="btn-gym" disabled={requesting}>
                      {requesting ? 'Submitting...' : 'Submit Request'}
                    </button>
                  </form>
                </div>
              </div>
            )}
          </div>

          <div className="col-lg-5">
            {memberships.length > 0 && (
              <>
                <p className="gym-section-label">History</p>
                <div className="d-flex flex-column gap-2">
                  {memberships.map(m => (
                    <div
                      key={m.membershipId}
                      className="gym-card"
                      style={m.status !== 'Active' ? { opacity: 0.7 } : {}}
                    >
                      <div className="gym-card-body" style={{ padding: '1rem 1.25rem' }}>
                        <div className="d-flex justify-content-between align-items-center mb-1">
                          <span className="gym-card-title" style={{ fontSize: '0.875rem' }}>
                            {m.planName}
                          </span>
                          <StatusBadge status={m.status} />
                        </div>
                        <div className="d-flex gap-3 mt-2">
                          <div>
                            <span className="gym-detail-label">Requested</span>
                            <span className="gym-detail-value" style={{ fontSize: '0.78rem' }}>
                              {formatDate(m.requestedAt)}
                            </span>
                          </div>
                          {m.startDate && (
                            <div>
                              <span className="gym-detail-label">Start</span>
                              <span className="gym-detail-value" style={{ fontSize: '0.78rem' }}>
                                {formatDate(m.startDate)}
                              </span>
                            </div>
                          )}
                          {m.endDate && (
                            <div>
                              <span className="gym-detail-label">End</span>
                              <span className="gym-detail-value" style={{ fontSize: '0.78rem' }}>
                                {formatDate(m.endDate)}
                              </span>
                            </div>
                          )}
                        </div>
                        {m.reviewNote && (
                          <p className="gym-card-text mt-2 mb-0" style={{ fontSize: '0.78rem' }}>
                            {m.reviewNote}
                          </p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </Layout>
  );
}
