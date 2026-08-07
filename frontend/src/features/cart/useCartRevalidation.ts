import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo } from "react";
import { useAppDispatch, useAppSelector } from "../../store";
import { menuQueryKeys } from "../menu/queryKeys";
import { cartActions, selectCartItems } from "./cartSlice";
import { loadCartProducts, revalidateCartLine } from "./revalidation";

export function useCartRevalidation() {
  const dispatch = useAppDispatch();
  const queryClient = useQueryClient();
  const items = useAppSelector(selectCartItems);
  const productIds = useMemo(
    () => [...new Set(items.map((line) => line.productId))].sort(),
    [items],
  );
  const productKey = productIds.join(",");
  const validation = useQuery({
    queryKey: menuQueryKeys.cartRevalidation(productIds),
    queryFn: () => loadCartProducts(queryClient, productIds),
    enabled: productIds.length > 0,
    retry: false,
    staleTime: 15_000,
  });

  useEffect(() => {
    if (!validation.data) {
      return;
    }
    dispatch(
      cartActions.applyRevalidation(
        items.map((line) =>
          revalidateCartLine(
            line,
            validation.data[line.productId] ?? { kind: "unverified" },
          ),
        ),
      ),
    );
  }, [dispatch, productKey, validation.data]);

  async function refresh() {
    await Promise.all(
      productIds.map((id) =>
        queryClient.invalidateQueries({
          queryKey: menuQueryKeys.publicProduct(id),
          exact: true,
          refetchType: "none",
        }),
      ),
    );
    return validation.refetch();
  }

  return { ...validation, refresh };
}
