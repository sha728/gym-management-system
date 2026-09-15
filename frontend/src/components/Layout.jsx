import Navbar from './Navbar';

export default function Layout({ children }) {
  return (
    <div className="gym-layout">
      <Navbar />
      <main className="gym-main">
        <div className="container py-4">
          {children}
        </div>
      </main>
      <footer className="gym-footer">
        <div className="container">
          <span className="text-muted small">APEX GYM &copy; {new Date().getFullYear()}</span>
        </div>
      </footer>
    </div>
  );
}
