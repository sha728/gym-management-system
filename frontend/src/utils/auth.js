export function parseToken(token) {
  try {
    // Fix base64url decoding - JWT uses base64url, not standard base64
    let part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    while (part.length % 4) part += '=';
    
    const payload = JSON.parse(atob(part));
    
    // Check if token is expired
    if (payload.exp && payload.exp * 1000 <= Date.now()) {
      localStorage.removeItem('token');
      return null;
    }
    
    return {
      userId: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || payload.sub,
      email: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || payload.email,
      role: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role,
    };
  } catch {
    localStorage.removeItem('token');
    return null;
  }
}