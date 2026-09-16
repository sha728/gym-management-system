const BASE = '/api';

/**
 * Thin wrapper around fetch that:
 * - Attaches the JWT from localStorage automatically
 * - Throws a structured error with the API's message on non-2xx responses
 * - Returns parsed JSON or null for 204 No Content
 */
async function request(path, options = {}) {
  const token = localStorage.getItem('token');

  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.headers || {}),
  };

  const res = await fetch(`${BASE}${path}`, { ...options, headers });

  if (res.status === 204) return null;

  const text = await res.text();
  let body;
  try {
    body = JSON.parse(text);
  } catch {
    body = text;
  }

  if (!res.ok) {
    const message =
      typeof body === 'string'
        ? body
        : body?.message || `Request failed (${res.status})`;
    const err = new Error(message);
    err.status = res.status;
    throw err;
  }

  return body;
}

// Convenience methods
export const api = {
  get: (path) => request(path),
  post: (path, data) => request(path, { method: 'POST', body: JSON.stringify(data) }),
  put: (path, data) => request(path, { method: 'PUT', body: JSON.stringify(data) }),
  delete: (path) => request(path, { method: 'DELETE' }),
};

// ---- Auth ----
export const authApi = {
  register: (data) => api.post('/auth/register', data),
  login: (data) => api.post('/auth/login', data),
  me: () => api.get('/auth/me'),
};

// ---- Programs ----
export const programsApi = {
  getAll: (includeInactive = false) =>
    api.get(`/fitnessprograms${includeInactive ? '?includeInactive=true' : ''}`),
  getById: (id) => api.get(`/fitnessprograms/${id}`),
  create: (data) => api.post('/fitnessprograms', data),
  update: (id, data) => api.put(`/fitnessprograms/${id}`, data),
  delete: (id) => api.delete(`/fitnessprograms/${id}`),
};

export const trainersApi = {
  getAll: (showAll = false) =>
    api.get(`/trainers${showAll ? '?showAll=true' : ''}`),
  getById: (id) => api.get(`/trainers/${id}`),
  create: (data) => api.post('/trainers', data),
  update: (id, data) => api.put(`/trainers/${id}`, data),
  delete: (id) => api.delete(`/trainers/${id}`),
  getAvailability: (id) => api.get(`/traineravailability/${id}`),
  addAvailability: (id, data) => api.post(`/traineravailability/${id}`, data),
  deleteAvailability: (trainerId, availId) =>
    api.delete(`/traineravailability/${trainerId}/${availId}`),
};

// ---- Sessions ----
export const sessionsApi = {
  getAll: (params = {}) => {
    const q = new URLSearchParams();
    if (params.programId) q.set('programId', params.programId);
    if (params.trainerId) q.set('trainerId', params.trainerId);
    if (params.includeInactive) q.set('includeInactive', 'true');
    const qs = q.toString();
    return api.get(`/sessions${qs ? '?' + qs : ''}`);
  },
  getById: (id) => api.get(`/sessions/${id}`),
  create: (data) => api.post('/sessions', data),
  update: (id, data) => api.put(`/sessions/${id}`, data),
  delete: (id) => api.delete(`/sessions/${id}`),
};

// ---- Bookings ----
export const bookingsApi = {
  create: (sessionId) => api.post('/bookings', { sessionId }),
  myBookings: () => api.get('/bookings/my-bookings'),
  cancel: (id) => api.delete(`/bookings/${id}`),
  // Admin
  getAll: (params = {}) => {
    const q = new URLSearchParams();
    if (params.status) q.set('status', params.status);
    if (params.page) q.set('page', params.page);
    if (params.pageSize) q.set('pageSize', params.pageSize);
    return api.get(`/bookings?${q.toString()}`);
  },
};

// ---- Membership Plans ----
export const plansApi = {
  getAll: (includeInactive = false) =>
    api.get(`/membershipplans${includeInactive ? '?includeInactive=true' : ''}`),
  getById: (id) => api.get(`/membershipplans/${id}`),
  create: (data) => api.post('/membershipplans', data),
  update: (id, data) => api.put(`/membershipplans/${id}`, data),
  delete: (id) => api.delete(`/membershipplans/${id}`),
};

// ---- Memberships ----
export const membershipsApi = {
  request: (membershipPlanId) => api.post('/memberships/requests', { membershipPlanId }),
  my: () => api.get('/memberships/my'),
  // Admin
  getAll: (params = {}) => {
    const q = new URLSearchParams();
    if (params.status) q.set('status', params.status);
    if (params.page) q.set('page', params.page);
    if (params.pageSize) q.set('pageSize', params.pageSize);
    return api.get(`/memberships?${q.toString()}`);
  },
  review: (id, data) => api.put(`/memberships/${id}/review`, data),
};

// ---- Users (Admin) ----
export const usersApi = {
  getMembers: (params = {}) => {
    const q = new URLSearchParams();
    if (params.page) q.set('page', params.page);
    if (params.pageSize) q.set('pageSize', params.pageSize);
    return api.get(`/users?${q.toString()}`);
  },
};
