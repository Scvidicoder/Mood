import { useQuery } from "@tanstack/react-query";
import { healthApi } from "../api/health";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import type { HealthReport, SystemInfo } from "../types/health";

export function HealthPage() {
  const liveQuery = useQuery({
    queryKey: ["health", "live"],
    queryFn: ({ signal }) => healthApi.getLive(signal),
  });
  const readyQuery = useQuery({
    queryKey: ["health", "ready"],
    queryFn: ({ signal }) => healthApi.getReady(signal),
  });
  const systemInfoQuery = useQuery({
    queryKey: ["system", "info"],
    queryFn: ({ signal }) => healthApi.getSystemInfo(signal),
  });

  const isRefreshing =
    liveQuery.isFetching ||
    readyQuery.isFetching ||
    systemInfoQuery.isFetching;

  const refreshAll = () => {
    void Promise.all([
      liveQuery.refetch(),
      readyQuery.refetch(),
      systemInfoQuery.refetch(),
    ]);
  };

  return (
    <section className="page">
      <div className="page-heading">
        <p className="eyebrow">Foundation diagnostics</p>
        <h1>System health</h1>
        <p>
          Live process state, PostgreSQL readiness, and public service metadata
          are queried independently from the backend.
        </p>
      </div>

      <div className="health-toolbar">
        <h2>Current status</h2>
        <button
          className="button"
          type="button"
          onClick={refreshAll}
          disabled={isRefreshing}
        >
          {isRefreshing ? "Checking…" : "Refresh"}
        </button>
      </div>

      <div className="status-grid">
        <HealthStatusCard
          title="Process"
          loading={liveQuery.isPending}
          error={liveQuery.error}
          report={liveQuery.data}
        />
        <HealthStatusCard
          title="PostgreSQL readiness"
          loading={readyQuery.isPending}
          error={readyQuery.error}
          report={readyQuery.data}
        />
        <SystemInfoCard
          loading={systemInfoQuery.isPending}
          error={systemInfoQuery.error}
          info={systemInfoQuery.data}
        />
      </div>
    </section>
  );
}

interface HealthStatusCardProps {
  title: string;
  loading: boolean;
  error: unknown;
  report?: HealthReport;
}

function HealthStatusCard({
  title,
  loading,
  error,
  report,
}: HealthStatusCardProps) {
  return (
    <article className="status-card">
      <StatusCardHeader title={title} loading={loading} error={error} />
      {loading ? (
        <LoadingState message={`Checking ${title.toLowerCase()}…`} />
      ) : error ? (
        <ErrorState error={error} />
      ) : (
        <dl className="status-details">
          <dt>Status</dt>
          <dd>{report?.status ?? "Unknown"}</dd>
          <dt>Checks</dt>
          <dd>{report?.checks.length ?? 0}</dd>
        </dl>
      )}
    </article>
  );
}

interface SystemInfoCardProps {
  loading: boolean;
  error: unknown;
  info?: SystemInfo;
}

function SystemInfoCard({ loading, error, info }: SystemInfoCardProps) {
  return (
    <article className="status-card">
      <StatusCardHeader title="Service information" loading={loading} error={error} />
      {loading ? (
        <LoadingState message="Loading service information…" />
      ) : error ? (
        <ErrorState error={error} />
      ) : (
        <dl className="status-details">
          <dt>Service</dt>
          <dd>{info?.service ?? "Unknown"}</dd>
          <dt>Environment</dt>
          <dd>{info?.environment ?? "Unknown"}</dd>
          <dt>API</dt>
          <dd>{info?.apiVersion ?? "Unknown"}</dd>
          <dt>UTC</dt>
          <dd>
            {info?.utcTime
              ? new Date(info.utcTime).toLocaleString()
              : "Unknown"}
          </dd>
        </dl>
      )}
    </article>
  );
}

interface StatusCardHeaderProps {
  title: string;
  loading: boolean;
  error: unknown;
}

function StatusCardHeader({
  title,
  loading,
  error,
}: StatusCardHeaderProps) {
  const badgeClass = error
    ? "status-badge status-badge--error"
    : loading
      ? "status-badge status-badge--loading"
      : "status-badge";
  const badgeText = error ? "Error" : loading ? "Loading" : "Available";

  return (
    <div className="status-card__header">
      <h3>{title}</h3>
      <span className={badgeClass}>{badgeText}</span>
    </div>
  );
}
