import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError } from "../../../api/client";
import {
  createOptionGroup,
  getOptionGroup,
  updateOptionGroup,
} from "../../../api/menu/adminOptionGroups";
import {
  createOptionValue,
  deleteOptionValue,
  getOptionValues,
  restoreOptionValue,
  setOptionValueActive,
  updateOptionValue,
} from "../../../api/menu/adminOptionValues";
import { ConflictNotice } from "../../../components/ConflictNotice";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { useToast } from "../../../components/ToastProvider";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import { useUnsavedChanges } from "../../../hooks/useUnsavedChanges";
import type {
  AdminOptionGroup,
  AdminOptionValue,
  OptionGroupInput,
  OptionValueInput,
} from "../../../types/menu";
import { fieldError, isConcurrencyConflict } from "../../../utils/apiErrors";
import { formatDate } from "../../../utils/format";

const emptyGroup: OptionGroupInput = {
  name: "",
  description: null,
  selectionType: "Single",
  defaultIsRequired: false,
  defaultMinimumSelections: 0,
  defaultMaximumSelections: 1,
  displayOrder: 0,
  isActive: true,
};

export function OptionGroupFormPage() {
  const { id } = useParams();
  const editing = Boolean(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { notify } = useToast();
  const [form, setForm] = useState<OptionGroupInput>(emptyGroup);
  const [rowVersion, setRowVersion] = useState("");
  const [dirty, setDirty] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [includeDeletedValues, setIncludeDeletedValues] = useState(false);
  useUnsavedChanges(dirty);
  const group = useQuery({
    queryKey: menuQueryKeys.optionGroup(id ?? "new"),
    queryFn: ({ signal }) => getOptionGroup(id!, signal),
    enabled: editing,
  });
  const values = useQuery({
    queryKey: menuQueryKeys.optionValues(id ?? "new", includeDeletedValues),
    queryFn: ({ signal }) => getOptionValues(id!, includeDeletedValues, signal),
    enabled: editing,
  });

  function reset(value: AdminOptionGroup) {
    setForm({
      name: value.name,
      description: value.description ?? null,
      selectionType: value.selectionType,
      defaultIsRequired: value.defaultIsRequired,
      defaultMinimumSelections: value.defaultMinimumSelections,
      defaultMaximumSelections: value.defaultMaximumSelections ?? null,
      displayOrder: value.displayOrder,
      isActive: value.isActive,
    });
    setRowVersion(value.rowVersion);
    setDirty(false);
    setConflict(false);
  }

  useEffect(() => {
    if (group.data && !dirty) reset(group.data);
  }, [dirty, group.data]);

  const validation = useMemo(() => {
    const errors: Record<string, string> = {};
    if (!form.name.trim()) errors.name = "Name is required.";
    if (form.name.trim().length > 120) errors.name = "Use 120 characters or fewer.";
    if ((form.description?.length ?? 0) > 500) {
      errors.description = "Use 500 characters or fewer.";
    }
    if (form.defaultMinimumSelections < 0) {
      errors.defaultMinimumSelections = "Minimum cannot be negative.";
    }
    if (form.defaultIsRequired && form.defaultMinimumSelections < 1) {
      errors.defaultMinimumSelections = "Required groups need a minimum of at least one.";
    }
    if (
      form.defaultMaximumSelections !== null &&
      form.defaultMaximumSelections < 1
    ) {
      errors.defaultMaximumSelections = "Maximum must be at least one.";
    }
    if (
      form.defaultMaximumSelections !== null &&
      form.defaultMinimumSelections > form.defaultMaximumSelections
    ) {
      errors.defaultMaximumSelections = "Maximum cannot be less than minimum.";
    }
    if (form.selectionType === "Single" && form.defaultMaximumSelections !== 1) {
      errors.defaultMaximumSelections = "Single selection groups require maximum one.";
    }
    if (form.displayOrder < 0) errors.displayOrder = "Order cannot be negative.";
    return errors;
  }, [form]);

  const save = useMutation({
    mutationFn: () => {
      const input = {
        ...form,
        name: form.name.trim(),
        description: form.description?.trim() || null,
      };
      return editing
        ? updateOptionGroup(id!, { ...input, rowVersion })
        : createOptionGroup(input);
    },
    onSuccess: (saved) => {
      queryClient.setQueryData(menuQueryKeys.optionGroup(saved.id), saved);
      void queryClient.invalidateQueries({ queryKey: ["admin", "option-groups"] });
      reset(saved);
      notify(editing ? "Option group saved." : "Option group created.");
      if (!editing) {
        navigate(`/staff/menu/option-groups/${saved.id}`, { replace: true });
      }
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) setConflict(true);
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!Object.keys(validation).length) save.mutate();
  }

  if (editing && group.isLoading) return <LoadingState message="Loading option group..." />;
  if (editing && group.error) {
    return (
      <div>
        <ErrorState error={group.error} />
        <button className="button" onClick={() => void group.refetch()} type="button">
          Retry
        </button>
      </div>
    );
  }
  const backendError = save.error instanceof ApiError ? save.error : null;

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Option groups</p>
          <h1>{editing ? `Edit ${group.data?.name ?? "group"}` : "New option group"}</h1>
          <p>Global values are reusable; product-specific modifiers are configured on products.</p>
        </div>
        <Link className="button button-secondary button-link" to="/staff/menu/option-groups">
          Back to option groups
        </Link>
      </div>
      {conflict ? (
        <ConflictNotice
          onDiscard={() => group.data && reset(group.data)}
          onReload={async () => {
            const latest = await group.refetch();
            if (latest.data) reset(latest.data);
          }}
        />
      ) : null}
      {group.data?.isDeleted ? (
        <div className="notice notice--warning">
          This option group is deleted. Restore it from the option-group list before editing.
        </div>
      ) : null}
      <form className="panel form-grid form-grid--wide" onSubmit={submit}>
        <label htmlFor="group-name">Name</label>
        <input
          disabled={group.data?.isDeleted}
          id="group-name"
          maxLength={120}
          onChange={(event) => {
            setForm({ ...form, name: event.target.value });
            setDirty(true);
          }}
          value={form.name}
        />
        <FieldError message={validation.name || fieldError(save.error, "name")} />
        <label htmlFor="group-description">Description</label>
        <textarea
          disabled={group.data?.isDeleted}
          id="group-description"
          maxLength={500}
          onChange={(event) => {
            setForm({ ...form, description: event.target.value });
            setDirty(true);
          }}
          rows={4}
          value={form.description ?? ""}
        />
        <FieldError message={validation.description || fieldError(save.error, "description")} />
        <label htmlFor="selection-type">Selection type</label>
        <select
          disabled={group.data?.isDeleted}
          id="selection-type"
          onChange={(event) => {
            const selectionType = event.target.value as "Single" | "Multiple";
            setForm({
              ...form,
              selectionType,
              defaultMaximumSelections:
                selectionType === "Single" ? 1 : form.defaultMaximumSelections,
            });
            setDirty(true);
          }}
          value={form.selectionType}
        >
          <option value="Single">Single</option>
          <option value="Multiple">Multiple</option>
        </select>
        <label className="checkbox-field">
          <input
            checked={form.defaultIsRequired}
            disabled={group.data?.isDeleted}
            onChange={(event) => {
              setForm({
                ...form,
                defaultIsRequired: event.target.checked,
                defaultMinimumSelections: event.target.checked
                  ? Math.max(1, form.defaultMinimumSelections)
                  : form.defaultMinimumSelections,
              });
              setDirty(true);
            }}
            type="checkbox"
          />
          Required by default
        </label>
        <label htmlFor="group-min">Default minimum selections</label>
        <input
          disabled={group.data?.isDeleted}
          id="group-min"
          min={0}
          onChange={(event) => {
            setForm({ ...form, defaultMinimumSelections: Number(event.target.value) });
            setDirty(true);
          }}
          type="number"
          value={form.defaultMinimumSelections}
        />
        <FieldError
          message={
            validation.defaultMinimumSelections ||
            fieldError(save.error, "defaultMinimumSelections")
          }
        />
        <label htmlFor="group-max">Default maximum selections</label>
        <input
          disabled={group.data?.isDeleted || form.selectionType === "Single"}
          id="group-max"
          min={1}
          onChange={(event) => {
            setForm({
              ...form,
              defaultMaximumSelections: event.target.value
                ? Number(event.target.value)
                : null,
            });
            setDirty(true);
          }}
          type="number"
          value={form.defaultMaximumSelections ?? ""}
        />
        <FieldError
          message={
            validation.defaultMaximumSelections ||
            fieldError(save.error, "defaultMaximumSelections")
          }
        />
        <label htmlFor="group-order">Display order</label>
        <input
          disabled={group.data?.isDeleted}
          id="group-order"
          min={0}
          onChange={(event) => {
            setForm({ ...form, displayOrder: Number(event.target.value) });
            setDirty(true);
          }}
          type="number"
          value={form.displayOrder}
        />
        <FieldError message={validation.displayOrder || fieldError(save.error, "displayOrder")} />
        <label className="checkbox-field">
          <input
            checked={form.isActive}
            disabled={group.data?.isDeleted}
            onChange={(event) => {
              setForm({ ...form, isActive: event.target.checked });
              setDirty(true);
            }}
            type="checkbox"
          />
          Active
        </label>
        {backendError && !conflict ? <ErrorState error={backendError} /> : null}
        <div className="form-actions">
          <button
            className="button"
            disabled={
              save.isPending ||
              Boolean(group.data?.isDeleted) ||
              Object.keys(validation).length > 0
            }
            type="submit"
          >
            {save.isPending ? "Saving..." : "Save option group"}
          </button>
          <span aria-live="polite">{dirty ? "Unsaved changes" : "All changes saved"}</span>
        </div>
      </form>
      {editing && id ? (
        <ValuesManager
          groupId={id}
          includeDeleted={includeDeletedValues}
          onIncludeDeleted={setIncludeDeletedValues}
          query={values}
        />
      ) : (
        <div className="notice">Save the group before adding global values.</div>
      )}
      {group.data ? (
        <p className="metadata-copy">
          Created {formatDate(group.data.createdAt)} - Updated {formatDate(group.data.updatedAt)}
        </p>
      ) : null}
    </section>
  );
}

