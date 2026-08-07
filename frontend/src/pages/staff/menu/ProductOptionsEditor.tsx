import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { getOptionGroups } from "../../../api/menu/adminOptionGroups";
import {
  addProductOptionGroup,
  addProductOptionValue,
  removeProductOptionGroup,
  removeProductOptionValue,
  updateProductOptionGroup,
  updateProductOptionValue,
  type ProductGroupInput,
  type ProductValueInput,
} from "../../../api/menu/adminProductOptions";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { useToast } from "../../../components/ToastProvider";
import type {
  AdminOptionGroup,
  AdminProduct,
  AdminProductOptionGroup,
  AdminProductOptionValue,
  Orderability,
} from "../../../types/menu";
import { formatMoney } from "../../../utils/format";

export function ProductOptionsEditor({
  product,
  onChanged,
}: {
  product: AdminProduct;
  onChanged: (orderability?: Orderability) => Promise<void>;
}) {
  const { notify } = useToast();
  const [groupId, setGroupId] = useState("");
  const globalGroups = useQuery({
    queryKey: ["admin", "option-groups", "product-editor"],
    queryFn: ({ signal }) =>
      getOptionGroups(
        { isActive: true, includeDeleted: false, pageSize: 100 },
        signal,
      ),
  });
  const availableGroups = useMemo(
    () =>
      globalGroups.data?.items.filter(
        (group) =>
          !product.optionGroups.some(
            (assignment) => assignment.optionGroupId === group.id,
          ),
      ) ?? [],
    [globalGroups.data, product.optionGroups],
  );
  const selectedGroup = availableGroups.find((group) => group.id === groupId);
  const addGroup = useMutation({
    mutationFn: (group: AdminOptionGroup) =>
      addProductOptionGroup(product.id, {
        optionGroupId: group.id,
        isRequired: group.defaultIsRequired,
        minimumSelections: group.defaultMinimumSelections,
        maximumSelections:
          group.selectionType === "Single"
            ? 1
            : (group.defaultMaximumSelections ?? Math.max(1, group.values.length)),
        displayOrder: product.optionGroups.length,
        isActive: true,
      }),
    onSuccess: async (result) => {
      notify("Option group assigned. Add only the values this product supports.");
      setGroupId("");
      await onChanged(result.orderability);
    },
  });

  return (
    <section className="panel editor-section">
      <div className="panel-heading">
        <div>
          <h2>Option configuration</h2>
          <p>
            Assign reusable groups, then configure product-specific values and
            price modifiers.
          </p>
        </div>
      </div>
      {globalGroups.isLoading ? (
        <LoadingState message="Loading option groups..." />
      ) : globalGroups.error ? (
        <ErrorState error={globalGroups.error} />
      ) : (
        <div className="inline-create">
          <label htmlFor="assign-option-group">Global option group</label>
          <select
            id="assign-option-group"
            onChange={(event) => setGroupId(event.target.value)}
            value={groupId}
          >
            <option value="">Choose a group</option>
            {availableGroups.map((group) => (
              <option key={group.id} value={group.id}>
                {group.name} ({group.selectionType})
              </option>
            ))}
          </select>
          <button
            className="button"
            disabled={!selectedGroup || addGroup.isPending}
            onClick={() => selectedGroup && addGroup.mutate(selectedGroup)}
            type="button"
          >
            {addGroup.isPending ? "Assigning..." : "Assign group"}
          </button>
        </div>
      )}
      {addGroup.error ? <ErrorState error={addGroup.error} /> : null}
      {product.optionGroups.length ? (
        <div className="option-assignment-list">
          {product.optionGroups.map((assignment) => (
            <AssignmentEditor
              assignment={assignment}
              globalGroup={globalGroups.data?.items.find(
                (group) => group.id === assignment.optionGroupId,
              )}
              key={assignment.id}
              onChanged={onChanged}
              productId={product.id}
            />
          ))}
        </div>
      ) : (
        <div className="empty-state empty-state--compact">
          <h3>No option groups assigned</h3>
          <p>A product may remain as an accepted draft while configuration is incomplete.</p>
        </div>
      )}
    </section>
  );
}

