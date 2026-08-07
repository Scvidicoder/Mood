import { useEffect, useState } from "react";
import { resolveMediaUrl } from "../utils/mediaUrl";

interface PublicProductImageProps {
  alt: string;
  imageUrl?: string | null;
  priority?: boolean;
  variant: "card" | "detail";
}

export function PublicProductImage({
  alt,
  imageUrl,
  priority = false,
  variant,
}: PublicProductImageProps) {
  const [failed, setFailed] = useState(false);
  const resolvedUrl = resolveMediaUrl(imageUrl);

  useEffect(() => {
    setFailed(false);
  }, [resolvedUrl]);

  if (!resolvedUrl || failed) {
    return (
      <div
        aria-label={`No image available for ${alt}`}
        className={`menu-image-placeholder menu-image-placeholder--${variant}`}
        role="img"
      >
        <span aria-hidden="true">MP</span>
        <small>Image unavailable</small>
      </div>
    );
  }

  return (
    <img
      alt={alt}
      className={`menu-product-image menu-product-image--${variant}`}
      decoding="async"
      loading={priority ? "eager" : "lazy"}
      onError={() => setFailed(true)}
      src={resolvedUrl}
    />
  );
}
