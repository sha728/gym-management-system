import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { authApi } from '../api/client';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const data = await authApi.login({ email, password });
      login(data.token);
      const payload = JSON.parse(atob(data.token.split('.')[1]));
      const role =
        payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
        payload.role || 'Member';
      navigate(role === 'Admin' ? '/admin/dashboard' : '/programs', { replace: true });
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-shell">
      <div className="auth-left">
        <div>
          <span className="auth-left-brand">APEX GYM</span>
        </div>
        <div>
          <p className="auth-left-tagline">Train hard.<br />Track everything.</p>
          <p className="auth-left-sub">
            Manage your sessions, track your bookings and stay on top of your membership — all in one place.
          </p>
        </div>
        <div className="auth-left-footer">APEX GYM &copy; {new Date().getFullYear()}</div>
      </div>

      <div className="auth-right">
        <div className="auth-form-wrap">
          <h1 className="auth-form-title">Sign in</h1>
          <p className="auth-form-sub">Enter your credentials to access your account.</p>

          {error && <div className="auth-error">{error}</div>}

          <form onSubmit={handleSubmit}>
            <div className="auth-form-group">
              <label htmlFor="email">Email Address</label>
              <input
                type="email"
                id="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="you@example.com"
                autoComplete="email"
                required
              />
            </div>
            <div className="auth-form-group">
              <label htmlFor="password">Password</label>
              <input
                type="password"
                id="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                placeholder="••••••••"
                autoComplete="current-password"
                required
              />
            </div>

            <button type="submit" className="btn btn-full mt-3" disabled={loading}>
              {loading ? 'Signing in...' : 'Sign In'}
            </button>
          </form>

          <div className="auth-footer-link">
            Not a member yet? <Link to="/register">Create an account</Link>
          </div>
        </div>
      </div>
    </div>
  );
}
