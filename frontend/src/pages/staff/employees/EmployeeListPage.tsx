import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  disableEmployee,
  enableEmployee,
  getEmployeeRoles,
  getEmployees,
  resetEmployeePassword,
} from "../../../api/employees";
import { useAuth } from "../../../app/AuthProvider";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { Pagination } from "../../../components/Pagination";
import { TemporaryPasswordNotice } from "../../../components/TemporaryPasswordNotice";
import { useToast } from "../../../components/ToastProvider";
import type {
  EmployeeListItem,
  EmployeeStatusFilter,
} from "../../../types/employees";
import { formatDate } from "../../../utils/format";

type PendingAction = "disable" | "enable" | "reset";

interface Confirmation {
  action: PendingAction;
  employee: EmployeeListItem;
}

interface TemporaryPasswordState {
  password: string;
  revokedSessionCount: number;
}

export function EmployeeListPage() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { notify } = useToast();
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [status, setStatus] = useState<EmployeeStatusFilter>("All");
  const [page, setPage] = useState(1);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [temporaryPassword, setTemporaryPassword] =
    useState<TemporaryPasswordState | null>(null);
  const [resetting, setResetting] = useState(false);
  const [resetError, setResetError] = useState<unknown>(null);
  const filters = useMemo(
    () => ({
      search: search.trim() || undefined,
      role: role || undefined,
      status,
      page,
      pageSize: 20,
    }),
    [page, role, search, status],
  );
  const employees = useQuery({
    queryKey: ["employees", filters],
    queryFn: ({ signal }) => getEmployees(filters, signal),
  });
  const roles = useQuery({
    queryKey: ["employee-roles"],
    queryFn: ({ signal }) => getEmployeeRoles(signal),
  });
  const stateMutation = useMutation({
    mutationFn: ({ action, employee }: Confirmation) =>
      action === "disable"
        ? disableEmployee(employee.id, employee.rowVersion)
        : enableEmployee(employee.id, employee.rowVersion),
    onSuccess: async (employee) => {
      setConfirmation(null);
      await queryClient.invalidateQueries({ queryKey: ["employees"] });
      notify(`${employee.fullName} was ${employee.isActive ? "enabled" : "disabled"}.`);
    },
  });

  async function confirmAction() {
    if (!confirmation) return;
    if (confirmation.action !== "reset") {
      stateMutation.mutate(confirmation);
      return;
    }

    setResetting(true);
    setResetError(null);
    try {
      const response = await resetEmployeePassword(
        confirmation.employee.id,
        confirmation.employee.rowVersion,
      );
      setTemporaryPassword({
        password: response.temporaryPassword,
        revokedSessionCount: response.revokedSessionCount,
      });
      setConfirmation(null);
      await queryClient.invalidateQueries({ queryKey: ["employees"] });
      notify(`Password reset for ${confirmation.employee.fullName}.`);
    } catch (error) {
      setResetError(error);
      setConfirmation(null);
    } finally {
      setResetting(false);
    }
  }

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Administration</p>
          <h1>Employees</h1>
          <p>Manage staff identities, multiple roles, access, and passwords.</p>
        </div>
        <Link className="button" to="/staff/employees/new">
          Create employee
        </Link>
      </div>

      {temporaryPassword ? (
        <TemporaryPasswordNotice
          onDismiss={() => setTemporaryPassword(null)}
          password={temporaryPassword.password}
          revokedSessionCount={temporaryPassword.revokedSessionCount}
        />
      ) : null}
      {resetError ? <ErrorState error={resetError} /> : null}
      {stateMutation.error ? <ErrorState error={stateMutation.error} /> : null}

      <div className="filter-bar employee-filter-bar">
        <label>
          Search
          <input
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
            placeholder="Name or username"
            type="search"
            value={search}
          />
        </label>
        <label>
          Role
          <select
            onChange={(event) => {
              setRole(event.target.value);
              setPage(1);
            }}
            value={role}
          >
            <option value="">All roles</option>
            {roles.data?.map((option) => (
              <option key={option.name} value={option.name}>
                {option.displayName}
              </option>
            ))}
          </select>
        </label>
        <label>
          Status
          <select
            onChange={(event) => {
              setStatus(event.target.value as EmployeeStatusFilter);
              setPage(1);
            }}
            value={status}
          >
            <option value="All">All</option>
            <option value="Active">Active</option>
            <option value="Disabled">Disabled</option>
          </select>
        </label>
      </div>

      {employees.isLoading ? (
        <LoadingState message="Loading employees..." />
      ) : employees.error ? (
        <div>
          <ErrorState error={employees.error} />
          <button className="button" onClick={() => void employees.refetch()} type="button">
            Retry
          </button>
        </div>
      ) : employees.data?.items.length ? (
        <>
          <div className="responsive-table-wrap">
            <table className="admin-table employee-table">
              <thead>
                <tr>
                  <th scope="col">Employee</th>
                  <th scope="col">Roles</th>
                  <th scope="col">Status</th>
                  <th scope="col">Password</th>
                  <th scope="col">Created</th>
                  <th scope="col">Last login</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {employees.data.items.map((employee) => (
                  <tr key={employee.id}>
                    <td data-label="Employee">
                      <strong>{employee.fullName}</strong>
                      <small>@{employee.username}</small>
                    </td>
                    <td data-label="Roles">
                      <div className="badge-stack">
                        {employee.roles.map((employeeRole) => (
                          <span className="status-badge" key={employeeRole}>
                            {employeeRole}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td data-label="Status">
                      <span className={`employee-status employee-status--${employee.isActive ? "active" : "disabled"}`}>
                        {employee.isActive ? "Active" : "Disabled"}
                      </span>
                    </td>
                    <td data-label="Password">
                      {employee.mustChangePassword ? "Change required" : "Current"}
                    </td>
                    <td data-label="Created">{formatDate(employee.createdAt)}</td>
                    <td data-label="Last login">
                      {employee.lastLoginAt ? formatDate(employee.lastLoginAt) : "Never"}
                    </td>
                    <td data-label="Actions">
                      <div className="table-actions">
                        <Link to={`/staff/employees/${employee.id}`}>View and edit</Link>
                        <button
                          onClick={() =>
                            setConfirmation({
                              action: employee.isActive ? "disable" : "enable",
                              employee,
                            })
                          }
                          type="button"
                        >
                          {employee.isActive ? "Disable" : "Enable"}
                        </button>
                        <button
                          onClick={() => setConfirmation({ action: "reset", employee })}
                          type="button"
                        >
                          Reset password
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            onPageChange={setPage}
            page={employees.data.page}
            totalPages={employees.data.totalPages}
          />
        </>
      ) : (
        <div className="empty-state">
          <h2>No employees found</h2>
          <p>Adjust the filters or create the first employee account.</p>
        </div>
      )}

      <ConfirmDialog
        confirmLabel={
          confirmation?.action === "reset"
            ? "Reset password"
            : confirmation?.action === "disable"
              ? "Disable employee"
              : "Enable employee"
        }
        description={confirmationDescription(confirmation, session?.accountId)}
        destructive={confirmation?.action !== "enable"}
        onCancel={() => setConfirmation(null)}
        onConfirm={() => void confirmAction()}
        open={Boolean(confirmation) && !stateMutation.isPending && !resetting}
        title={
          confirmation?.action === "reset"
            ? "Reset employee password?"
            : confirmation?.action === "disable"
              ? "Disable employee access?"
              : "Enable employee access?"
        }
      />
    </section>
  );
}

function confirmationDescription(
  confirmation: Confirmation | null,
  currentEmployeeId?: string,
): string {
  if (!confirmation) return "";
  const isSelf = confirmation.employee.id === currentEmployeeId;
  if (confirmation.action === "reset") {
    return `A new one-time temporary password will replace the current password for ${confirmation.employee.fullName}. All active sessions will be revoked.`;
  }
  if (confirmation.action === "enable") {
    return `${confirmation.employee.fullName} will be able to sign in again, but revoked sessions will not be restored.`;
  }
  return `${confirmation.employee.fullName} will lose staff API access immediately and all refresh sessions will be revoked.${isSelf ? " This is your own account." : ""} At least one active Administrator must remain.`;
}
