import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  createCategory,
  getAdminCategory,
  updateCategory,
} from "../../../api/menu/adminCategories";
import { ApiError } from "../../../api/client";
import { ConflictNotice } from "../../../components/ConflictNotice";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { useToast } from "../../../components/ToastProvider";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import { useUnsavedChanges } from "../../../hooks/useUnsavedChanges";
import type { AdminCategory, CategoryInput } from "../../../types/menu";
import { fieldError, isConcurrencyConflict } from "../../../utils/apiErrors";
import { formatDate } from "../../../utils/format";

const emptyForm: CategoryInput = {
  name: "",
  description: null,
  displayOrder: 0,
  isVisible: true,
};

export function CategoryFormPage() {
  const { id } = useParams();
  const editing = Boolean(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { notify } = useToast();
  const [form, setForm] = useState<CategoryInput>(emptyForm);
  const [rowVersion, setRowVersion] = useState("");
  const [isDirty, setIsDirty] = useState(false);
  const [conflict, setConflict] = useState(false);
  useUnsavedChanges(isDirty);
  const category = useQuery({
    queryKey: menuQueryKeys.category(id ?? "new"),
    queryFn: ({ signal }) => getAdminCategory(id!, signal),
    enabled: editing,
  });

  const resetFromCategory = (value: AdminCategory) => {
    setForm({
      name: value.name,
      description: value.description ?? null,
      displayOrder: value.displayOrder,
      isVisible: value.isVisible,
    });
    setRowVersion(value.rowVersion);
    setIsDirty(false);
    setConflict(false);
  };

  useEffect(() => {
    if (category.data && !isDirty) {
      resetFromCategory(category.data);
    }
  }, [category.data, isDirty]);

  const mutation = useMutation({
    mutationFn: () => {
      const input = {
        ...form,
        name: form.name.trim(),
        description: form.description?.trim() || null,
      };
      return editing
        ? updateCategory(id!, { ...input, rowVersion })
        : createCategory(input);
    },
    onSuccess: (saved) => {
      queryClient.setQueryData(menuQueryKeys.category(saved.id), saved);
      void queryClient.invalidateQueries({ queryKey: ["admin", "categories"] });
      resetFromCategory(saved);
      notify(editing ? "Category saved." : "Category created.");
      if (!editing) {
        navigate(`/staff/menu/categories/${saved.id}`, { replace: true });
      }
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        setConflict(true);
      }
    },
  });
  const validation = useMemo(() => {
    const errors: Record<string, string> = {};
    if (!form.name.trim()) errors.name = "Name is required.";
    if (form.name.trim().length > 120) errors.name = "Use 120 characters or fewer.";
    if ((form.description?.length ?? 0) > 500) {
      errors.description = "Use 500 characters or fewer.";
    }
    if (form.displayOrder < 0) errors.displayOrder = "Order cannot be negative.";
    return errors;
  }, [form]);

  function submit(event: FormEvent) {
    event.preventDefault();
    if (Object.keys(validation).length === 0) mutation.mutate();
  }

  async function reloadLatest() {
    if (!id) return;
    const result = await category.refetch();
    if (result.data) resetFromCategory(result.data);
  }

  if (editing && category.isLoading) {
    return <LoadingState message="Loading category…" />;
  }
  if (editing && category.error) {
    return (
      <div>
        <ErrorState error={category.error} />
        <button className="button" onClick={() => void category.refetch()} type="button">
          Retry
        </button>
      </div>
    );
  }

  const backendError = mutation.error instanceof ApiError ? mutation.error : null;

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Categories</p>
          <h1>{editing ? `Edit ${category.data?.name ?? "category"}` : "New category"}</h1>
          <p>Category names are trimmed but intentionally not globally unique.</p>
        </div>
        <Link className="button button-secondary button-link" to="/staff/menu/categories">
          Back to categories
        </Link>
      </div>
      {conflict ? (
        <ConflictNotice
          onDiscard={() => category.data && resetFromCategory(category.data)}
          onReload={() => void reloadLatest()}
        />
      ) : null}
      {category.data?.isDeleted ? (
        <div className="notice notice--warning">
          This category is deleted. Restore it from the categories list before editing.
        </div>
      ) : null}
      <form className="panel form-grid form-grid--wide" onSubmit={submit}>
        <label htmlFor="category-name">Name</label>
        <input
          aria-describedby="category-name-error"
          disabled={category.data?.isDeleted}
          id="category-name"
          maxLength={120}
          onChange={(event) => {
            setForm({ ...form, name: event.target.value });
            setIsDirty(true);
          }}
          value={form.name}
        />
        <FieldError
          id="category-name-error"
          message={validation.name || fieldError(mutation.error, "name")}
        />
        <label htmlFor="category-description">Description</label>
        <textarea
          aria-describedby="category-description-error"
          disabled={category.data?.isDeleted}
          id="category-description"
          maxLength={500}
          onChange={(event) => {
            setForm({ ...form, description: event.target.value });
            setIsDirty(true);
          }}
          rows={5}
          value={form.description ?? ""}
        />
        <FieldError
          id="category-description-error"
          message={
            validation.description || fieldError(mutation.error, "description")
          }
        />
        <label htmlFor="category-order">Display order</label>
        <input
          aria-describedby="category-order-error"
          disabled={category.data?.isDeleted}
          id="category-order"
          min={0}
          onChange={(event) => {
            setForm({ ...form, displayOrder: Number(event.target.value) });
            setIsDirty(true);
          }}
          type="number"
          value={form.displayOrder}
        />
        <FieldError
          id="category-order-error"
          message={
            validation.displayOrder || fieldError(mutation.error, "displayOrder")
          }
        />
        <label className="checkbox-field">
          <input
            checked={form.isVisible}
            disabled={category.data?.isDeleted}
            onChange={(event) => {
              setForm({ ...form, isVisible: event.target.checked });
              setIsDirty(true);
            }}
            type="checkbox"
          />
          Visible on the public menu
        </label>
        {backendError && !conflict ? <ErrorState error={backendError} /> : null}
        <div className="form-actions">
          <button
            className="button"
            disabled={
              mutation.isPending ||
              Boolean(category.data?.isDeleted) ||
              Object.keys(validation).length > 0
            }
            type="submit"
          >
            {mutation.isPending ? "Saving…" : "Save category"}
          </button>
          <Link className="button button-secondary button-link" to="/staff/menu/categories">
            Cancel
          </Link>
        </div>
      </form>
      {category.data ? (
        <p className="metadata-copy">
          Created {formatDate(category.data.createdAt)} · Updated{" "}
          {formatDate(category.data.updatedAt)}
        </p>
      ) : null}
    </section>
  );
}

function FieldError({ id, message }: { id: string; message?: string }) {
  return message ? (
    <span className="field-error" id={id} role="alert">
      {message}
    </span>
  ) : null;
}
