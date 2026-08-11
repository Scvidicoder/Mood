import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import {
  disableEmployee,
  enableEmployee,
  getEmployee,
  getEmployeeActions,
  getEmployeeRoles,
  resetEmployeePassword,
  updateEmployee,
} from "../../../api/employees";
import { useAuth } from "../../../app/AuthProvider";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { ConflictNotice } from "../../../components/ConflictNotice";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { Pagination } from "../../../components/Pagination";
import { TemporaryPasswordNotice } from "../../../components/TemporaryPasswordNotice";
import { useToast } from "../../../components/ToastProvider";
import type {
  EmployeeDetails,
  ResetEmployeePasswordResponse,
} from "../../../types/employees";
import { isConcurrencyConflict } from "../../../utils/apiErrors";
import { formatDate } from "../../../utils/format";
import { EmployeePermissionsSection } from "./EmployeePermissionsSection";

type Confirmation = "disable" | "enable" | "reset";

export function EmployeeDetailPage() {
  const { id = "" } = useParams();
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { notify } = useToast();
  const [fullName, setFullName] = useState("");
  const [username, setUsername] = useState("");
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [rowVersion, setRowVersion] = useState("");
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [temporaryPassword, setTemporaryPassword] =
    useState<ResetEmployeePasswordResponse | null>(null);
  const [resetting, setResetting] = useState(false);
  const [resetError, setResetError] = useState<unknown>(null);
  const [actionType, setActionType] = useState("");
  const [entityType, setEntityType] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [actionPage, setActionPage] = useState(1);
  const employee = useQuery({
    queryKey: ["employee", id],
    queryFn: ({ signal }) => getEmployee(id, signal),
    enabled: Boolean(id),
  });
  const roles = useQuery({
    queryKey: ["employee-roles"],
    queryFn: ({ signal }) => getEmployeeRoles(signal),
  });
  const actionFilters = useMemo(
    () => ({
      actionType: actionType.trim() || undefined,
      entityType: entityType.trim() || undefined,
      dateFrom: dateFrom ? new Date(`${dateFrom}T00:00:00`).toISOString() : undefined,
      dateTo: dateTo ? new Date(`${dateTo}T23:59:59.999`).toISOString() : undefined,
      page: actionPage,
      pageSize: 10,
    }),
    [actionPage, actionType, dateFrom, dateTo, entityType],
  );
  const actions = useQuery({
    queryKey: ["employee-actions", id, actionFilters],
    queryFn: ({ signal }) => getEmployeeActions(id, actionFilters, signal),
    enabled: Boolean(id),
  });

  useEffect(() => {
    if (employee.data) {
      applyEmployee(employee.data);
    }
  }, [employee.data]);

  function applyEmployee(value: EmployeeDetails) {
    setFullName(value.fullName);
    setUsername(value.username);
    setSelectedRoles(value.roles);
    setRowVersion(value.rowVersion);
  }

  const update = useMutation({
    mutationFn: () =>
      updateEmployee(id, {
        fullName: fullName.trim(),
        username: username.trim(),
        roles: selectedRoles,
        rowVersion,
      }),
    onSuccess: async (updated) => {
      applyEmployee(updated);
      queryClient.setQueryData(["employee", id], updated);
      await queryClient.invalidateQueries({ queryKey: ["employees"] });
      notify("Employee details updated.");
    },
  });
  const stateMutation = useMutation({
    mutationFn: (action: "disable" | "enable") =>
      action === "disable"
        ? disableEmployee(id, rowVersion)
        : enableEmployee(id, rowVersion),
    onSuccess: async (updated) => {
      applyEmployee(updated);
      queryClient.setQueryData(["employee", id], updated);
      setConfirmation(null);
      await queryClient.invalidateQueries({ queryKey: ["employees"] });
      notify(`Employee ${updated.isActive ? "enabled" : "disabled"}.`);
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    update.mutate();
  }

  async function confirmAction() {
    if (!confirmation) return;
    if (confirmation !== "reset") {
      stateMutation.mutate(confirmation);
      return;
    }

    setResetting(true);
    setResetError(null);
    try {
      const response = await resetEmployeePassword(id, rowVersion);
      setRowVersion(response.rowVersion);
      setTemporaryPassword(response);
      setConfirmation(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["employee", id] }),
        queryClient.invalidateQueries({ queryKey: ["employees"] }),
        queryClient.invalidateQueries({ queryKey: ["employee-actions", id] }),
      ]);
      notify("Password reset and existing sessions revoked.");
    } catch (error) {
      setResetError(error);
      setConfirmation(null);
    } finally {
      setResetting(false);
    }
  }

  if (employee.isLoading || roles.isLoading) {
    return <LoadingState message="Loading employee details..." />;
  }

  if (employee.error || roles.error || !employee.data) {
    return <ErrorState error={employee.error ?? roles.error} />;
  }

  const isSelf = employee.data.id === session?.accountId;
  const isAdministrator = employee.data.roles.includes("Administrator");

  return (
    <section>
      <Link className="staff-back-link" to="/staff/employees">
        Back to employees
      </Link>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Employee management</p>
          <h1>{employee.data.fullName}</h1>
          <p>@{employee.data.username}</p>
        </div>
        <span className={`employee-status employee-status--${employee.data.isActive ? "active" : "disabled"}`}>
          {employee.data.isActive ? "Active" : "Disabled"}
        </span>
      </div>

      {temporaryPassword ? (
        <TemporaryPasswordNotice
          onDismiss={() => setTemporaryPassword(null)}
          password={temporaryPassword.temporaryPassword}
          revokedSessionCount={temporaryPassword.revokedSessionCount}
        />
      ) : null}
      {resetError ? <ErrorState error={resetError} /> : null}
      {stateMutation.error ? <ErrorState error={stateMutation.error} /> : null}
      {isConcurrencyConflict(update.error) ? (
        <ConflictNotice
          onDiscard={() => update.reset()}
          onReload={() => {
            update.reset();
            void employee.refetch();
          }}
        />
      ) : update.error ? (
        <ErrorState error={update.error} />
      ) : null}

      <div className="employee-detail-grid">
        <form className="panel employee-form" onSubmit={submit}>
          <h2>Identity and roles</h2>
          <label htmlFor="employee-full-name">Full name</label>
          <input
            id="employee-full-name"
            maxLength={100}
            minLength={2}
            onChange={(event) => setFullName(event.target.value)}
            required
            value={fullName}
          />
          <label htmlFor="employee-username">Username</label>
          <input
            autoCapitalize="none"
            id="employee-username"
            maxLength={64}
            minLength={3}
            onChange={(event) => setUsername(event.target.value)}
            pattern="[A-Za-z0-9._-]+"
            required
            value={username}
          />
          <fieldset className="role-selector">
            <legend>Roles</legend>
            {roles.data?.map((role) => (
              <label className="checkbox-field" key={role.name}>
                <input
                  checked={selectedRoles.includes(role.name)}
                  onChange={(event) =>
                    setSelectedRoles((current) =>
                      event.target.checked
                        ? [...current, role.name]
                        : current.filter((value) => value !== role.name),
                    )
                  }
                  type="checkbox"
                />
                {role.displayName}
              </label>
            ))}
          </fieldset>
          {isAdministrator ? (
            <p className="notice notice--warning">
              Removing Administrator access is blocked if this is the final active
              Administrator account.
            </p>
          ) : null}
          <button
            className="button"
            disabled={update.isPending || selectedRoles.length === 0}
            type="submit"
          >
            {update.isPending ? "Saving..." : "Save changes"}
          </button>
        </form>

        <aside className="panel employee-account-panel">
          <h2>Account status</h2>
          <dl className="detail-list">
            <dt>Status</dt>
            <dd>{employee.data.isActive ? "Active" : "Disabled"}</dd>
            <dt>Password change</dt>
            <dd>{employee.data.mustChangePassword ? "Required" : "Not required"}</dd>
            <dt>Created</dt>
            <dd>{formatDate(employee.data.createdAt)}</dd>
            <dt>Updated</dt>
            <dd>{formatDate(employee.data.updatedAt)}</dd>
            <dt>Last login</dt>
            <dd>
              {employee.data.lastLoginAt ? formatDate(employee.data.lastLoginAt) : "Never"}
            </dd>
          </dl>
          <div className="employee-account-actions">
            <button
              className={employee.data.isActive ? "button button-danger" : "button"}
              onClick={() => setConfirmation(employee.data.isActive ? "disable" : "enable")}
              type="button"
            >
              {employee.data.isActive ? "Disable access" : "Enable access"}
            </button>
            <button
              className="button button-secondary"
              onClick={() => setConfirmation("reset")}
              type="button"
            >
              Reset password
            </button>
          </div>
          {isSelf ? (
            <p className="warning-copy">
              This is your account. Self-disable is allowed only when another active
              Administrator remains.
            </p>
          ) : null}
        </aside>
      </div>

      <EmployeePermissionsSection employeeId={employee.data.id} />

      <section className="panel employee-actions-section">
        <div className="staff-page-heading staff-page-heading--compact">
          <div>
            <h2>Action history</h2>
            <p>Activity performed by this employee and account changes affecting them.</p>
          </div>
        </div>
        <div className="filter-bar filter-bar--wide">
          <label>
            Action type
            <input
              onChange={(event) => {
                setActionType(event.target.value);
                setActionPage(1);
              }}
              value={actionType}
            />
          </label>
          <label>
            Entity type
            <input
              onChange={(event) => {
                setEntityType(event.target.value);
                setActionPage(1);
              }}
              value={entityType}
            />
          </label>
          <label>
            From
            <input
              onChange={(event) => {
                setDateFrom(event.target.value);
                setActionPage(1);
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
                setActionPage(1);
              }}
              type="date"
              value={dateTo}
            />
          </label>
        </div>
        {actions.isLoading ? (
          <LoadingState message="Loading employee action history..." />
        ) : actions.error ? (
          <ErrorState error={actions.error} />
        ) : actions.data?.items.length ? (
          <>
            <div className="responsive-table-wrap">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th scope="col">Timestamp</th>
                    <th scope="col">Actor</th>
                    <th scope="col">Action</th>
                    <th scope="col">Entity</th>
                    <th scope="col">Description</th>
                  </tr>
                </thead>
                <tbody>
                  {actions.data.items.map((action) => (
                    <tr key={action.id}>
                      <td data-label="Timestamp">{formatDate(action.timestamp)}</td>
                      <td data-label="Actor">{action.actingEmployeeName}</td>
                      <td data-label="Action">{action.actionType}</td>
                      <td data-label="Entity">{action.entityType}</td>
                      <td data-label="Description">{action.description}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              onPageChange={setActionPage}
              page={actions.data.page}
              totalPages={actions.data.totalPages}
            />
          </>
        ) : (
          <div className="empty-state empty-state--compact">
            <h3>No actions found</h3>
          </div>
        )}
      </section>

      <ConfirmDialog
        confirmLabel={
          confirmation === "reset"
            ? "Reset password"
            : confirmation === "disable"
              ? "Disable employee"
              : "Enable employee"
        }
        description={confirmationDescription(confirmation, employee.data.fullName, isSelf)}
        destructive={confirmation !== "enable"}
        onCancel={() => setConfirmation(null)}
        onConfirm={() => void confirmAction()}
        open={Boolean(confirmation) && !stateMutation.isPending && !resetting}
        title={
          confirmation === "reset"
            ? "Reset employee password?"
            : confirmation === "disable"
              ? "Disable employee access?"
              : "Enable employee access?"
        }
      />
    </section>
  );
}

function confirmationDescription(
  confirmation: Confirmation | null,
  fullName: string,
  isSelf: boolean,
) {
  if (confirmation === "reset") {
    return `Generate a new one-time temporary password for ${fullName} and revoke every active refresh session?`;
  }
  if (confirmation === "enable") {
    return `Restore sign-in access for ${fullName}? Previously revoked sessions remain revoked.`;
  }
  return `Disable ${fullName}, revoke active refresh sessions, and reject existing access tokens on staff APIs?${isSelf ? " This is your own account." : ""}`;
}
