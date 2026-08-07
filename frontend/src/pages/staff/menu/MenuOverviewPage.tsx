import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { getAuditLog } from "../../../api/audit";
import { getAdminCategories } from "../../../api/menu/adminCategories";
import { getOptionGroups } from "../../../api/menu/adminOptionGroups";
import { getAdminProducts } from "../../../api/menu/adminProducts";
import { useAuth } from "../../../app/AuthProvider";
import { hasStaffCapability } from "../../../auth/permissions";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { formatDate } from "../../../utils/format";

export function MenuOverviewPage() {
  const { session } = useAuth();
  const canViewAudit = hasStaffCapability(session, "viewAuditLog");
  const categories = useQuery({
    queryKey: ["admin", "menu-overview", "categories"],
    queryFn: ({ signal }) =>
      getAdminCategories({ includeDeleted: false, pageSize: 1 }, signal),
  });
  const products = useQuery({
    queryKey: ["admin", "menu-overview", "products"],
    queryFn: ({ signal }) =>
      getAdminProducts({ includeDeleted: false, pageSize: 1 }, signal),
  });
  const unavailable = useQuery({
    queryKey: ["admin", "menu-overview", "unavailable"],
    queryFn: ({ signal }) =>
      getAdminProducts(
        { includeDeleted: false, isAvailable: false, pageSize: 1 },
        signal,
      ),
  });
  const hidden = useQuery({
    queryKey: ["admin", "menu-overview", "hidden"],
    queryFn: ({ signal }) =>
      getAdminProducts(
        { includeDeleted: false, isVisible: false, pageSize: 1 },
        signal,
      ),
  });
  const groups = useQuery({
    queryKey: ["admin", "menu-overview", "groups"],
    queryFn: ({ signal }) =>
      getOptionGroups({ includeDeleted: false, pageSize: 1 }, signal),
  });
  const audit = useQuery({
    queryKey: ["admin", "menu-overview", "audit"],
    queryFn: ({ signal }) => getAuditLog({ pageSize: 5 }, signal),
    enabled: canViewAudit,
  });
  const summaryQueries = [categories, products, unavailable, hidden, groups];
  const firstError = summaryQueries.find((query) => query.error)?.error;

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Menu administration</p>
          <h1>Menu overview</h1>
          <p>Live counts from the current administrative menu endpoints.</p>
        </div>
      </div>
      {summaryQueries.some((query) => query.isLoading) ? (
        <LoadingState message="Loading menu summary…" />
      ) : firstError ? (
        <div>
          <ErrorState error={firstError} />
          <button
            className="button button-secondary"
            onClick={() => summaryQueries.forEach((query) => void query.refetch())}
            type="button"
          >
            Retry
          </button>
        </div>
      ) : (
        <div className="summary-grid">
          <SummaryCard label="Categories" value={categories.data?.totalCount ?? 0} />
          <SummaryCard label="Products" value={products.data?.totalCount ?? 0} />
          <SummaryCard
            label="Unavailable"
            value={unavailable.data?.totalCount ?? 0}
          />
          <SummaryCard label="Hidden" value={hidden.data?.totalCount ?? 0} />
          <SummaryCard label="Option groups" value={groups.data?.totalCount ?? 0} />
        </div>
      )}
      <div className="quick-links">
        <Link className="button button-link" to="/staff/menu/categories/new">
          New category
        </Link>
        <Link className="button button-link" to="/staff/menu/products/new">
          New product
        </Link>
        <Link className="button button-link" to="/staff/menu/option-groups/new">
          New option group
        </Link>
      </div>
      {canViewAudit ? (
        <section className="panel panel--spaced">
          <div className="panel-heading">
            <h2>Recent menu activity</h2>
            <Link to="/staff/audit-log">View audit log</Link>
          </div>
          {audit.isLoading ? (
            <LoadingState />
          ) : audit.error ? (
            <ErrorState error={audit.error} />
          ) : audit.data?.items.length ? (
            <ul className="activity-list">
              {audit.data.items.map((item) => (
                <li key={item.id}>
                  <strong>{item.description}</strong>
                  <span>
                    {item.employeeName} · {formatDate(item.timestamp)}
                  </span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="empty-copy">No audit actions have been recorded yet.</p>
          )}
        </section>
      ) : null}
    </section>
  );
}

function SummaryCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="summary-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
