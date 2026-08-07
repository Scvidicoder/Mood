import type {
  PublicProductDetail,
  PublicProductOptionGroup,
  PublicProductOptionValue,
} from "../../types/menu";
import {
  CART_CURRENCY,
  MAX_CART_UNIT_PRICE_MINOR,
  type CartLine,
  type CartOptionSnapshot,
} from "../../types/cart";

export type ProductSelection = Record<string, string[]>;

export interface ConfigurationIssue {
  code: string;
  message: string;
  groupId?: string;
}

export interface InitialSelection {
  selection: ProductSelection;
  warnings: ConfigurationIssue[];
}

export interface ConfigurationResult {
  isValid: boolean;
  issues: ConfigurationIssue[];
  selectedValues: PublicProductOptionValue[];
  unitPriceMinor?: number;
  volumeMilliliters?: number;
  calories?: number;
}

export function createInitialSelection(
  product: PublicProductDetail,
  prefilledOptionValueIds?: readonly string[],
): InitialSelection {
  const prefilled = prefilledOptionValueIds
    ? new Set(prefilledOptionValueIds)
    : null;
  const selection: ProductSelection = {};
  const warnings: ConfigurationIssue[] = [];

  for (const group of product.optionGroups) {
    const availableValues = group.values.filter((value) => value.isAvailable);
    const requested = prefilled
      ? availableValues.filter((value) => prefilled.has(value.optionValueId))
      : availableValues.filter((value) => value.isDefault);

    if (
      group.selectionType === "Single" &&
      requested.length > 1
    ) {
      selection[group.id] = [];
      warnings.push({
        code: "CONTRADICTORY_SINGLE_DEFAULTS",
        message: `${group.name} has multiple defaults. Choose one option to continue.`,
        groupId: group.id,
      });
      continue;
    }

    if (
      group.selectionType === "Multiple" &&
      requested.length > group.maximumSelections
    ) {
      selection[group.id] = [];
      warnings.push({
        code: "CONTRADICTORY_MULTIPLE_DEFAULTS",
        message: `${group.name} has more defaults than its selection limit. Choose the options you want.`,
        groupId: group.id,
      });
      continue;
    }

    selection[group.id] = requested.map((value) => value.optionValueId);

    if (
      !prefilled &&
      group.values.some((value) => value.isDefault && !value.isAvailable)
    ) {
      warnings.push({
        code: "DEFAULT_VALUE_UNAVAILABLE",
        message: `${group.name} has an unavailable default. Choose an available option.`,
        groupId: group.id,
      });
    }
  }

  if (prefilled) {
    const knownIds = new Set(
      product.optionGroups.flatMap((group) =>
        group.values.map((value) => value.optionValueId),
      ),
    );
    const missingCount = [...prefilled].filter((id) => !knownIds.has(id)).length;
    if (missingCount > 0) {
      warnings.push({
        code: "PREFILLED_OPTION_MISSING",
        message:
          "One or more saved options are no longer on the menu. Review this configuration.",
      });
    }
  }

  return { selection, warnings };
}

export function validateConfiguration(
  product: PublicProductDetail,
  selection: ProductSelection,
  blockingWarnings: readonly ConfigurationIssue[] = [],
): ConfigurationResult {
  const issues: ConfigurationIssue[] = [...blockingWarnings];
  const selectedValues: PublicProductOptionValue[] = [];
  const basePriceMinor = moneyToMinor(product.basePrice);

  if (product.currency !== CART_CURRENCY) {
    issues.push({
      code: "CURRENCY_MISMATCH",
      message: `This cart supports ${CART_CURRENCY}, but the product uses ${product.currency}.`,
    });
  }
  if (!product.isAvailable) {
    issues.push({
      code: "PRODUCT_UNAVAILABLE",
      message: "This product is currently unavailable.",
    });
  }
  if (!product.isOrderable) {
    issues.push(
      ...(product.availabilityIssues.length > 0
        ? product.availabilityIssues.map((issue) => ({
            code: issue.code,
            message: issue.message,
            groupId: issue.productOptionGroupId,
          }))
        : [
            {
              code: "PRODUCT_NOT_ORDERABLE",
              message: "This product cannot be ordered right now.",
            },
          ]),
    );
  }
  if (basePriceMinor == null) {
    issues.push({
      code: "INVALID_BASE_PRICE",
      message: "The current product price cannot be calculated safely.",
    });
  }

  for (const group of product.optionGroups) {
    const selectedIds = new Set(selection[group.id] ?? []);
    const valuesById = new Map(
      group.values.map((value) => [value.optionValueId, value]),
    );
    const groupValues = [...selectedIds]
      .map((id) => valuesById.get(id))
      .filter((value): value is PublicProductOptionValue => Boolean(value));

    if (selectedIds.size !== groupValues.length) {
      issues.push({
        code: "OPTION_VALUE_MISSING",
        message: `${group.name} contains an option that is no longer available.`,
        groupId: group.id,
      });
    }
    if (groupValues.some((value) => !value.isAvailable)) {
      issues.push({
        code: "OPTION_VALUE_UNAVAILABLE",
        message: `${group.name} contains an unavailable option.`,
        groupId: group.id,
      });
    }
    if (group.selectionType === "Single" && selectedIds.size > 1) {
      issues.push({
        code: "SINGLE_SELECTION_LIMIT",
        message: `Choose at most one option for ${group.name}.`,
        groupId: group.id,
      });
    }
    if (selectedIds.size < group.minimumSelections) {
      issues.push({
        code: "MINIMUM_SELECTIONS_NOT_MET",
        message: minimumMessage(group),
        groupId: group.id,
      });
    }
    if (selectedIds.size > group.maximumSelections) {
      issues.push({
        code: "MAXIMUM_SELECTIONS_EXCEEDED",
        message: `Choose no more than ${group.maximumSelections} for ${group.name}.`,
        groupId: group.id,
      });
    }
    if (group.values.length === 0 && group.minimumSelections > 0) {
      issues.push({
        code: "REQUIRED_OPTION_DATA_MISSING",
        message: `${group.name} has no selectable option data.`,
        groupId: group.id,
      });
    }

    selectedValues.push(
      ...groupValues.filter((value) => value.isAvailable),
    );
  }

  let unitPriceMinor = basePriceMinor;
  for (const value of selectedValues) {
    const modifierMinor = moneyToMinor(value.priceModifier);
    if (modifierMinor == null) {
      issues.push({
        code: "INVALID_OPTION_PRICE",
        message: `${value.name} has a price that cannot be calculated safely.`,
      });
      unitPriceMinor = undefined;
      continue;
    }
    if (unitPriceMinor != null) {
      const nextUnitPriceMinor = unitPriceMinor + modifierMinor;
      unitPriceMinor =
        Number.isSafeInteger(nextUnitPriceMinor) &&
        nextUnitPriceMinor <= MAX_CART_UNIT_PRICE_MINOR
          ? nextUnitPriceMinor
          : undefined;
    }
  }

  if (unitPriceMinor != null && unitPriceMinor < 0) {
    issues.push({
      code: "INVALID_CONFIGURED_PRICE",
      message: "The configured price cannot be calculated safely.",
    });
    unitPriceMinor = undefined;
  }

  return {
    isValid: issues.length === 0 && unitPriceMinor != null,
    issues: uniqueIssues(issues),
    selectedValues,
    unitPriceMinor,
    volumeMilliliters: configuredMetric(
      product.volumeMilliliters,
      selectedValues,
      "volumeMilliliters",
    ),
    calories: configuredMetric(
      product.calories,
      selectedValues,
      "calories",
    ),
  };
}

