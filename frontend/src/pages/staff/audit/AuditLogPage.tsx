import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getAuditLog } from "../../../api/audit";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { Pagination } from "../../../components/Pagination";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import { formatDate } from "../../../utils/format";

export function AuditLogPage() {
  const [employeeId, setEmployeeId] = useState("");
  const [actionType, setActionType] = useState("");
  const [entityType, setEntityType] = useState("");
  const [entityId, setEntityId] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [page, setPage] = useState(1);
  const filters = useMemo(
    () => ({
      employeeId: employeeId || undefined,
      actionType: actionType.trim() || undefined,
      entityType: entityType.trim() || undefined,
      entityId: entityId || undefined,
      dateFrom: dateFrom ? new Date(`${dateFrom}T00:00:00`).toISOString() : undefined,
      dateTo: dateTo ? new Date(`${dateTo}T23:59:59.999`).toISOString() : undefined,
      page,
      pageSize: 20,
    }),
    [actionType, dateFrom, dateTo, employeeId, entityId, entityType, page],
  );
  const audit = useQuery({
    queryKey: menuQueryKeys.audit(filters),
    queryFn: ({ signal }) => getAuditLog(filters, signal),
  });

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Administration</p>
          <h1>Audit log</h1>
          <p>Review attributable administrative changes and correlation identifiers.</p>
        </div>
      </div>
      <div className="filter-bar filter-bar--wide">
        <label>
          Employee ID
          <input
            onChange={(event) => {
              setEmployeeId(event.target.value.trim());
              setPage(1);
            }}
            placeholder="UUID"
            value={employeeId}
          />
        </label>
        <label>
          Action type
          <input
            onChange={(event) => {
              setActionType(event.target.value);
              setPage(1);
            }}
            placeholder="Created, Updated..."
            value={actionType}
          />
        </label>
        <label>
          Entity type
          <input
            onChange={(event) => {
              setEntityType(event.target.value);
              setPage(1);
            }}
            placeholder="Product, Category..."
            value={entityType}
          />
        </label>
        <label>
          Entity ID
          <input
            onChange={(event) => {
              setEntityId(event.target.value.trim());
              setPage(1);
            }}
            placeholder="UUID"
            value={entityId}
          />
        </label>
        <label>
          From
          <input
            onChange={(event) => {
              setDateFrom(event.target.value);
              setPage(1);
            }}
            type="date"
            value={dateFrom}
          />
        </label>
        <label>
          To
          <input
            onChange={(event) => {
              setDateTo(event.target.value);
              setPage(1);
            }}
            type="date"
            value={dateTo}
          />
        </label>
      </div>
      {audit.isLoading ? (
        <LoadingState message="Loading audit history..." />
      ) : audit.error ? (
        <div>
          <ErrorState error={audit.error} />
          <button className="button" onClick={() => void audit.refetch()} type="button">
            Retry
          </button>
        </div>
      ) : audit.data?.items.length ? (
        <>
          <div className="responsive-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">Timestamp</th>
                  <th scope="col">Employee</th>
                  <th scope="col">Action</th>
                  <th scope="col">Entity</th>
                  <th scope="col">Description</th>
                  <th scope="col">Correlation ID</th>
                  <th scope="col">Details</th>
                </tr>
              </thead>
              <tbody>
                {audit.data.items.map((item) => (
                  <tr key={item.id}>
                    <td data-label="Timestamp">{formatDate(item.timestamp)}</td>
                    <td data-label="Employee">
                      <strong>{item.employeeName}</strong>
                      <small>{item.employeeId}</small>
                    </td>
                    <td data-label="Action">{item.actionType}</td>
                    <td data-label="Entity">
                      <strong>{item.entityType}</strong>
                      <small>{item.entityId}</small>
                    </td>
                    <td data-label="Description">{item.description}</td>
                    <td data-label="Correlation ID">
                      <code>{item.correlationId}</code>
                    </td>
                    <td data-label="Details">
                      <Link to={`/staff/audit-log/${item.id}`}>View details</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            onPageChange={setPage}
            page={audit.data.page}
            totalPages={audit.data.totalPages}
          />
        </>
      ) : (
        <div className="empty-state">
          <h2>No audit entries found</h2>
          <p>Adjust the filters or check again after an administrative change.</p>
        </div>
      )}
    </section>
  );
}