function AssignmentEditor({
  productId,
  assignment,
  globalGroup,
  onChanged,
}: {
  productId: string;
  assignment: AdminProductOptionGroup;
  globalGroup?: AdminOptionGroup;
  onChanged: (orderability?: Orderability) => Promise<void>;
}) {
  const { notify } = useToast();
  const [form, setForm] = useState<ProductGroupInput>({
    optionGroupId: assignment.optionGroupId,
    isRequired: assignment.isRequired,
    minimumSelections: assignment.minimumSelections,
    maximumSelections: assignment.maximumSelections,
    displayOrder: assignment.displayOrder,
    isActive: assignment.isActive,
  });
  const [valueId, setValueId] = useState("");
  useEffect(() => {
    setForm({
      optionGroupId: assignment.optionGroupId,
      isRequired: assignment.isRequired,
      minimumSelections: assignment.minimumSelections,
      maximumSelections: assignment.maximumSelections,
      displayOrder: assignment.displayOrder,
      isActive: assignment.isActive,
    });
  }, [assignment]);
  const unusedValues =
    globalGroup?.values.filter(
      (value) =>
        !value.isDeleted &&
        !assignment.values.some(
          (assigned) => assigned.optionValueId === value.id,
        ),
    ) ?? [];
  const update = useMutation({
    mutationFn: () =>
      updateProductOptionGroup(productId, assignment.id, {
        isRequired: form.isRequired,
        minimumSelections: form.minimumSelections,
        maximumSelections: form.maximumSelections,
        displayOrder: form.displayOrder,
        isActive: form.isActive,
        rowVersion: assignment.rowVersion,
      }),
    onSuccess: async (result) => {
      notify(`${assignment.optionGroupName} configuration saved.`);
      await onChanged(result.orderability);
    },
  });
  const remove = useMutation({
    mutationFn: () => removeProductOptionGroup(productId, assignment),
    onSuccess: async () => {
      notify(`${assignment.optionGroupName} removed from this product.`);
      await onChanged();
    },
  });
  const addValue = useMutation({
    mutationFn: (selectedValueId: string) =>
      addProductOptionValue(productId, assignment.id, {
        optionValueId: selectedValueId,
        priceModifier: 0,
        isDefault: false,
        isAvailable: true,
        displayOrder: assignment.values.length,
        volumeMilliliters: null,
        calories: null,
      }),
    onSuccess: async (result) => {
      notify("Allowed value added. Its product-specific settings are ready to edit.");
      setValueId("");
      await onChanged(result.orderability);
    },
  });
  const invalid =
    form.minimumSelections < 0 ||
    form.maximumSelections < 1 ||
    form.minimumSelections > form.maximumSelections ||
    (assignment.selectionType === "Single" && form.maximumSelections !== 1) ||
    (form.isRequired && form.minimumSelections < 1);

  return (
    <article className="option-assignment">
      <div className="panel-heading">
        <div>
          <h3>{assignment.optionGroupName}</h3>
          <p>
            {assignment.selectionType} selection
            {!assignment.optionGroupIsActive ? " - global group inactive" : ""}
            {assignment.optionGroupIsDeleted ? " - global group deleted" : ""}
          </p>
        </div>
        <button
          className="text-button text-button--danger"
          disabled={remove.isPending}
          onClick={() => remove.mutate()}
          type="button"
        >
          Remove assignment
        </button>
      </div>
      <div className="compact-form-grid">
        <label className="checkbox-field">
          <input
            checked={form.isRequired}
            onChange={(event) =>
              setForm({
                ...form,
                isRequired: event.target.checked,
                minimumSelections: event.target.checked
                  ? Math.max(1, form.minimumSelections)
                  : form.minimumSelections,
              })
            }
            type="checkbox"
          />
          Required
        </label>
        <label>
          Minimum
          <input
            min={0}
            onChange={(event) =>
              setForm({ ...form, minimumSelections: Number(event.target.value) })
            }
            type="number"
            value={form.minimumSelections}
          />
        </label>
        <label>
          Maximum
          <input
            min={1}
            onChange={(event) =>
              setForm({ ...form, maximumSelections: Number(event.target.value) })
            }
            type="number"
            value={form.maximumSelections}
          />
        </label>
        <label>
          Order
          <input
            min={0}
            onChange={(event) =>
              setForm({ ...form, displayOrder: Number(event.target.value) })
            }
            type="number"
            value={form.displayOrder}
          />
        </label>
        <label className="checkbox-field">
          <input
            checked={form.isActive}
            onChange={(event) => setForm({ ...form, isActive: event.target.checked })}
            type="checkbox"
          />
          Active for product
        </label>
      </div>
      {invalid ? (
        <p className="field-error" role="alert">
          Required groups need minimum one; minimum cannot exceed maximum; Single
          groups require maximum one.
        </p>
      ) : null}
      {update.error || remove.error ? (
        <ErrorState error={update.error ?? remove.error} />
      ) : null}
      <button
        className="button button-secondary"
        disabled={invalid || update.isPending}
        onClick={() => update.mutate()}
        type="button"
      >
        {update.isPending ? "Saving..." : "Save group settings"}
      </button>

      <div className="allowed-values">
        <div className="panel-heading">
          <div>
            <h4>Allowed values</h4>
            <p>Prices and measurements below apply only to this product.</p>
          </div>
        </div>
        <div className="inline-create">
          <label htmlFor={`value-${assignment.id}`}>Global value</label>
          <select
            id={`value-${assignment.id}`}
            onChange={(event) => setValueId(event.target.value)}
            value={valueId}
          >
            <option value="">Choose a value</option>
            {unusedValues.map((value) => (
              <option key={value.id} value={value.id}>
                {value.name}{!value.isActive ? " (inactive)" : ""}
              </option>
            ))}
          </select>
          <button
            className="button button-secondary"
            disabled={!valueId || addValue.isPending}
            onClick={() => addValue.mutate(valueId)}
            type="button"
          >
            Add value
          </button>
        </div>
        {addValue.error ? <ErrorState error={addValue.error} /> : null}
        {assignment.values.length ? (
          assignment.values.map((value) => (
            <ValueEditor
              allValues={assignment.values}
              assignment={assignment}
              key={value.id}
              onChanged={onChanged}
              productId={productId}
              value={value}
            />
          ))
        ) : (
          <p className="warning-copy">No allowed values are configured yet.</p>
        )}
      </div>
    </article>
  );
}