function ValuesManager({
  groupId,
  includeDeleted,
  onIncludeDeleted,
  query,
}: {
  groupId: string;
  includeDeleted: boolean;
  onIncludeDeleted: (value: boolean) => void;
  query: ReturnType<typeof useQuery<AdminOptionValue[]>>;
}) {
  const queryClient = useQueryClient();
  const { notify } = useToast();
  const [newValue, setNewValue] = useState<OptionValueInput>({
    name: "",
    description: null,
    displayOrder: 0,
    isActive: true,
  });
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ["admin", "option-values", groupId] });
  const create = useMutation({
    mutationFn: () =>
      createOptionValue(groupId, {
        ...newValue,
        name: newValue.name.trim(),
        description: newValue.description?.trim() || null,
      }),
    onSuccess: () => {
      notify("Global option value created.");
      setNewValue({ name: "", description: null, displayOrder: 0, isActive: true });
      void invalidate();
      void queryClient.invalidateQueries({ queryKey: ["admin", "option-groups"] });
    },
  });

  return (
    <section className="panel editor-section panel--spaced">
      <div className="panel-heading">
        <div>
          <h2>Global values</h2>
          <p>These values are reusable. Product prices and measurements are not set here.</p>
        </div>
        <label className="checkbox-field">
          <input
            checked={includeDeleted}
            onChange={(event) => onIncludeDeleted(event.target.checked)}
            type="checkbox"
          />
          Include deleted
        </label>
      </div>
      <form
        className="inline-value-form"
        onSubmit={(event) => {
          event.preventDefault();
          if (newValue.name.trim()) create.mutate();
        }}
      >
        <label>
          Name
          <input
            maxLength={120}
            onChange={(event) => setNewValue({ ...newValue, name: event.target.value })}
            required
            value={newValue.name}
          />
        </label>
        <label>
          Description
          <input
            maxLength={500}
            onChange={(event) =>
              setNewValue({ ...newValue, description: event.target.value })
            }
            value={newValue.description ?? ""}
          />
        </label>
        <label>
          Display order
          <input
            min={0}
            onChange={(event) =>
              setNewValue({ ...newValue, displayOrder: Number(event.target.value) })
            }
            type="number"
            value={newValue.displayOrder}
          />
        </label>
        <label className="checkbox-field">
          <input
            checked={newValue.isActive}
            onChange={(event) =>
              setNewValue({ ...newValue, isActive: event.target.checked })
            }
            type="checkbox"
          />
          Active
        </label>
        <button className="button" disabled={create.isPending} type="submit">
          {create.isPending ? "Adding..." : "Add value"}
        </button>
      </form>
      {create.error ? <ErrorState error={create.error} /> : null}
      {query.isLoading ? (
        <LoadingState message="Loading values..." />
      ) : query.error ? (
        <div>
          <ErrorState error={query.error} />
          <button className="button" onClick={() => void query.refetch()} type="button">
            Retry
          </button>
        </div>
      ) : query.data?.length ? (
        <div className="value-list">
          {query.data.map((value) => (
            <GlobalValueEditor
              groupId={groupId}
              key={value.id}
              onChanged={invalidate}
              value={value}
            />
          ))}
        </div>
      ) : (
        <div className="empty-state empty-state--compact">
          <h3>No global values</h3>
          <p>Create the first reusable value above.</p>
        </div>
      )}
    </section>
  );
}

