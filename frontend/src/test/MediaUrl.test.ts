import { describe, expect, it } from "vitest";
import { resolveMediaUrl } from "../utils/mediaUrl";

describe("media URL resolution", () => {
  it("resolves root-relative media URLs against the backend origin", () => {
    expect(resolveMediaUrl("/media/aa/bb/image.png")).toBe(
      "https://api.test/media/aa/bb/image.png",
    );
  });

  it("preserves absolute media URLs and empty values", () => {
    expect(resolveMediaUrl("https://cdn.test/image.webp")).toBe(
      "https://cdn.test/image.webp",
    );
    expect(resolveMediaUrl(null)).toBeNull();
  });
});