function ValueEditor({
  productId,
  assignment,
  value,
  allValues,
  onChanged,
}: {
  productId: string;
  assignment: AdminProductOptionGroup;
  value: AdminProductOptionValue;
  allValues: AdminProductOptionValue[];
  onChanged: (orderability?: Orderability) => Promise<void>;
}) {
  const { notify } = useToast();
  const [form, setForm] = useState<ProductValueInput>({
    optionValueId: value.optionValueId,
    priceModifier: value.priceModifier,
    isDefault: value.isDefault,
    isAvailable: value.isAvailable,
    displayOrder: value.displayOrder,
    volumeMilliliters: value.volumeMilliliters ?? null,
    calories: value.calories ?? null,
  });
  useEffect(() => {
    setForm({
      optionValueId: value.optionValueId,
      priceModifier: value.priceModifier,
      isDefault: value.isDefault,
      isAvailable: value.isAvailable,
      displayOrder: value.displayOrder,
      volumeMilliliters: value.volumeMilliliters ?? null,
      calories: value.calories ?? null,
    });
  }, [value]);
  const save = useMutation({
    mutationFn: async () => {
      if (
        assignment.selectionType === "Single" &&
        form.isDefault &&
        !value.isDefault
      ) {
        const previousDefault = allValues.find(
          (candidate) => candidate.isDefault && candidate.id !== value.id,
        );
        if (previousDefault) {
          await updateProductOptionValue(productId, previousDefault.id, {
            priceModifier: previousDefault.priceModifier,
            isDefault: false,
            isAvailable: previousDefault.isAvailable,
            displayOrder: previousDefault.displayOrder,
            volumeMilliliters: previousDefault.volumeMilliliters ?? null,
            calories: previousDefault.calories ?? null,
            rowVersion: previousDefault.rowVersion,
          });
        }
      }
      return updateProductOptionValue(productId, value.id, {
        priceModifier: form.priceModifier,
        isDefault: form.isDefault,
        isAvailable: form.isAvailable,
        displayOrder: form.displayOrder,
        volumeMilliliters: form.volumeMilliliters,
        calories: form.calories,
        rowVersion: value.rowVersion,
      });
    },
    onSuccess: async (result) => {
      notify(`${value.optionValueName} settings saved.`);
      await onChanged(result.orderability);
    },
  });
  const remove = useMutation({
    mutationFn: () => removeProductOptionValue(productId, value),
    onSuccess: async () => {
      notify(`${value.optionValueName} removed from this product.`);
      await onChanged();
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    save.mutate();
  }

  return (
    <form className="value-editor" onSubmit={submit}>
      <div>
        <strong>{value.optionValueName}</strong>
        <small>
          {formatMoney(form.priceModifier)} modifier
          {!value.optionValueIsActive ? " - global value inactive" : ""}
          {value.optionValueIsDeleted ? " - global value deleted" : ""}
        </small>
      </div>
      <label>
        Price modifier (TJS)
        <input
          onChange={(event) =>
            setForm({ ...form, priceModifier: Number(event.target.value) })
          }
          step="0.01"
          type="number"
          value={form.priceModifier}
        />
      </label>
      <label>
        Order
        <input
          min={0}
          onChange={(event) =>
            setForm({ ...form, displayOrder: Number(event.target.value) })
          }
          type="number"
          value={form.displayOrder}
        />
      </label>
      <label>
        Volume (ml)
        <input
          min={0}
          onChange={(event) =>
            setForm({
              ...form,
              volumeMilliliters: event.target.value
                ? Number(event.target.value)
                : null,
            })
          }
          type="number"
          value={form.volumeMilliliters ?? ""}
        />
      </label>
      <label>
        Calories
        <input
          min={0}
          onChange={(event) =>
            setForm({
              ...form,
              calories: event.target.value ? Number(event.target.value) : null,
            })
          }
          type="number"
          value={form.calories ?? ""}
        />
      </label>
      <label className="checkbox-field">
        <input
          checked={form.isAvailable}
          onChange={(event) => setForm({ ...form, isAvailable: event.target.checked })}
          type="checkbox"
        />
        Available
      </label>
      {assignment.selectionType === "Single" ? (
        <label className="checkbox-field">
          <input
            checked={form.isDefault}
            onChange={(event) => setForm({ ...form, isDefault: event.target.checked })}
            type="checkbox"
          />
          Default
        </label>
      ) : null}
      {save.error || remove.error ? (
        <ErrorState error={save.error ?? remove.error} />
      ) : null}
      <div className="table-actions">
        <button className="button button-secondary" disabled={save.isPending} type="submit">
          {save.isPending ? "Saving..." : "Save value"}
        </button>
        <button
          className="text-button text-button--danger"
          disabled={remove.isPending}
          onClick={() => remove.mutate()}
          type="button"
        >
          Remove
        </button>
      </div>
    </form>
  );
}
