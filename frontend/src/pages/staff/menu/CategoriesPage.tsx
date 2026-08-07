import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  deleteCategory,
  getAdminCategories,
  reorderCategories,
  restoreCategory,
  setCategoryVisibility,
} from "../../../api/menu/adminCategories";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { Pagination } from "../../../components/Pagination";
import { useToast } from "../../../components/ToastProvider";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import type { AdminCategory } from "../../../types/menu";
import { isConcurrencyConflict } from "../../../utils/apiErrors";
import { formatDate } from "../../../utils/format";

export function CategoriesPage() {
  const queryClient = useQueryClient();
  const { notify } = useToast();
  const [search, setSearch] = useState("");
  const [includeDeleted, setIncludeDeleted] = useState(false);
  const [page, setPage] = useState(1);
  const [pendingAction, setPendingAction] = useState<{
    category: AdminCategory;
    action: "delete" | "restore";
  } | null>(null);
  const filters = useMemo(
    () => ({ search, includeDeleted, page, pageSize: 20 }),
    [includeDeleted, page, search],
  );
  const categories = useQuery({
    queryKey: menuQueryKeys.categories(filters),
    queryFn: ({ signal }) => getAdminCategories(filters, signal),
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "categories"] });
  const handleMutationError = (error: unknown) => {
    if (isConcurrencyConflict(error)) {
      notify("The category changed elsewhere. The current list was reloaded.", "error");
      void invalidate();
    }
  };
  const visibility = useMutation({
    mutationFn: setCategoryVisibility,
    onSuccess: () => {
      notify("Category visibility updated.");
      void invalidate();
    },
    onError: handleMutationError,
  });
  const remove = useMutation({
    mutationFn: deleteCategory,
    onSuccess: () => {
      notify("Category moved to deleted items.");
      setPendingAction(null);
      void invalidate();
    },
    onError: handleMutationError,
  });
  const restore = useMutation({
    mutationFn: restoreCategory,
    onSuccess: () => {
      notify("Category restored.");
      setPendingAction(null);
      void invalidate();
    },
    onError: handleMutationError,
  });
  const reorder = useMutation({
    mutationFn: reorderCategories,
    onSuccess: () => {
      notify("Category order saved.");
      void invalidate();
    },
    onError: (error) => {
      notify(
        isConcurrencyConflict(error)
          ? "The category order changed elsewhere. Server ordering was restored."
          : "The category order could not be saved.",
        "error",
      );
      void invalidate();
    },
  });

  function move(categoryIndex: number, direction: -1 | 1) {
    const current = categories.data?.items;
    if (!current) return;
    const targetIndex = categoryIndex + direction;
    if (targetIndex < 0 || targetIndex >= current.length) return;
    const next = current.map((category) => ({ ...category }));
    const currentOrder = next[categoryIndex].displayOrder;
    next[categoryIndex].displayOrder = next[targetIndex].displayOrder;
    next[targetIndex].displayOrder = currentOrder;
    [next[categoryIndex], next[targetIndex]] = [
      next[targetIndex],
      next[categoryIndex],
    ];
    reorder.mutate(
      next.map(({ id, displayOrder, rowVersion }) => ({
        id,
        displayOrder,
        rowVersion,
      })),
    );
  }

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Menu</p>
          <h1>Categories</h1>
          <p>Search, reorder, hide, delete, and restore menu categories.</p>
        </div>
        <Link className="button button-link" to="/staff/menu/categories/new">
          Create category
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
            placeholder="Category name"
            type="search"
            value={search}
          />
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
      {categories.isLoading ? (
        <LoadingState message="Loading categories…" />
      ) : categories.error ? (
        <div>
          <ErrorState error={categories.error} />
          <button
            className="button button-secondary"
            onClick={() => void categories.refetch()}
            type="button"
          >
            Retry
          </button>
        </div>
      ) : categories.data?.items.length ? (
        <>
          <div className="responsive-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th scope="col">Order</th>
                  <th scope="col">Category</th>
                  <th scope="col">Products</th>
                  <th scope="col">Status</th>
                  <th scope="col">Updated</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {categories.data.items.map((category, index) => (
                  <tr key={category.id}>
                    <td data-label="Order">
                      <span>{category.displayOrder}</span>
                      <div className="reorder-controls" aria-label={`Reorder ${category.name}`}>
                        <button
                          aria-label={`Move ${category.name} up`}
                          disabled={index === 0 || reorder.isPending}
                          onClick={() => move(index, -1)}
                          type="button"
                        >
                          ↑
                        </button>
                        <button
                          aria-label={`Move ${category.name} down`}
                          disabled={
                            index === categories.data.items.length - 1 ||
                            reorder.isPending
                          }
                          onClick={() => move(index, 1)}
                          type="button"
                        >
                          ↓
                        </button>
                      </div>
                    </td>
                    <td data-label="Category">
                      <strong>{category.name}</strong>
                      <small>{category.description || "No description"}</small>
                    </td>
                    <td data-label="Products">{category.productCount}</td>
                    <td data-label="Status">
                      <div className="badge-stack">
                        <span className="status-badge">
                          {category.isVisible ? "Visible" : "Hidden"}
                        </span>
                        {category.isDeleted ? (
                          <span className="status-badge status-badge--error">
                            Deleted
                          </span>
                        ) : null}
                      </div>
                    </td>
                    <td data-label="Updated">{formatDate(category.updatedAt)}</td>
                    <td data-label="Actions">
                      <div className="table-actions">
                        <Link to={`/staff/menu/categories/${category.id}`}>Edit</Link>
                        {!category.isDeleted ? (
                          <>
                            <button
                              disabled={visibility.isPending}
                              onClick={() => visibility.mutate(category)}
                              type="button"
                            >
                              {category.isVisible ? "Hide" : "Show"}
                            </button>
                            <button
                              onClick={() =>
                                setPendingAction({ category, action: "delete" })
                              }
                              type="button"
                            >
                              Delete
                            </button>
                          </>
                        ) : (
                          <button
                            onClick={() =>
                              setPendingAction({ category, action: "restore" })
                            }
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
            page={categories.data.page}
            totalPages={categories.data.totalPages}
          />
        </>
      ) : (
        <div className="empty-state">
          <h2>{search ? "No search results" : "No categories yet"}</h2>
          <p>Adjust the filters or create the first category.</p>
        </div>
      )}
      <ConfirmDialog
        confirmLabel={pendingAction?.action === "restore" ? "Restore" : "Delete"}
        description={
          pendingAction?.action === "restore"
            ? "The category will return, but its deleted products will remain deleted."
            : "The category will be soft-deleted and hidden from the public menu."
        }
        destructive={pendingAction?.action === "delete"}
        onCancel={() => setPendingAction(null)}
        onConfirm={() => {
          if (!pendingAction) return;
          if (pendingAction.action === "restore") {
            restore.mutate(pendingAction.category);
          } else {
            remove.mutate(pendingAction.category);
          }
        }}
        open={pendingAction !== null}
        title={`${pendingAction?.action === "restore" ? "Restore" : "Delete"} ${pendingAction?.category.name ?? "category"}?`}
      />
    </section>
  );
}
