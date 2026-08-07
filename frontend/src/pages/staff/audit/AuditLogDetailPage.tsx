import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { getAuditLogDetail } from "../../../api/audit";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import { formatDate } from "../../../utils/format";

type JsonObject = Record<string, unknown>;

export function AuditLogDetailPage() {
  const { id = "" } = useParams();
  const audit = useQuery({
    queryKey: menuQueryKeys.auditDetail(id),
    queryFn: ({ signal }) => getAuditLogDetail(id, signal),
  });
  if (audit.isLoading) return <LoadingState message="Loading audit details..." />;
  if (audit.error) {
    return (
      <div>
        <ErrorState error={audit.error} />
        <button className="button" onClick={() => void audit.refetch()} type="button">
          Retry
        </button>
      </div>
    );
  }
  if (!audit.data) return null;
  const oldValues = parseJson(audit.data.oldValuesJson);
  const newValues = parseJson(audit.data.newValuesJson);
  const changedFields =
    isObject(oldValues) || isObject(newValues)
      ? Array.from(
          new Set([
            ...Object.keys(isObject(oldValues) ? oldValues : {}),
            ...Object.keys(isObject(newValues) ? newValues : {}),
          ]),
        ).filter(
          (key) =>
            JSON.stringify(isObject(oldValues) ? oldValues[key] : undefined) !==
            JSON.stringify(isObject(newValues) ? newValues[key] : undefined),
        )
      : [];

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Audit log</p>
          <h1>{audit.data.actionType} {audit.data.entityType}</h1>
          <p>{audit.data.description}</p>
        </div>
        <Link className="button button-secondary button-link" to="/staff/audit-log">
          Back to audit log
        </Link>
      </div>
      <dl className="panel detail-list">
        <dt>Timestamp</dt>
        <dd>{formatDate(audit.data.timestamp)}</dd>
        <dt>Employee</dt>
        <dd>{audit.data.employeeName} ({audit.data.employeeId})</dd>
        <dt>Entity</dt>
        <dd>{audit.data.entityType} ({audit.data.entityId})</dd>
        <dt>Correlation ID</dt>
        <dd><code>{audit.data.correlationId}</code></dd>
      </dl>
      {changedFields.length ? (
        <section className="panel panel--spaced">
          <h2>Changed fields</h2>
          <div className="responsive-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">Field</th>
                  <th scope="col">Before</th>
                  <th scope="col">After</th>
                </tr>
              </thead>
              <tbody>
                {changedFields.map((field) => (
                  <tr key={field}>
                    <th scope="row">{field}</th>
                    <td><code>{formatJsonValue(isObject(oldValues) ? oldValues[field] : undefined)}</code></td>
                    <td><code>{formatJsonValue(isObject(newValues) ? newValues[field] : undefined)}</code></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
      <div className="audit-json-grid">
        <JsonPanel label="Old values" raw={audit.data.oldValuesJson} value={oldValues} />
        <JsonPanel label="New values" raw={audit.data.newValuesJson} value={newValues} />
      </div>
    </section>
  );
}

function JsonPanel({
  label,
  raw,
  value,
}: {
  label: string;
  raw?: string;
  value: unknown;
}) {
  return (
    <section className="panel">
      <h2>{label}</h2>
      <pre className="audit-json">
        {raw ? (value === undefined ? raw : JSON.stringify(value, null, 2)) : "No values recorded."}
      </pre>
    </section>
  );
}

function parseJson(raw?: string): unknown {
  if (!raw) return undefined;
  try {
    return JSON.parse(raw) as unknown;
  } catch {
    return undefined;
  }
}

function isObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function formatJsonValue(value: unknown): string {
  return value === undefined ? "-" : JSON.stringify(value);
}
