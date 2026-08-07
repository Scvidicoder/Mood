import type { QueryClient } from "@tanstack/react-query";
import { ApiError } from "../../api/client";
import { getPublicProduct } from "../../api/menu/publicMenu";
import { menuQueryKeys } from "../menu/queryKeys";
import type { CartLine, CartLineRevalidation } from "../../types/cart";
import type { PublicProductDetail } from "../../types/menu";
import {
  buildCartLine,
  validateConfiguration,
  type ConfigurationIssue,
  type ProductSelection,
} from "./configuration";

const revalidationConcurrency = 4;

export type ProductRevalidationResult =
  | { kind: "found"; product: PublicProductDetail }
  | { kind: "missing" }
  | { kind: "unverified" };

export async function loadCartProducts(
  queryClient: QueryClient,
  productIds: readonly string[],
): Promise<Record<string, ProductRevalidationResult>> {
  const uniqueIds = [...new Set(productIds)].sort();
  const results: Record<string, ProductRevalidationResult> = {};
  let nextIndex = 0;

  async function worker() {
    while (nextIndex < uniqueIds.length) {
      const id = uniqueIds[nextIndex];
      nextIndex += 1;
      try {
        const product = await queryClient.fetchQuery({
          queryKey: menuQueryKeys.publicProduct(id),
          queryFn: ({ signal }) => getPublicProduct(id, signal),
          staleTime: 15_000,
          retry: false,
        });
        results[id] = { kind: "found", product };
      } catch (error) {
        results[id] =
          error instanceof ApiError && error.status === 404
            ? { kind: "missing" }
            : { kind: "unverified" };
      }
    }
  }

  await Promise.all(
    Array.from(
      { length: Math.min(revalidationConcurrency, uniqueIds.length) },
      () => worker(),
    ),
  );
  return results;
}

export function revalidateCartLine(
  line: CartLine,
  result: ProductRevalidationResult,
  now = new Date().toISOString(),
): CartLineRevalidation {
  if (result.kind === "missing") {
    return {
      lineId: line.id,
      state: "unavailable",
      messages: [
        "This product is no longer on the public menu. Remove it or choose another item.",
      ],
    };
  }
  if (result.kind === "unverified") {
    return {
      lineId: line.id,
      state: "needsAttention",
      messages: [
        "We could not check this item against the current menu. Reconnect and try again before continuing.",
      ],
    };
  }

  const product = result.product;
  if (!product.isAvailable) {
    return {
      lineId: line.id,
      state: "unavailable",
      messages: [
        "This product is currently unavailable. It remains in your cart so you can review or remove it.",
      ],
    };
  }

  const currentGroups = new Map(
    product.optionGroups.map((group) => [group.id, group]),
  );
  const selection: ProductSelection = Object.fromEntries(
    product.optionGroups.map((group) => [group.id, []]),
  );
  const staleIssues: ConfigurationIssue[] = [];

  for (const option of line.selectedOptions) {
    const group = currentGroups.get(option.groupId);
    if (!group) {
      staleIssues.push({
        code: "OPTION_GROUP_REMOVED",
        message: `${option.groupName} is no longer available for this product.`,
      });
      continue;
    }
    const value = group.values.find(
      (candidate) => candidate.optionValueId === option.optionValueId,
    );
    if (!value) {
      staleIssues.push({
        code: "OPTION_VALUE_REMOVED",
        message: `${option.valueName} is no longer available for ${group.name}.`,
        groupId: group.id,
      });
      continue;
    }
    selection[group.id].push(option.optionValueId);
  }

  const validation = validateConfiguration(
    product,
    selection,
    staleIssues,
  );
  if (!validation.isValid) {
    return {
      lineId: line.id,
      state: "needsAttention",
      messages: validation.issues.map((issue) => issue.message),
    };
  }

  const refreshedLine = buildCartLine(product, selection, validation, {
    id: line.id,
    quantity: line.quantity,
    createdAt: line.createdAt,
    now,
  });
  const updates = describeSnapshotUpdates(line, refreshedLine);

  return {
    lineId: line.id,
    state: updates.length > 0 ? "updated" : "valid",
    messages:
      updates.length > 0
        ? updates
        : [],
    refreshedLine,
  };
}

function describeSnapshotUpdates(
  previous: CartLine,
  current: CartLine,
): string[] {
  const messages: string[] = [];
  if (previous.unitPriceMinor !== current.unitPriceMinor) {
    messages.push("The current price changed and your cart was updated.");
  }
  if (previous.productName !== current.productName) {
    messages.push("The product name changed and your cart was updated.");
  }
  if (
    previous.imageUrl != null &&
    previous.imageUrl !== current.imageUrl
  ) {
    messages.push("The product image changed.");
  }

  const previousOptions = previous.selectedOptions
    .map(optionSnapshot)
    .sort();
  const currentOptions = current.selectedOptions.map(optionSnapshot).sort();
  if (previousOptions.join("|") !== currentOptions.join("|")) {
    messages.push("Option names or price details changed and were refreshed.");
  }
  return messages;
}

function optionSnapshot(option: CartLine["selectedOptions"][number]): string {
  return [
    option.groupId,
    option.groupName,
    option.optionValueId,
    option.valueName,
    option.priceModifierMinor,
    option.volumeMilliliters ?? "",
    option.calories ?? "",
  ].join(":");
}
