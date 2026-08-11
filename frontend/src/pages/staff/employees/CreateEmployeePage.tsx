import { useQuery } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { createEmployee, getEmployeeRoles } from "../../../api/employees";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { TemporaryPasswordNotice } from "../../../components/TemporaryPasswordNotice";
import type { CreateEmployeeResponse } from "../../../types/employees";

export function CreateEmployeePage() {
  const [fullName, setFullName] = useState("");
  const [username, setUsername] = useState("");
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [created, setCreated] = useState<CreateEmployeeResponse | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const roles = useQuery({
    queryKey: ["employee-roles"],
    queryFn: ({ signal }) => getEmployeeRoles(signal),
  });

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      setCreated(await createEmployee({
        fullName: fullName.trim(),
        username: username.trim(),
        roles: selectedRoles,
      }));
    } catch (submitError) {
      setError(submitError);
    } finally {
      setSubmitting(false);
    }
  }

  if (roles.isLoading) {
    return <LoadingState message="Loading employee roles..." />;
  }

  if (roles.error) {
    return <ErrorState error={roles.error} />;
  }

  return (
    <section>
      <Link className="staff-back-link" to="/staff/employees">
        Back to employees
      </Link>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Employee management</p>
          <h1>Create employee</h1>
          <p>The backend generates a secure temporary password automatically.</p>
        </div>
      </div>

      {created ? (
        <>
          <TemporaryPasswordNotice password={created.temporaryPassword} />
          <div className="panel">
            <h2>Employee created successfully</h2>
            <p>
              {created.employee.fullName} must change the temporary password at
              first sign-in.
            </p>
            <div className="inline-actions">
              <Link className="button" to={`/staff/employees/${created.employee.id}`}>
                Open employee details
              </Link>
              <button
                className="button button-secondary"
                onClick={() => {
                  setCreated(null);
                  setFullName("");
                  setUsername("");
                  setSelectedRoles([]);
                }}
                type="button"
              >
                Create another employee
              </button>
            </div>
          </div>
        </>
      ) : (
        <form className="panel employee-form" onSubmit={(event) => void submit(event)}>
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
          {selectedRoles.length === 0 ? (
            <p className="warning-copy">Select at least one role.</p>
          ) : null}
          {error ? <ErrorState error={error} /> : null}
          <div className="form-actions">
            <button
              className="button"
              disabled={submitting || selectedRoles.length === 0}
              type="submit"
            >
              {submitting ? "Creating..." : "Create employee"}
            </button>
            <Link className="button button-secondary" to="/staff/employees">
              Cancel
            </Link>
          </div>
        </form>
      )}
    </section>
  );
}
