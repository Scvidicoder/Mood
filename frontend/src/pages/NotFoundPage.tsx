import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <section className="page">
      <div className="page-heading">
        <p className="eyebrow">404</p>
        <h1>Page not found</h1>
        <p>The requested route does not exist in the foundation application.</p>
        <Link className="not-found-link" to="/">
          Return home
        </Link>
      </div>
    </section>
  );
}
