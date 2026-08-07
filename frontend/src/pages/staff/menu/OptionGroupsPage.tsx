import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  deleteOptionGroup,
  getOptionGroups,
  restoreOptionGroup,
  setOptionGroupActive,
} from "../../../api/menu/adminOptionGroups";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { Pagination } from "../../../components/Pagination";
import { useToast } from "../../../components/ToastProvider";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import type { AdminOptionGroup } from "../../../types/menu";
import { isConcurrencyConflict } from "../../../utils/apiErrors";
import { formatDate } from "../../../utils/format";

export function OptionGroupsPage() {
  const queryClient = useQueryClient();
  const { notify } = useToast();
  const [search, setSearch] = useState("");
  const [active, setActive] = useState("");
  const [includeDeleted, setIncludeDeleted] = useState(false);
  const [page, setPage] = useState(1);
  const [pending, setPending] = useState<{
    group: AdminOptionGroup;
    action: "delete" | "restore";
  } | null>(null);
  const filters = useMemo(
    () => ({
      search,
      isActive: active === "" ? undefined : active === "true",
      includeDeleted,
      page,
      pageSize: 20,
    }),
    [active, includeDeleted, page, search],
  );
  const groups = useQuery({
    queryKey: menuQueryKeys.optionGroups(filters),
    queryFn: ({ signal }) => getOptionGroups(filters, signal),
  });
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "option-groups"] });
  const handleError = (error: unknown) => {
    notify(
      isConcurrencyConflict(error)
        ? "This option group changed elsewhere. The list was refreshed."
        : error instanceof Error
          ? error.message
          : "The option group could not be updated.",
      "error",
    );
    void invalidate();
  };
  const activeMutation = useMutation({
    mutationFn: setOptionGroupActive,
    onSuccess: () => {
      notify("Option group status updated.");
      void invalidate();
    },
    onError: handleError,
  });
  const deleteMutation = useMutation({
    mutationFn: deleteOptionGroup,
    onSuccess: () => {
      notify("Option group moved to deleted items.");
      setPending(null);
      void invalidate();
    },
    onError: handleError,
  });
  const restoreMutation = useMutation({
    mutationFn: restoreOptionGroup,
    onSuccess: () => {
      notify("Option group restored.");
      setPending(null);
      void invalidate();
    },
    onError: handleError,
  });

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Menu</p>
          <h1>Option groups</h1>
          <p>Maintain reusable groups and values; product pricing stays in product editors.</p>
        </div>
        <Link className="button button-link" to="/staff/menu/option-groups/new">
          Create option group
        </Link>
      </div>
      <div className="filter-bar">
        <label>
          Search
          <input
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
            placeholder="Group name"
            type="search"
            value={search}
          />
        </label>
        <label>
          Active status
          <select
            onChange={(event) => {
              setActive(event.target.value);
              setPage(1);
            }}
            value={active}
          >
            <option value="">Any</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
        </label>
        <label className="checkbox-field">
          <input
            checked={includeDeleted}
            onChange={(event) => {
              setIncludeDeleted(event.target.checked);
              setPage(1);
            }}
            type="checkbox"
          />
          Include deleted
        </label>
      </div>
      {groups.isLoading ? (
        <LoadingState message="Loading option groups..." />
      ) : groups.error ? (
        <div>
          <ErrorState error={groups.error} />
          <button className="button" onClick={() => void groups.refetch()} type="button">
            Retry
          </button>
        </div>
      ) : groups.data?.items.length ? (
        <>
          <div className="responsive-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">Order</th>
                  <th scope="col">Group</th>
                  <th scope="col">Selection</th>
                  <th scope="col">Defaults</th>
                  <th scope="col">Values</th>
                  <th scope="col">Status</th>
                  <th scope="col">Updated</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {groups.data.items.map((group) => (
                  <tr key={group.id}>
                    <td data-label="Order">{group.displayOrder}</td>
                    <td data-label="Group">
                      <strong>{group.name}</strong>
                      <small>{group.description || "No description"}</small>
                    </td>
                    <td data-label="Selection">{group.selectionType}</td>
                    <td data-label="Defaults">
                      {group.defaultIsRequired ? "Required" : "Optional"};{" "}
                      {group.defaultMinimumSelections} to{" "}
                      {group.defaultMaximumSelections ?? "unlimited"}
                    </td>
                    <td data-label="Values">{group.values.length}</td>
                    <td data-label="Status">
                      <div className="badge-stack">
                        <span className="status-badge">
                          {group.isActive ? "Active" : "Inactive"}
                        </span>
                        {group.isDeleted ? (
                          <span className="status-badge status-badge--error">Deleted</span>
                        ) : null}
                      </div>
                    </td>
                    <td data-label="Updated">{formatDate(group.updatedAt)}</td>
                    <td data-label="Actions">
                      <div className="table-actions">
                        <Link to={`/staff/menu/option-groups/${group.id}`}>Edit</Link>
                        {!group.isDeleted ? (
                          <>
                            <button
                              disabled={activeMutation.isPending}
                              onClick={() => activeMutation.mutate(group)}
                              type="button"
                            >
                              {group.isActive ? "Deactivate" : "Activate"}
                            </button>
                            <button
                              onClick={() => setPending({ group, action: "delete" })}
                              type="button"
                            >
                              Delete
                            </button>
                          </>
                        ) : (
                          <button
                            onClick={() => setPending({ group, action: "restore" })}
                            type="button"
                          >
                            Restore
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            onPageChange={setPage}
            page={groups.data.page}
            totalPages={groups.data.totalPages}
          />
        </>
      ) : (
        <div className="empty-state">
          <h2>{search ? "No search results" : "No option groups yet"}</h2>
          <p>Adjust the filters or create a reusable option group.</p>
        </div>
      )}
      <ConfirmDialog
        confirmLabel={pending?.action === "restore" ? "Restore" : "Delete"}
        description="Existing product assignments remain auditable and may become non-orderable."
        destructive={pending?.action === "delete"}
        onCancel={() => setPending(null)}
        onConfirm={() => {
          if (pending?.action === "delete") deleteMutation.mutate(pending.group);
          if (pending?.action === "restore") restoreMutation.mutate(pending.group);
        }}
        open={pending !== null}
        title={`${pending?.action === "restore" ? "Restore" : "Delete"} ${pending?.group.name ?? "option group"}?`}
      />
    </section>
  );
}
