import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { getAdminCategories } from "../../../api/menu/adminCategories";
import {
  deleteProduct,
  duplicateProduct,
  getAdminProducts,
  reorderProducts,
  restoreProduct,
  setProductAvailability,
  setProductVisibility,
} from "../../../api/menu/adminProducts";
import { ConfirmDialog } from "../../../components/ConfirmDialog";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { Pagination } from "../../../components/Pagination";
import { useToast } from "../../../components/ToastProvider";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import type { AdminProductListItem } from "../../../types/menu";
import { isConcurrencyConflict } from "../../../utils/apiErrors";
import { formatDate, formatMoney } from "../../../utils/format";
import { resolveMediaUrl } from "../../../utils/mediaUrl";

type PendingAction =
  | { action: "delete" | "restore"; product: AdminProductListItem }
  | { action: "duplicate"; product: AdminProductListItem };

export function ProductsPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { notify } = useToast();
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [availability, setAvailability] = useState("");
  const [visibility, setVisibility] = useState("");
  const [includeDeleted, setIncludeDeleted] = useState(false);
  const [page, setPage] = useState(1);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [duplicateName, setDuplicateName] = useState("");
  useEffect(() => {
    if (pendingAction?.action !== "duplicate") return;
    function handleDialogKey(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setPendingAction(null);
        return;
      }
      if (event.key !== "Tab") return;
      const dialog = document.querySelector<HTMLElement>("#duplicate-dialog");
      const controls = dialog?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
      );
      if (!controls?.length) return;
      const first = controls[0];
      const last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }
    document.addEventListener("keydown", handleDialogKey);
    return () => document.removeEventListener("keydown", handleDialogKey);
  }, [pendingAction]);
  const filters = useMemo(
    () => ({
      search,
      categoryId: categoryId || undefined,
      isAvailable: availability === "" ? undefined : availability === "true",
      isVisible: visibility === "" ? undefined : visibility === "true",
      includeDeleted,
      page,
      pageSize: 20,
    }),
    [availability, categoryId, includeDeleted, page, search, visibility],
  );
  const products = useQuery({
    queryKey: menuQueryKeys.products(filters),
    queryFn: ({ signal }) => getAdminProducts(filters, signal),
  });
  const categories = useQuery({
    queryKey: ["admin", "product-filter-categories"],
    queryFn: ({ signal }) =>
      getAdminCategories({ includeDeleted: true, pageSize: 100 }, signal),
  });
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "products"] });
  const mutationError = (error: unknown) => {
    notify(
      isConcurrencyConflict(error)
        ? "This product changed elsewhere. The current list was reloaded."
        : error instanceof Error
          ? error.message
          : "The product could not be updated.",
      "error",
    );
    void invalidate();
  };
  const availabilityMutation = useMutation({
    mutationFn: setProductAvailability,
    onSuccess: () => {
      notify("Product availability updated.");
      void invalidate();
    },
    onError: mutationError,
  });
  const visibilityMutation = useMutation({
    mutationFn: setProductVisibility,
    onSuccess: () => {
      notify("Product visibility updated.");
      void invalidate();
    },
    onError: mutationError,
  });
  const deleteMutation = useMutation({
    mutationFn: deleteProduct,
    onSuccess: () => {
      notify("Product moved to deleted items.");
      setPendingAction(null);
      void invalidate();
    },
    onError: mutationError,
  });
  const restoreMutation = useMutation({
    mutationFn: restoreProduct,
    onSuccess: () => {
      notify("Product restored.");
      setPendingAction(null);
      void invalidate();
    },
    onError: mutationError,
  });
  const duplicateMutation = useMutation({
    mutationFn: ({ id, name }: { id: string; name?: string }) =>
      duplicateProduct(id, name),
    onSuccess: (result) => {
      notify("Product duplicated with its option configuration.");
      setPendingAction(null);
      setDuplicateName("");
      void invalidate();
      navigate(`/staff/menu/products/${result.resource.id}`);
    },
  });
  const reorderMutation = useMutation({
    mutationFn: (items: AdminProductListItem[]) =>
      reorderProducts(
        categoryId,
        items.map(({ id, displayOrder, rowVersion }) => ({
          id,
          displayOrder,
          rowVersion,
        })),
      ),
    onSuccess: () => {
      notify("Product order saved.");
      void invalidate();
    },
    onError: mutationError,
  });

  function move(index: number, direction: -1 | 1) {
    const current = products.data?.items;
    const target = index + direction;
    if (!categoryId || !current || target < 0 || target >= current.length) return;
    const next = current.map((product) => ({ ...product }));
    const firstOrder = next[index].displayOrder;
    next[index].displayOrder = next[target].displayOrder;
    next[target].displayOrder = firstOrder;
    [next[index], next[target]] = [next[target], next[index]];
    reorderMutation.mutate(next);
  }

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Menu</p>
          <h1>Products</h1>
          <p>Manage product details, orderability, imagery, and menu status.</p>
        </div>
        <Link className="button button-link" to="/staff/menu/products/new">
          Create product
        </Link>
      </div>
      <div className="filter-bar filter-bar--wide">
        <label>
          Search
          <input
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
            placeholder="Product name"
            type="search"
            value={search}
          />
        </label>
        <label>
          Category
          <select
            onChange={(event) => {
              setCategoryId(event.target.value);
              setPage(1);
            }}
            value={categoryId}
          >
            <option value="">All categories</option>
            {categories.data?.items.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}{category.isDeleted ? " (deleted)" : ""}
              </option>
            ))}
          </select>
        </label>
        <label>
          Availability
          <select
            onChange={(event) => {
              setAvailability(event.target.value);
              setPage(1);
            }}
            value={availability}
          >
            <option value="">Any</option>
            <option value="true">Available</option>
            <option value="false">Unavailable</option>
          </select>
        </label>
        <label>
          Visibility
          <select
            onChange={(event) => {
              setVisibility(event.target.value);
              setPage(1);
            }}
            value={visibility}
          >
            <option value="">Any</option>
            <option value="true">Visible</option>
            <option value="false">Hidden</option>
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
      {!categoryId ? (
        <p className="hint-copy">Select one category to enable product reordering.</p>
      ) : null}
      {products.isLoading ? (
        <LoadingState message="Loading products..." />
      ) : products.error ? (
        <div>
          <ErrorState error={products.error} />
          <button className="button" onClick={() => void products.refetch()} type="button">
            Retry
          </button>
        </div>
      ) : products.data?.items.length ? (
        <>
          <div className="responsive-table-wrap">
            <table className="admin-table admin-table--products">
              <thead>
                <tr>
                  <th scope="col">Order</th>
                  <th scope="col">Product</th>
                  <th scope="col">Category</th>
                  <th scope="col">Price</th>
                  <th scope="col">Status</th>
                  <th scope="col">Updated</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {products.data.items.map((product, index) => (
                  <tr key={product.id}>
                    <td data-label="Order">
                      {product.displayOrder}
                      {categoryId ? (
                        <div className="reorder-controls" aria-label={`Reorder ${product.name}`}>
                          <button
                            aria-label={`Move ${product.name} up`}
                            disabled={index === 0 || reorderMutation.isPending}
                            onClick={() => move(index, -1)}
                            type="button"
                          >
                            Up
                          </button>
                          <button
                            aria-label={`Move ${product.name} down`}
                            disabled={
                              index === products.data.items.length - 1 ||
                              reorderMutation.isPending
                            }
                            onClick={() => move(index, 1)}
                            type="button"
                          >
                            Down
                          </button>
                        </div>
                      ) : null}
                    </td>
                    <td data-label="Product">
                      <div className="product-cell">
                        {product.imageUrl ? (
                          <img
                            alt=""
                            loading="lazy"
                            src={resolveMediaUrl(product.imageUrl) ?? undefined}
                          />
                        ) : (
                          <span aria-hidden="true" className="image-placeholder">No image</span>
                        )}
                        <div>
                          <strong>{product.name}</strong>
                          {!product.isOrderable ? (
                            <small className="warning-copy">
                              {product.availabilityIssues[0]?.message ??
                                "Configuration incomplete"}
                            </small>
                          ) : (
                            <small>Orderable</small>
                          )}
                        </div>
                      </div>
                    </td>
                    <td data-label="Category">{product.categoryName}</td>
                    <td data-label="Price">{formatMoney(product.basePrice)}</td>
                    <td data-label="Status">
                      <div className="badge-stack">
                        <span className="status-badge">
                          {product.isAvailable ? "Available" : "Unavailable"}
                        </span>
                        <span className="status-badge">
                          {product.isVisible ? "Visible" : "Hidden"}
                        </span>
                        {product.isDeleted ? (
                          <span className="status-badge status-badge--error">Deleted</span>
                        ) : null}
                      </div>
                    </td>
                    <td data-label="Updated">{formatDate(product.updatedAt)}</td>
                    <td data-label="Actions">
                      <div className="table-actions">
                        <Link to={`/staff/menu/products/${product.id}`}>Edit</Link>
                        {!product.isDeleted ? (
                          <>
                            <button
                              disabled={availabilityMutation.isPending}
                              onClick={() => availabilityMutation.mutate(product)}
                              type="button"
                            >
                              {product.isAvailable ? "Make unavailable" : "Make available"}
                            </button>
                            <button
                              disabled={visibilityMutation.isPending}
                              onClick={() => visibilityMutation.mutate(product)}
                              type="button"
                            >
                              {product.isVisible ? "Hide" : "Show"}
                            </button>
                            <button
                              onClick={() => {
                                setDuplicateName(`${product.name} copy`);
                                setPendingAction({ action: "duplicate", product });
                              }}
                              type="button"
                            >
                              Duplicate
                            </button>
                            <button
                              onClick={() => setPendingAction({ action: "delete", product })}
                              type="button"
                            >
                              Delete
                            </button>
                          </>
                        ) : (
                          <button
                            onClick={() => setPendingAction({ action: "restore", product })}
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
            page={products.data.page}
            totalPages={products.data.totalPages}
          />
        </>
      ) : (
        <div className="empty-state">
          <h2>{search ? "No search results" : "No products found"}</h2>
          <p>Adjust the filters or create a product.</p>
        </div>
      )}
      <ConfirmDialog
        confirmLabel={pendingAction?.action === "restore" ? "Restore" : "Delete"}
        description="This changes menu availability but preserves the audit history."
        destructive={pendingAction?.action === "delete"}
        onCancel={() => setPendingAction(null)}
        onConfirm={() => {
          if (pendingAction?.action === "delete") deleteMutation.mutate(pendingAction.product);
          if (pendingAction?.action === "restore") restoreMutation.mutate(pendingAction.product);
        }}
        open={pendingAction?.action === "delete" || pendingAction?.action === "restore"}
        title={`${pendingAction?.action === "restore" ? "Restore" : "Delete"} ${pendingAction?.product.name ?? "product"}?`}
      />
      {pendingAction?.action === "duplicate" ? (
        <div className="dialog-backdrop" role="presentation">
          <form
            aria-labelledby="duplicate-dialog-title"
            aria-modal="true"
            className="dialog"
            id="duplicate-dialog"
            onSubmit={(event) => {
              event.preventDefault();
              duplicateMutation.mutate({
                id: pendingAction.product.id,
                name: duplicateName,
              });
            }}
            role="dialog"
          >
            <h2 id="duplicate-dialog-title">Duplicate {pendingAction.product.name}?</h2>
            <p>The product and its option configuration are copied; image bytes are not.</p>
            <label htmlFor="duplicate-name">New product name</label>
            <input
              autoFocus
              id="duplicate-name"
              onChange={(event) => setDuplicateName(event.target.value)}
              required
              value={duplicateName}
            />
            {duplicateMutation.error ? <ErrorState error={duplicateMutation.error} /> : null}
            <div className="dialog-actions">
              <button
                className="button button-secondary"
                onClick={() => setPendingAction(null)}
                type="button"
              >
                Cancel
              </button>
              <button className="button" disabled={duplicateMutation.isPending} type="submit">
                {duplicateMutation.isPending ? "Duplicating..." : "Duplicate"}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </section>
  );
}
