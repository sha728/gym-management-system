import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { authApi } from '../api/client';

export default function Register() {
  const [formData, setFormData] = useState({ name: '', email: '', password: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const handleChange = (e) => setFormData({ ...formData, [e.target.id]: e.target.value });

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await authApi.register(formData);
      navigate('/login');
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
          <p className="auth-left-tagline">Your fitness.<br />Your schedule.</p>
          <p className="auth-left-sub">
            Join Apex Gym and get access to classes, trainers and everything you need to stay on track.
          </p>
        </div>
        <div className="auth-left-footer">APEX GYM &copy; {new Date().getFullYear()}</div>
      </div>

      <div className="auth-right">
        <div className="auth-form-wrap">
          <h1 className="auth-form-title">Create account</h1>
          <p className="auth-form-sub">Fill in your details to get started.</p>

          {error && <div className="auth-error">{error}</div>}

          <form onSubmit={handleSubmit}>
            <div className="auth-form-group">
              <label htmlFor="name">Full Name</label>
              <input
                type="text"
                id="name"
                value={formData.name}
                onChange={handleChange}
                placeholder="Jane Smith"
                autoComplete="name"
                required
              />
            </div>
            <div className="auth-form-group">
              <label htmlFor="email">Email Address</label>
              <input
                type="email"
                id="email"
                value={formData.email}
                onChange={handleChange}
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
                value={formData.password}
                onChange={handleChange}
                placeholder="••••••••"
                autoComplete="new-password"
                required
              />
            </div>

            <button type="submit" className="btn btn-full mt-3" disabled={loading}>
              {loading ? 'Creating account...' : 'Create Account'}
            </button>
          </form>

          <div className="auth-footer-link">
            Already have an account? <Link to="/login">Sign in</Link>
          </div>
        </div>
      </div>
    </div>
  );
}
