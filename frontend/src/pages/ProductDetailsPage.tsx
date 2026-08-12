import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import {
  Link,
  useLocation,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { getPublicProduct } from "../api/menu/publicMenu";
import { ErrorState } from "../components/ErrorState";
import { ProductConfigurator } from "../components/ProductConfigurator";
import { useToast } from "../components/ToastProvider";
import { ProductDetailsSkeleton } from "../components/PublicMenuSkeleton";
import { PublicProductImage } from "../components/PublicProductImage";
import { cartActions } from "../features/cart/cartSlice";
import {
  buildCartLine,
  type ConfigurationResult,
  type ProductSelection,
} from "../features/cart/configuration";
import { menuQueryKeys } from "../features/menu/queryKeys";
import { useAppDispatch, useAppSelector } from "../store";
import type { MenuIssue, PublicProductDetail } from "../types/menu";
import { formatMoney } from "../utils/format";

interface ProductLocationState {
  from?: string;
}

export function ProductDetailsPage() {
  const { id = "" } = useParams();
  const [searchParams] = useSearchParams();
  const editLineId = searchParams.get("editLine") ?? undefined;
  const editLine = useAppSelector((state) =>
    editLineId
      ? state.cart.items.find((line) => line.id === editLineId)
      : undefined,
  );
  const location = useLocation();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const from =
    (location.state as ProductLocationState | null)?.from ??
    (editLineId ? "/cart" : "/");
  const product = useQuery({
    queryKey: menuQueryKeys.publicProduct(id),
    queryFn: ({ signal }) => getPublicProduct(id, signal),
    enabled: Boolean(id),
  });

  useEffect(() => {
    if (!product.data) {
      return;
    }

    const previousTitle = document.title;
    document.title = `${product.data.name} - Mood Pickup`;
    headingRef.current?.focus();
    return () => {
      document.title = previousTitle;
    };
  }, [product.data]);

  return (
    <section className="product-detail-page">
      <div className="product-detail-page__inner">
        <Link className="product-detail-back" to={from}>
          <span aria-hidden="true">&larr;</span>{" "}
          {editLineId ? "Back to cart" : "Back to menu"}
        </Link>

        {editLineId && !editLine ? (
          <div className="cart-context-warning" role="status">
            That cart line is no longer available. You can configure this as a
            new item.
          </div>
        ) : null}

        {product.isLoading ? (
          <ProductDetailsSkeleton />
        ) : product.error ? (
          <div className="menu-feedback product-detail-error">
            <p className="eyebrow">Product unavailable</p>
            <h1>We could not load these details.</h1>
            <ErrorState error={product.error} />
            <div className="product-detail-error__actions">
              <button
                className="button"
                onClick={() => void product.refetch()}
                type="button"
              >
                Retry details
              </button>
              <Link className="button button-link button-secondary" to="/">
                Browse the menu
              </Link>
            </div>
          </div>
        ) : product.data ? (
          <ProductDetail
            editLineId={editLine?.id}
            initialOptionValueIds={editLine?.selectedOptions.map(
              (option) => option.optionValueId,
            )}
            product={product.data}
            titleRef={headingRef}
          />
        ) : null}
      </div>
    </section>
  );
}

function ProductDetail({
  editLineId,
  initialOptionValueIds,
  product,
  titleRef,
}: {
  editLineId?: string;
  initialOptionValueIds?: readonly string[];
  product: PublicProductDetail;
  titleRef: React.RefObject<HTMLHeadingElement | null>;
}) {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { notify } = useToast();
  const [successMessage, setSuccessMessage] = useState("");

  function submit(
    selection: ProductSelection,
    result: ConfigurationResult,
  ) {
    const line = buildCartLine(product, selection, result);
    if (editLineId) {
      dispatch(cartActions.replaceConfiguredLine({ lineId: editLineId, line }));
      navigate("/cart", { replace: true });
      return;
    }

    dispatch(cartActions.addConfiguredLine(line));
    notify(`${product.name} added to your cart.`);
    setSuccessMessage(
      `${product.name} was added. You can keep browsing or review your cart.`,
    );
  }

  return (
    <>
      <article className="product-detail-hero">
        <div className="product-detail-hero__media">
          <PublicProductImage
            alt={product.name}
            imageUrl={product.imageUrl}
            priority
            variant="detail"
          />
        </div>
        <div className="product-detail-hero__copy">
          <p className="eyebrow">Mood Pickup menu</p>
          <h1 ref={titleRef} tabIndex={-1}>
            {product.name}
          </h1>
          <p className="product-detail-price">
            {product.priceFrom === product.basePrice ? null : (
              <span>From </span>
            )}
            {formatMoney(product.priceFrom)}
          </p>
          <div className="product-detail-status">
            <span
              className={`menu-status ${
                product.isAvailable
                  ? "menu-status--available"
                  : "menu-status--unavailable"
              }`}
            >
              {product.isAvailable
                ? "Available today"
                : "Currently unavailable"}
            </span>
            <span
              className={`menu-status ${
                product.isOrderable
                  ? "menu-status--ready"
                  : "menu-status--warning"
              }`}
            >
              {product.isOrderable ? "Ready to order" : "Not orderable"}
            </span>
          </div>
          <p className="product-detail-description">
            {product.description || "More details will be added soon."}
          </p>
          <dl className="product-detail-metrics">
            <DetailMetric label="Calories" unit="kcal" value={product.calories} />
            <DetailMetric label="Weight" unit="g" value={product.weightGrams} />
            <DetailMetric
              label="Volume"
              unit="ml"
              value={product.volumeMilliliters}
            />
          </dl>
          {product.ingredients ? (
            <section
              className="product-ingredients"
              aria-labelledby="ingredients-title"
            >
              <h2 id="ingredients-title">Ingredients</h2>
              <p>{product.ingredients}</p>
            </section>
          ) : null}
        </div>
      </article>

      {!product.isOrderable ? (
        <section
          aria-labelledby="availability-title"
          className="product-availability-panel"
        >
          <div>
            <p className="eyebrow">Availability note</p>
            <h2 id="availability-title">
              This item cannot be ordered right now.
            </h2>
          </div>
          <IssueList issues={product.availabilityIssues} />
        </section>
      ) : null}

      {successMessage ? (
        <div
          aria-live="polite"
          className="configuration-success"
          role="status"
        >
          <span>{successMessage}</span>
          <Link to="/cart">View cart</Link>
        </div>
      ) : null}

      <ProductConfigurator
        initialOptionValueIds={initialOptionValueIds}
        key={`${product.id}:${editLineId ?? "new"}`}
        onSubmit={submit}
        product={product}
        submitLabel={editLineId ? "Save Changes" : "Add to Cart"}
      />
    </>
  );
}

function DetailMetric({
  label,
  unit,
  value,
}: {
  label: string;
  unit: string;
  value?: number;
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value == null ? "-" : `${value} ${unit}`}</dd>
    </div>
  );
}

function IssueList({ issues }: { issues: MenuIssue[] }) {
  return (
    <ul className="product-availability-panel__issues">
      {issues.map((issue, index) => (
        <li key={`${issue.code}-${index}`}>{issue.message}</li>
      ))}
    </ul>
  );
}