export function buildCartLine(
  product: PublicProductDetail,
  selection: ProductSelection,
  result: ConfigurationResult,
  options: {
    id?: string;
    quantity?: number;
    createdAt?: string;
    now?: string;
  } = {},
): CartLine {
  if (!result.isValid || result.unitPriceMinor == null) {
    throw new Error("Cannot build a cart line from an invalid configuration.");
  }

  const selectedOptions = product.optionGroups.flatMap((group) => {
    const selectedIds = new Set(selection[group.id] ?? []);
    return group.values
      .filter((value) => selectedIds.has(value.optionValueId))
      .map<CartOptionSnapshot>((value) => ({
        groupId: group.id,
        groupName: group.name,
        optionValueId: value.optionValueId,
        valueName: value.name,
        priceModifierMinor: moneyToMinor(value.priceModifier) ?? 0,
        volumeMilliliters: value.volumeMilliliters,
        calories: value.calories,
      }));
  });
  const now = options.now ?? new Date().toISOString();

  return {
    id: options.id ?? createLocalId(),
    configurationKey: createConfigurationKey(
      product.id,
      selectedOptions.map((option) => option.optionValueId),
    ),
    productId: product.id,
    productName: product.name,
    imageUrl: product.imageUrl,
    basePriceMinor: moneyToMinor(product.basePrice) ?? 0,
    currency: CART_CURRENCY,
    selectedOptions,
    quantity: options.quantity ?? 1,
    unitPriceMinor: result.unitPriceMinor,
    createdAt: options.createdAt ?? now,
    updatedAt: now,
    state: "valid",
    messages: [],
  };
}

export function createConfigurationKey(
  productId: string,
  selectedOptionValueIds: readonly string[],
): string {
  return `${productId}:${[...new Set(selectedOptionValueIds)].sort().join(",")}`;
}

export function moneyToMinor(value: number): number | undefined {
  if (!Number.isFinite(value) || value < 0) {
    return undefined;
  }
  const minor = Math.round(value * 100);
  return Number.isSafeInteger(minor) &&
    minor <= MAX_CART_UNIT_PRICE_MINOR &&
    Math.abs(value * 100 - minor) <= 0.000_001
    ? minor
    : undefined;
}

function minimumMessage(group: PublicProductOptionGroup): string {
  if (
    group.selectionType === "Single" &&
    group.minimumSelections === 1
  ) {
    return `Choose one option for ${group.name}.`;
  }
  return `Choose at least ${group.minimumSelections} for ${group.name}.`;
}

function configuredMetric(
  baseValue: number | undefined,
  selectedValues: readonly PublicProductOptionValue[],
  key: "volumeMilliliters" | "calories",
): number | undefined {
  const explicitValues = [
    ...new Set(
      selectedValues
        .map((value) => value[key])
        .filter((value): value is number => value != null),
    ),
  ];
  return explicitValues.length === 1 ? explicitValues[0] : baseValue;
}

function uniqueIssues(
  issues: readonly ConfigurationIssue[],
): ConfigurationIssue[] {
  const seen = new Set<string>();
  return issues.filter((issue) => {
    const key = `${issue.code}:${issue.groupId ?? ""}:${issue.message}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function createLocalId(): string {
  return globalThis.crypto?.randomUUID?.() ??
    `cart-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
