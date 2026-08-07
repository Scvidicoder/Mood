export const menuQueryKeys = {
  publicCategories: ["public", "categories"] as const,
  publicProducts: (filters: unknown) =>
    ["public", "products", filters] as const,
  publicProduct: (id: string) => ["public", "product", id] as const,
  cartRevalidation: (productIds: readonly string[]) =>
    ["public", "cart-revalidation", [...productIds].sort()] as const,
  categories: (filters: unknown) => ["admin", "categories", filters] as const,
  category: (id: string) => ["admin", "category", id] as const,
  products: (filters: unknown) => ["admin", "products", filters] as const,
  product: (id: string) => ["admin", "product", id] as const,
  optionGroups: (filters: unknown) => ["admin", "option-groups", filters] as const,
  optionGroup: (id: string) => ["admin", "option-group", id] as const,
  optionValues: (id: string, includeDeleted: boolean) =>
    ["admin", "option-values", id, includeDeleted] as const,
  audit: (filters: unknown) => ["admin", "audit-log", filters] as const,
  auditDetail: (id: string) => ["admin", "audit-log", id] as const,
};
