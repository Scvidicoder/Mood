import { Link } from "react-router-dom";

export function ForbiddenPage() {
  return (
    <section className="page state-page">
      <p className="eyebrow">Access denied</p>
      <h1>That area is not available to this account</h1>
      <p>
        Your session is valid, but it does not include the permission required
        for this page.
      </p>
      <Link className="button button-link" to="/">
        Return home
      </Link>
    </section>
  );
}