function GlobalValueEditor({
  value,
  onChanged,
}: {
  groupId: string;
  value: AdminOptionValue;
  onChanged: () => Promise<unknown>;
}) {
  const { notify } = useToast();
  const [form, setForm] = useState<OptionValueInput>({
    name: value.name,
    description: value.description ?? null,
    displayOrder: value.displayOrder,
    isActive: value.isActive,
  });
  useEffect(() => {
    setForm({
      name: value.name,
      description: value.description ?? null,
      displayOrder: value.displayOrder,
      isActive: value.isActive,
    });
  }, [value]);
  const errorHandler = (error: unknown) => {
    notify(
      isConcurrencyConflict(error)
        ? "This value changed elsewhere. Reload the latest values."
        : error instanceof Error
          ? error.message
          : "The value could not be updated.",
      "error",
    );
    void onChanged();
  };
  const save = useMutation({
    mutationFn: () =>
      updateOptionValue(value.id, {
        ...form,
        name: form.name.trim(),
        description: form.description?.trim() || null,
        rowVersion: value.rowVersion,
      }),
    onSuccess: () => {
      notify(`${value.name} saved.`);
      void onChanged();
    },
    onError: errorHandler,
  });
  const active = useMutation({
    mutationFn: () => setOptionValueActive(value),
    onSuccess: () => {
      notify("Value status updated.");
      void onChanged();
    },
    onError: errorHandler,
  });
  const remove = useMutation({
    mutationFn: () => deleteOptionValue(value),
    onSuccess: () => {
      notify("Value moved to deleted items.");
      void onChanged();
    },
    onError: errorHandler,
  });
  const restore = useMutation({
    mutationFn: () => restoreOptionValue(value),
    onSuccess: () => {
      notify("Value restored.");
      void onChanged();
    },
    onError: errorHandler,
  });

  return (
    <article className="global-value-card">
      <div className="compact-form-grid">
        <label>
          Name
          <input
            disabled={value.isDeleted}
            onChange={(event) => setForm({ ...form, name: event.target.value })}
            value={form.name}
          />
        </label>
        <label>
          Description
          <input
            disabled={value.isDeleted}
            onChange={(event) =>
              setForm({ ...form, description: event.target.value })
            }
            value={form.description ?? ""}
          />
        </label>
        <label>
          Order
          <input
            disabled={value.isDeleted}
            min={0}
            onChange={(event) =>
              setForm({ ...form, displayOrder: Number(event.target.value) })
            }
            type="number"
            value={form.displayOrder}
          />
        </label>
      </div>
      <div className="badge-stack">
        <span className="status-badge">{value.isActive ? "Active" : "Inactive"}</span>
        {value.isDeleted ? (
          <span className="status-badge status-badge--error">Deleted</span>
        ) : null}
      </div>
      <div className="table-actions">
        {!value.isDeleted ? (
          <>
            <button
              className="button button-secondary"
              disabled={save.isPending || !form.name.trim()}
              onClick={() => save.mutate()}
              type="button"
            >
              Save
            </button>
            <button disabled={active.isPending} onClick={() => active.mutate()} type="button">
              {value.isActive ? "Deactivate" : "Activate"}
            </button>
            <button disabled={remove.isPending} onClick={() => remove.mutate()} type="button">
              Delete
            </button>
          </>
        ) : (
          <button disabled={restore.isPending} onClick={() => restore.mutate()} type="button">
            Restore
          </button>
        )}
      </div>
      {save.error || active.error || remove.error || restore.error ? (
        <ErrorState error={save.error ?? active.error ?? remove.error ?? restore.error} />
      ) : null}
    </article>
  );
}

function FieldError({ message }: { message?: string }) {
  return message ? (
    <span className="field-error" role="alert">{message}</span>
  ) : null;
}
