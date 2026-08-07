import { useQuery } from "@tanstack/react-query";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
} from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import {
  getPublicCategories,
  getPublicProducts,
} from "../api/menu/publicMenu";
import { ErrorState } from "../components/ErrorState";
import { PublicMenuSkeleton } from "../components/PublicMenuSkeleton";
import { PublicProductImage } from "../components/PublicProductImage";
import { menuQueryKeys } from "../features/menu/queryKeys";
import type {
  MenuIssue,
  PublicCategory,
  PublicProductListItem,
} from "../types/menu";
import { formatMoney } from "../utils/format";

const searchDebounceMilliseconds = 300;

export function HomePage() {
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const search = searchParams.get("search")?.trim() ?? "";
  const categoryId = searchParams.get("category") ?? "";
  const [searchInput, setSearchInput] = useState(search);
  const [observedCategoryId, setObservedCategoryId] = useState("");
  const menuRootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setSearchInput(search);
  }, [search]);

  useEffect(() => {
    const nextSearch = searchInput.trim();
    if (nextSearch === search) {
      return;
    }

    const timeout = window.setTimeout(() => {
      setSearchParams(
        (current) => {
          const next = new URLSearchParams(current);
          if (nextSearch) {
            next.set("search", nextSearch);
          } else {
            next.delete("search");
          }
          return next;
        },
        { replace: true },
      );
    }, searchDebounceMilliseconds);

    return () => window.clearTimeout(timeout);
  }, [search, searchInput, setSearchParams]);

  const filters = useMemo(
    () => ({
      categoryId: categoryId || undefined,
      search: search || undefined,
    }),
    [categoryId, search],
  );
  const categories = useQuery({
    queryKey: menuQueryKeys.publicCategories,
    queryFn: ({ signal }) => getPublicCategories(signal),
  });
  const products = useQuery({
    queryKey: menuQueryKeys.publicProducts(filters),
    queryFn: ({ signal }) => getPublicProducts(filters, signal),
  });
  const groupedProducts = useMemo(
    () => groupProducts(categories.data ?? [], products.data ?? []),
    [categories.data, products.data],
  );

  useEffect(() => {
    if (categoryId || !menuRootRef.current || !("IntersectionObserver" in window)) {
      setObservedCategoryId(categoryId);
      return;
    }

    const sections = menuRootRef.current.querySelectorAll<HTMLElement>(
      "[data-menu-category]",
    );
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((left, right) => right.intersectionRatio - left.intersectionRatio)[0];
        if (visible) {
          setObservedCategoryId(
            (visible.target as HTMLElement).dataset.menuCategory ?? "",
          );
        }
      },
      { rootMargin: "-28% 0px -62% 0px", threshold: [0, 0.1, 0.5] },
    );
    sections.forEach((section) => observer.observe(section));
    return () => observer.disconnect();
  }, [categoryId, groupedProducts]);

  useEffect(() => {
    if (products.isLoading || !window.location.hash) {
      return;
    }

    const target = document.getElementById(window.location.hash.slice(1));
    target?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [products.isLoading, categoryId]);

  const firstError = categories.error ?? products.error;
  const activeCategoryId = categoryId || observedCategoryId;
  const resultCount = products.data?.length ?? 0;

  function clearSearch() {
    setSearchInput("");
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.delete("search");
        return next;
      },
      { replace: true },
    );
  }

  return (
    <>
      <section className="menu-hero" aria-labelledby="menu-title">
        <div className="menu-hero__inner">
          <div className="menu-hero__copy">
            <p className="eyebrow">Made for your mood</p>
            <h1 id="menu-title">A little pause, poured beautifully.</h1>
            <p>
              Explore coffee, breakfast, and something sweet from the current
              Mood Pickup menu.
            </p>
          </div>
          <form
            className="menu-search"
            onSubmit={(event) => event.preventDefault()}
            role="search"
          >
            <label htmlFor="menu-search">Search the menu</label>
            <div className="menu-search__control">
              <span aria-hidden="true" className="menu-search__icon">
                ⌕
              </span>
              <input
                autoComplete="off"
                id="menu-search"
                onChange={(event: ChangeEvent<HTMLInputElement>) =>
                  setSearchInput(event.target.value)
                }
                placeholder="Try cappuccino or cheesecake"
                type="search"
                value={searchInput}
              />
              {searchInput ? (
                <button
                  aria-label="Clear menu search"
                  className="menu-search__clear"
                  onClick={clearSearch}
                  type="button"
                >
                  Clear
                </button>
              ) : null}
            </div>
          </form>
        </div>
      </section>

      <div className="customer-menu">
        <nav aria-label="Menu categories" className="category-nav">
          <div className="category-nav__scroller">
            <Link
              aria-current={!categoryId ? "page" : undefined}
              className="category-chip"
              to={menuHref(search)}
            >
              All menu
            </Link>
            {categories.data?.map((category) => (
              <Link
                aria-current={
                  activeCategoryId === category.id ? "location" : undefined
                }
                className="category-chip"
                key={category.id}
                to={menuHref(search, category.id)}
              >
                {category.name}
              </Link>
            ))}
          </div>
        </nav>

        <div className="menu-content" ref={menuRootRef}>
          <div className="menu-results-heading">
            <div>
              <p className="eyebrow">
                {categoryId
                  ? categories.data?.find((category) => category.id === categoryId)
                      ?.name ?? "Selected category"
                  : "Our menu"}
              </p>
              <h2>{search ? `Results for “${search}”` : "Choose your moment"}</h2>
            </div>
            {!categories.isLoading && !products.isLoading && !firstError ? (
              <p aria-live="polite" className="menu-result-count">
                {resultCount} {resultCount === 1 ? "item" : "items"}
                {products.isFetching ? " · Updating…" : ""}
              </p>
            ) : null}
          </div>

          {categories.isLoading || products.isLoading ? (
            <PublicMenuSkeleton />
          ) : firstError ? (
            <div className="menu-feedback" role="region" aria-label="Menu error">
              <p className="eyebrow">Something went wrong</p>
              <h2>We couldn’t bring up the menu.</h2>
              <ErrorState error={firstError} />
              <button
                className="button"
                onClick={() => {
                  void categories.refetch();
                  void products.refetch();
                }}
                type="button"
              >
                Retry menu
              </button>
            </div>
          ) : groupedProducts.length > 0 ? (
            groupedProducts.map(({ category, products: categoryProducts }) => (
              <section
                aria-labelledby={`category-title-${category.id}`}
                className="menu-category-section"
                data-menu-category={category.id}
                id={`category-${category.id}`}
                key={category.id}
              >
                <div className="menu-category-heading">
                  <div>
                    <h2 id={`category-title-${category.id}`}>{category.name}</h2>
                    {category.description ? <p>{category.description}</p> : null}
                  </div>
                  <span>{categoryProducts.length} items</span>
                </div>
                <div className="menu-product-grid">
                  {categoryProducts.map((product) => (
                    <ProductCard
                      from={`${location.pathname}${location.search}${location.hash}`}
                      key={product.id}
                      product={product}
                    />
                  ))}
                </div>
              </section>
            ))
          ) : (
            <div className="menu-feedback menu-feedback--empty">
              <span aria-hidden="true" className="menu-feedback__mark">
                MP
              </span>
              <p className="eyebrow">Nothing matched</p>
              <h2>No menu items found.</h2>
              <p>
                {search
                  ? "Try a different product name or description."
                  : "There are no customer-visible products in this category yet."}
              </p>
              {search ? (
                <button
                  className="button button-secondary"
                  onClick={clearSearch}
                  type="button"
                >
                  Clear search
                </button>
              ) : categoryId ? (
                <Link className="button button-link button-secondary" to={menuHref(search)}>
                  View all menu
                </Link>
              ) : null}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

function ProductCard({
  from,
  product,
}: {
  from: string;
  product: PublicProductListItem;
}) {
  return (
    <article
      aria-labelledby={`product-title-${product.id}`}
      className={`menu-product-card ${
        product.isAvailable ? "" : "menu-product-card--unavailable"
      }`}
    >
      <Link
        aria-label={`View details for ${product.name}`}
        className="menu-product-card__image-link"
        state={{ from }}
        to={`/product/${product.id}`}
      >
        <PublicProductImage
          alt={product.name}
          imageUrl={product.imageUrl}
          variant="card"
        />
      </Link>
      <div className="menu-product-card__body">
        <div className="menu-product-card__title-row">
          <div>
            <Link
              id={`product-title-${product.id}`}
              state={{ from }}
              to={`/product/${product.id}`}
            >
              {product.name}
            </Link>
            <p>{product.shortDescription || "A Mood Pickup menu favourite."}</p>
          </div>
          <strong>{formatMoney(product.priceFrom)}</strong>
        </div>
        <ProductMetrics product={product} />
        <div className="menu-product-card__status">
          <span
            className={`menu-status ${
              product.isAvailable ? "menu-status--available" : "menu-status--unavailable"
            }`}
          >
            {product.isAvailable ? "Available" : "Unavailable"}
          </span>
          <span
            className={`menu-status ${
              product.isOrderable ? "menu-status--ready" : "menu-status--warning"
            }`}
          >
            {product.isOrderable ? "Ready to order" : "Not orderable"}
          </span>
        </div>
        {!product.isOrderable ? (
          <AvailabilityIssues issues={product.availabilityIssues} />
        ) : null}
        <Link
          className="menu-product-card__details"
          state={{ from }}
          to={`/product/${product.id}`}
        >
          View details <span aria-hidden="true">→</span>
        </Link>
      </div>
    </article>
  );
}

function ProductMetrics({
  product,
}: {
  product: Pick<
    PublicProductListItem,
    "calories" | "volumeMilliliters" | "weightGrams"
  >;
}) {
  return (
    <dl className="menu-metrics">
      <div>
        <dt>Calories</dt>
        <dd>{metric(product.calories, "kcal")}</dd>
      </div>
      <div>
        <dt>Weight</dt>
        <dd>{metric(product.weightGrams, "g")}</dd>
      </div>
      <div>
        <dt>Volume</dt>
        <dd>{metric(product.volumeMilliliters, "ml")}</dd>
      </div>
    </dl>
  );
}

function AvailabilityIssues({ issues }: { issues: MenuIssue[] }) {
  return (
    <ul aria-label="Availability notes" className="menu-availability-issues">
      {issues.map((issue, index) => (
        <li key={`${issue.code}-${index}`}>{issue.message}</li>
      ))}
    </ul>
  );
}

function groupProducts(
  categories: PublicCategory[],
  products: PublicProductListItem[],
) {
  return categories
    .map((category) => ({
      category,
      products: products.filter((product) => product.categoryId === category.id),
    }))
    .filter((group) => group.products.length > 0);
}

function menuHref(search: string, categoryId?: string) {
  const parameters = new URLSearchParams();
  if (search) {
    parameters.set("search", search);
  }
  if (categoryId) {
    parameters.set("category", categoryId);
  }
  const query = parameters.toString();
  return `/${query ? `?${query}` : ""}${
    categoryId ? `#category-${categoryId}` : ""
  }`;
}

function metric(value: number | undefined, unit: string) {
  return value == null ? "—" : `${value} ${unit}`;
}
