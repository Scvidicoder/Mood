import type { MediaImage } from "../types/media";
import { apiClient } from "./client";

export function uploadImage(
  file: File,
  signal?: AbortSignal,
): Promise<MediaImage> {
  const formData = new FormData();
  formData.append("file", file);
  return apiClient.upload("admin/media/images", formData, { signal });
}
