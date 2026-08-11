import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import {
  getEmployeePermissions,
  replaceEmployeePermissionOverrides,
} from "../../../api/employees";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { useToast } from "../../../components/ToastProvider";
import type {
  EmployeePermission,
  EmployeePermissionsResponse,
} from "../../../types/employees";

interface EmployeePermissionsSectionProps {
  employeeId: string;
}

export function EmployeePermissionsSection({
  employeeId,
}: EmployeePermissionsSectionProps) {
  const queryClient = useQueryClient();
  const { notify } = useToast();
  const [overrides, setOverrides] = useState<Record<string, boolean>>({});
  const permissions = useQuery({
    queryKey: ["employee-permissions", employeeId],
    queryFn: ({ signal }) => getEmployeePermissions(employeeId, signal),
    enabled: Boolean(employeeId),
  });

  useEffect(() => {
    if (permissions.data) {
      setOverrides(toOverrideMap(permissions.data));
    }
  }, [permissions.data]);

  const groups = useMemo(() => {
    const grouped = new Map<string, EmployeePermission[]>();
    for (const permission of permissions.data?.permissions ?? []) {
      const group = grouped.get(permission.group) ?? [];
      group.push(permission);
      grouped.set(permission.group, group);
    }
    return [...grouped.entries()];
  }, [permissions.data]);

  const save = useMutation({
    mutationFn: (reset: boolean) =>
      replaceEmployeePermissionOverrides(
        employeeId,
        reset
          ? []
          : Object.entries(overrides).map(([permission, isAllowed]) => ({
              permission,
              isAllowed,
            })),
      ),
    onSuccess: (updated, reset) => {
      queryClient.setQueryData(["employee-permissions", employeeId], updated);
      setOverrides(toOverrideMap(updated));
      notify(
        reset
          ? "Permissions reset to role defaults."
          : "Employee permissions updated.",
      );
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    save.mutate(false);
  }

  function setPermission(permission: EmployeePermission, isAllowed: boolean) {
    setOverrides((current) => {
      const next = { ...current };
      if (isAllowed === permission.roleAllowed) {
        delete next[permission.permission];
      } else {
        next[permission.permission] = isAllowed;
      }
      return next;
    });
  }

  if (permissions.isLoading) {
    return <LoadingState message="Loading employee permissions..." />;
  }

  if (permissions.error || !permissions.data) {
    return <ErrorState error={permissions.error} />;
  }

  return (
    <section className="panel employee-permissions-section">
      <div className="staff-page-heading staff-page-heading--compact">
        <div>
          <h2>Permissions</h2>
          <p>Individual overrides take precedence over role defaults.</p>
        </div>
      </div>
      {save.error ? <ErrorState error={save.error} /> : null}
      <form onSubmit={submit}>
        <div className="permission-groups">
          {groups.map(([group, items]) => (
            <fieldset className="permission-group" key={group}>
              <legend>{group}</legend>
              {items.map((permission) => (
                <label className="checkbox-field" key={permission.permission}>
                  <input
                    checked={
                      overrides[permission.permission] ?? permission.roleAllowed
                    }
                    onChange={(event) =>
                      setPermission(permission, event.target.checked)
                    }
                    type="checkbox"
                  />
                  {permission.displayName}
                </label>
              ))}
            </fieldset>
          ))}
        </div>
        <div className="permission-actions">
          <button className="button" disabled={save.isPending} type="submit">
            {save.isPending ? "Saving..." : "Save permissions"}
          </button>
          <button
            className="button button-secondary"
            disabled={save.isPending}
            onClick={() => save.mutate(true)}
            type="button"
          >
            Reset to Role Defaults
          </button>
        </div>
      </form>
    </section>
  );
}

function toOverrideMap(response: EmployeePermissionsResponse) {
  return Object.fromEntries(
    response.permissions
      .filter((permission) => typeof permission.override === "boolean")
      .map((permission) => [permission.permission, permission.override!]),
  );
}
