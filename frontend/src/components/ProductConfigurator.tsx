import {
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
} from "react";
import type {
  PublicProductDetail,
  PublicProductOptionGroup,
  PublicProductOptionValue,
} from "../types/menu";
import {
  createInitialSelection,
  validateConfiguration,
  type ConfigurationIssue,
  type ConfigurationResult,
  type ProductSelection,
} from "../features/cart/configuration";
import { formatMoneyMinor, formatMoneyModifier } from "../utils/format";

interface ProductConfiguratorProps {
  product: PublicProductDetail;
  initialOptionValueIds?: readonly string[];
  onSubmit: (
    selection: ProductSelection,
    result: ConfigurationResult,
  ) => void;
  submitLabel: string;
}

export function ProductConfigurator({
  product,
  initialOptionValueIds,
  onSubmit,
  submitLabel,
}: ProductConfiguratorProps) {
  const initial = useMemo(
    () => createInitialSelection(product, initialOptionValueIds),
    [initialOptionValueIds, product],
  );
  const [selection, setSelection] = useState(initial.selection);
  const [warnings, setWarnings] = useState(initial.warnings);
  const [interactionMessage, setInteractionMessage] = useState("");
  const submittingRef = useRef(false);
  const result = useMemo(
    () => validateConfiguration(product, selection, warnings),
    [product, selection, warnings],
  );

  function changeSingle(groupId: string, valueId: string) {
    setSelection((current) => ({
      ...current,
      [groupId]: valueId ? [valueId] : [],
    }));
    clearWarningsFor(groupId);
  }

  function changeMultiple(
    group: PublicProductOptionGroup,
    valueId: string,
    checked: boolean,
  ) {
    setSelection((current) => {
      const selected = current[group.id] ?? [];
      if (checked && selected.length >= group.maximumSelections) {
        setInteractionMessage(
          `${group.name} allows up to ${group.maximumSelections} selections.`,
        );
        return current;
      }
      return {
        ...current,
        [group.id]: checked
          ? [...selected, valueId]
          : selected.filter((id) => id !== valueId),
      };
    });
    clearWarningsFor(group.id);
  }

  function clearWarningsFor(groupId: string) {
    setWarnings((current) =>
      current.filter(
        (warning) => warning.groupId && warning.groupId !== groupId,
      ),
    );
    setInteractionMessage("");
  }

  function submit() {
    if (!result.isValid || submittingRef.current) {
      return;
    }
    submittingRef.current = true;
    onSubmit(selection, result);
    window.setTimeout(() => {
      submittingRef.current = false;
    }, 500);
  }

  const globalIssues = result.issues.filter((issue) => !issue.groupId);

  return (
    <section aria-labelledby="options-title" className="product-options">
      <div className="product-options__heading">
        <div>
          <p className="eyebrow">Make it yours</p>
          <h2 id="options-title">Choose your options</h2>
        </div>
        <p>Required choices are marked below.</p>
      </div>

      {product.optionGroups.length > 0 ? (
        <div className="product-option-groups">
          {product.optionGroups.map((group) => (
            <OptionGroup
              group={group}
              issues={result.issues.filter(
                (issue) => issue.groupId === group.id,
              )}
              key={group.id}
              onMultipleChange={changeMultiple}
              onSingleChange={changeSingle}
              selectedIds={selection[group.id] ?? []}
            />
          ))}
        </div>
      ) : (
        <div className="product-options-empty">
          <h3>No choices needed</h3>
          <p>This item is served as configured on the menu.</p>
        </div>
      )}

      <div className="configurator-summary">
        <div>
          <p className="eyebrow">Configured price</p>
          <strong aria-live="polite" className="configurator-price">
            {result.unitPriceMinor == null
              ? "Price unavailable"
              : formatMoneyMinor(result.unitPriceMinor)}
          </strong>
          <ConfiguredMetrics product={product} result={result} />
        </div>
        <button
          className="button configurator-submit"
          disabled={!result.isValid}
          onClick={submit}
          type="button"
        >
          {submitLabel}
        </button>
      </div>

      {interactionMessage ? (
        <p aria-live="polite" className="configuration-limit-message">
          {interactionMessage}
        </p>
      ) : null}
      {globalIssues.length > 0 ? (
        <ConfigurationIssues issues={globalIssues} />
      ) : null}
    </section>
  );
}

