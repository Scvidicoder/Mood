export function PublicMenuSkeleton() {
  return (
    <div
      aria-busy="true"
      aria-label="Loading menu"
      className="menu-skeleton"
      role="status"
    >
      <span className="visually-hidden">Loading menu…</span>
      {Array.from({ length: 6 }, (_, index) => (
        <div className="menu-card-skeleton" key={index}>
          <span className="skeleton-block skeleton-block--image" />
          <span className="skeleton-block skeleton-block--title" />
          <span className="skeleton-block skeleton-block--copy" />
          <span className="skeleton-block skeleton-block--meta" />
        </div>
      ))}
    </div>
  );
}

export function ProductDetailsSkeleton() {
  return (
    <div
      aria-busy="true"
      aria-label="Loading product details"
      className="product-detail-skeleton"
      role="status"
    >
      <span className="visually-hidden">Loading product details…</span>
      <span className="skeleton-block product-detail-skeleton__image" />
      <div>
        <span className="skeleton-block skeleton-block--eyebrow" />
        <span className="skeleton-block product-detail-skeleton__title" />
        <span className="skeleton-block skeleton-block--copy" />
        <span className="skeleton-block skeleton-block--copy" />
      </div>
    </div>
  );
}
