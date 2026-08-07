import type {
  PagedResponse,
  PublicCategory,
  PublicProductDetail,
  PublicProductListItem,
} from "../../types/menu";
import { apiClient } from "../client";
import { queryString } from "../queryString";

const publicMenuPageSize = 100;

export interface PublicProductFilters {
  categoryId?: string;
  search?: string;
}

export function getPublicCategories(
  signal?: AbortSignal,
): Promise<PublicCategory[]> {
  return apiClient.get("categories", { signal });
}

export async function getPublicProducts(
  filters: PublicProductFilters,
  signal?: AbortSignal,
): Promise<PublicProductListItem[]> {
  const firstPage = await getPublicProductPage(filters, 1, signal);
  if (firstPage.totalPages <= 1) {
    return firstPage.items;
  }

  const remainingPages = await Promise.all(
    Array.from({ length: firstPage.totalPages - 1 }, (_, index) =>
      getPublicProductPage(filters, index + 2, signal),
    ),
  );

  return [
    ...firstPage.items,
    ...remainingPages.flatMap((page) => page.items),
  ];
}

export function getPublicProduct(
  id: string,
  signal?: AbortSignal,
): Promise<PublicProductDetail> {
  return apiClient.get(`products/${id}`, { signal });
}

function getPublicProductPage(
  filters: PublicProductFilters,
  page: number,
  signal?: AbortSignal,
): Promise<PagedResponse<PublicProductListItem>> {
  return apiClient.get(
    `products${queryString({
      categoryId: filters.categoryId,
      search: filters.search,
      page,
      pageSize: publicMenuPageSize,
    })}`,
    { signal },
  );
}