function OptionGroup({
  group,
  issues,
  onMultipleChange,
  onSingleChange,
  selectedIds,
}: {
  group: PublicProductOptionGroup;
  issues: ConfigurationIssue[];
  onMultipleChange: (
    group: PublicProductOptionGroup,
    valueId: string,
    checked: boolean,
  ) => void;
  onSingleChange: (groupId: string, valueId: string) => void;
  selectedIds: readonly string[];
}) {
  const selected = new Set(selectedIds);
  const atMaximum = selected.size >= group.maximumSelections;
  const descriptionId = `option-group-help-${group.id}`;
  const errorId = `option-group-error-${group.id}`;

  return (
    <fieldset
      aria-describedby={`${descriptionId}${issues.length > 0 ? ` ${errorId}` : ""}`}
      className="product-option-group"
    >
      <legend>{group.name}</legend>
      <div className="product-option-group__heading">
        <div>
          {group.description ? <p>{group.description}</p> : null}
        </div>
        <span id={descriptionId}>
          {group.isRequired ? "Required" : "Optional"} -{" "}
          {selectionSummary(group)}
        </span>
      </div>
      <div className="product-option-values">
        {group.selectionType === "Single" &&
        group.minimumSelections === 0 ? (
          <label className="product-option-value">
            <input
              checked={selected.size === 0}
              name={`option-group-${group.id}`}
              onChange={() => onSingleChange(group.id, "")}
              type="radio"
              value=""
            />
            <span className="product-option-value__copy">
              <strong>No option</strong>
            </span>
            <span className="product-option-value__meta">Included</span>
          </label>
        ) : null}
        {group.values.map((value) => {
          const isSelected = selected.has(value.optionValueId);
          const selectionBlocked =
            group.selectionType === "Multiple" &&
            atMaximum &&
            !isSelected;
          return (
            <OptionValue
              group={group}
              isSelected={isSelected}
              key={value.id}
              onChange={(event) => {
                if (group.selectionType === "Single") {
                  onSingleChange(group.id, value.optionValueId);
                } else {
                  onMultipleChange(
                    group,
                    value.optionValueId,
                    event.target.checked,
                  );
                }
              }}
              selectionBlocked={selectionBlocked}
              value={value}
            />
          );
        })}
      </div>
      {atMaximum && group.selectionType === "Multiple" ? (
        <p className="configuration-group-hint">
          Maximum {group.maximumSelections} selected. Deselect one to choose
          another.
        </p>
      ) : null}
      {issues.length > 0 ? (
        <div id={errorId}>
          <ConfigurationIssues issues={issues} />
        </div>
      ) : null}
    </fieldset>
  );
}

function OptionValue({
  group,
  isSelected,
  onChange,
  selectionBlocked,
  value,
}: {
  group: PublicProductOptionGroup;
  isSelected: boolean;
  onChange: (event: ChangeEvent<HTMLInputElement>) => void;
  selectionBlocked: boolean;
  value: PublicProductOptionValue;
}) {
  const disabled = !value.isAvailable || selectionBlocked;
  return (
    <label
      className={`product-option-value ${
        !value.isAvailable ? "product-option-value--unavailable" : ""
      }`}
    >
      <input
        checked={isSelected}
        disabled={disabled}
        name={`option-group-${group.id}`}
        onChange={onChange}
        type={group.selectionType === "Single" ? "radio" : "checkbox"}
        value={value.optionValueId}
      />
      <span className="product-option-value__copy">
        <span>
          <strong>{value.name}</strong>
          {value.isDefault ? (
            <span className="menu-status menu-status--default">Default</span>
          ) : null}
          {!value.isAvailable ? (
            <span className="menu-status menu-status--unavailable">
              Unavailable
            </span>
          ) : null}
        </span>
        {value.description ? <small>{value.description}</small> : null}
      </span>
      <span className="product-option-value__meta">
        <strong>{formatMoneyModifier(value.priceModifier)}</strong>
        {value.calories != null ? <span>{value.calories} kcal</span> : null}
        {value.volumeMilliliters != null ? (
          <span>{value.volumeMilliliters} ml</span>
        ) : null}
      </span>
    </label>
  );
}

function ConfigurationIssues({
  issues,
}: {
  issues: readonly ConfigurationIssue[];
}) {
  return (
    <ul
      aria-live="polite"
      className="configuration-issues"
      role="alert"
    >
      {issues.map((issue, index) => (
        <li key={`${issue.code}-${index}`}>{issue.message}</li>
      ))}
    </ul>
  );
}

function ConfiguredMetrics({
  product,
  result,
}: {
  product: PublicProductDetail;
  result: ConfigurationResult;
}) {
  return (
    <span className="configured-metrics">
      {result.calories != null ? `${result.calories} kcal` : null}
      {result.volumeMilliliters != null
        ? `${result.volumeMilliliters} ml`
        : null}
      {product.weightGrams != null ? `${product.weightGrams} g` : null}
    </span>
  );
}

function selectionSummary(group: PublicProductOptionGroup): string {
  if (group.selectionType === "Single") {
    return group.minimumSelections === 0 ? "choose up to one" : "choose one";
  }
  if (group.minimumSelections === group.maximumSelections) {
    return `choose ${group.maximumSelections}`;
  }
  if (group.minimumSelections > 0) {
    return `choose ${group.minimumSelections}-${group.maximumSelections}`;
  }
  return `choose up to ${group.maximumSelections}`;
}
