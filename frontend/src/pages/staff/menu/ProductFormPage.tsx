import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError } from "../../../api/client";
import { uploadImage } from "../../../api/media";
import { getAdminCategories } from "../../../api/menu/adminCategories";
import {
  assignProductImage,
  createProduct,
  getAdminProduct,
  updateProduct,
} from "../../../api/menu/adminProducts";
import { ConflictNotice } from "../../../components/ConflictNotice";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { OrderabilityPanel } from "../../../components/OrderabilityPanel";
import { useToast } from "../../../components/ToastProvider";
import { menuQueryKeys } from "../../../features/menu/queryKeys";
import { useUnsavedChanges } from "../../../hooks/useUnsavedChanges";
import type { MediaImage } from "../../../types/media";
import type {
  AdminProduct,
  Orderability,
  ProductInput,
} from "../../../types/menu";
import { fieldError, isConcurrencyConflict } from "../../../utils/apiErrors";
import { formatDate } from "../../../utils/format";
import { resolveMediaUrl } from "../../../utils/mediaUrl";
import { ProductOptionsEditor } from "./ProductOptionsEditor";

const allowedImageTypes = ["image/jpeg", "image/png", "image/webp"];
const maximumImageBytes = 5_242_880;

interface ProductForm {
  categoryId: string;
  name: string;
  shortDescription: string;
  description: string;
  ingredients: string;
  basePrice: string;
  defaultWeightGrams: string;
  defaultVolumeMilliliters: string;
  defaultCalories: string;
  imageId: string | null;
  isAvailable: boolean;
  isVisible: boolean;
  displayOrder: number;
}

const emptyForm: ProductForm = {
  categoryId: "",
  name: "",
  shortDescription: "",
  description: "",
  ingredients: "",
  basePrice: "0.00",
  defaultWeightGrams: "",
  defaultVolumeMilliliters: "",
  defaultCalories: "",
  imageId: null,
  isAvailable: true,
  isVisible: true,
  displayOrder: 0,
};

export function ProductFormPage() {
  const { id } = useParams();
  const editing = Boolean(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { notify } = useToast();
  const [form, setForm] = useState<ProductForm>(emptyForm);
  const [rowVersion, setRowVersion] = useState("");
  const [isDirty, setIsDirty] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [uploadedMedia, setUploadedMedia] = useState<MediaImage | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [fileError, setFileError] = useState("");
  const [currentOrderability, setCurrentOrderability] =
    useState<Orderability | null>(null);
  useUnsavedChanges(isDirty);

  const categories = useQuery({
    queryKey: ["admin", "product-form-categories"],
    queryFn: ({ signal }) =>
      getAdminCategories({ includeDeleted: true, pageSize: 100 }, signal),
  });
  const product = useQuery({
    queryKey: menuQueryKeys.product(id ?? "new"),
    queryFn: ({ signal }) => getAdminProduct(id!, signal),
    enabled: editing,
  });

  function resetFromProduct(value: AdminProduct) {
    setForm({
      categoryId: value.categoryId,
      name: value.name,
      shortDescription: value.shortDescription ?? "",
      description: value.description ?? "",
      ingredients: value.ingredients ?? "",
      basePrice: value.basePrice.toFixed(2),
      defaultWeightGrams: value.defaultWeightGrams?.toString() ?? "",
      defaultVolumeMilliliters:
        value.defaultVolumeMilliliters?.toString() ?? "",
      defaultCalories: value.defaultCalories?.toString() ?? "",
      imageId: value.imageId ?? null,
      isAvailable: value.isAvailable,
      isVisible: value.isVisible,
      displayOrder: value.displayOrder,
    });
    setRowVersion(value.rowVersion);
    setCurrentOrderability(value.orderability);
    setUploadedMedia(null);
    setSelectedFile(null);
    setIsDirty(false);
    setConflict(false);
  }

  useEffect(() => {
    if (product.data && !isDirty) resetFromProduct(product.data);
  }, [isDirty, product.data]);

  useEffect(() => {
    if (!selectedFile) {
      setPreviewUrl(null);
      return;
    }
    const nextUrl = URL.createObjectURL(selectedFile);
    setPreviewUrl(nextUrl);
    return () => URL.revokeObjectURL(nextUrl);
  }, [selectedFile]);

  const validation = useMemo(() => {
    const errors: Record<string, string> = {};
    if (!form.categoryId) errors.categoryId = "Choose a category.";
    if (!form.name.trim()) errors.name = "Name is required.";
    if (form.name.trim().length > 160) errors.name = "Use 160 characters or fewer.";
    if (form.shortDescription.length > 300) {
      errors.shortDescription = "Use 300 characters or fewer.";
    }
    if (form.description.length > 2000) {
      errors.description = "Use 2,000 characters or fewer.";
    }
    if (form.ingredients.length > 1000) {
      errors.ingredients = "Use 1,000 characters or fewer.";
    }
    if (!/^\d+(\.\d{1,2})?$/.test(form.basePrice)) {
      errors.basePrice = "Enter a non-negative amount with at most two decimals.";
    }
    for (const field of [
      "defaultWeightGrams",
      "defaultVolumeMilliliters",
      "defaultCalories",
    ] as const) {
      if (form[field] && (!/^\d+$/.test(form[field]) || Number(form[field]) < 0)) {
        errors[field] = "Enter a whole number of zero or more.";
      }
    }
    if (form.displayOrder < 0) errors.displayOrder = "Order cannot be negative.";
    return errors;
  }, [form]);

  function toInput(): ProductInput {
    const nullableText = (value: string) => value.trim() || null;
    const nullableNumber = (value: string) =>
      value === "" ? null : Number(value);
    return {
      categoryId: form.categoryId,
      name: form.name.trim(),
      shortDescription: nullableText(form.shortDescription),
      description: nullableText(form.description),
      ingredients: nullableText(form.ingredients),
      basePrice: Number(form.basePrice),
      defaultWeightGrams: nullableNumber(form.defaultWeightGrams),
      defaultVolumeMilliliters: nullableNumber(form.defaultVolumeMilliliters),
      defaultCalories: nullableNumber(form.defaultCalories),
      imageId: form.imageId,
      isAvailable: form.isAvailable,
      isVisible: form.isVisible,
      displayOrder: form.displayOrder,
    };
  }

  const save = useMutation({
    mutationFn: () =>
      editing
        ? updateProduct(id!, { ...toInput(), rowVersion })
        : createProduct(toInput()),
    onSuccess: (result) => {
      queryClient.setQueryData(menuQueryKeys.product(result.resource.id), result.resource);
      void queryClient.invalidateQueries({ queryKey: ["admin", "products"] });
      resetFromProduct(result.resource);
      notify(editing ? "Product saved." : "Product created.");
      if (!editing) {
        navigate(`/staff/menu/products/${result.resource.id}`, { replace: true });
      }
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) setConflict(true);
    },
  });

  const mediaUpload = useMutation({
    mutationFn: (file: File) => uploadImage(file),
    onSuccess: async (media) => {
      setUploadedMedia(media);
      setForm((current) => ({ ...current, imageId: media.id }));
      setFileError("");
      if (editing && id) {
        try {
          const result = await assignProductImage(id, media.id, rowVersion);
          queryClient.setQueryData(menuQueryKeys.product(id), result.resource);
          setRowVersion(result.resource.rowVersion);
          setCurrentOrderability(result.orderability);
          notify("Image uploaded and assigned.");
          void queryClient.invalidateQueries({ queryKey: ["admin", "products"] });
        } catch (error) {
          if (isConcurrencyConflict(error)) setConflict(true);
          throw error;
        }
      } else {
        setIsDirty(true);
        notify("Image uploaded. Save the new product to assign it.");
      }
    },
  });
  const removeImage = useMutation({
    mutationFn: () => assignProductImage(id!, null, rowVersion),
    onSuccess: (result) => {
      queryClient.setQueryData(menuQueryKeys.product(id!), result.resource);
      setForm((current) => ({ ...current, imageId: null }));
      setRowVersion(result.resource.rowVersion);
      setCurrentOrderability(result.orderability);
      setSelectedFile(null);
      setUploadedMedia(null);
      notify("Product image removed. The media record was preserved.");
      void queryClient.invalidateQueries({ queryKey: ["admin", "products"] });
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) setConflict(true);
    },
  });

  async function refreshProduct(orderability?: Orderability) {
    if (orderability) setCurrentOrderability(orderability);
    const result = await product.refetch();
    if (result.data) {
      queryClient.setQueryData(menuQueryKeys.product(result.data.id), result.data);
      setRowVersion(result.data.rowVersion);
      setCurrentOrderability(result.data.orderability);
    }
  }

  function selectImage(file?: File) {
    setFileError("");
    if (!file) {
      setSelectedFile(null);
      return;
    }
    if (!allowedImageTypes.includes(file.type)) {
      setFileError("Choose a JPEG, PNG, or WebP image.");
      return;
    }
    if (file.size === 0) {
      setFileError("The selected file is empty.");
      return;
    }
    if (file.size > maximumImageBytes) {
      setFileError("The selected image exceeds the 5 MB upload limit.");
      return;
    }
    setSelectedFile(file);
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    if (Object.keys(validation).length === 0) save.mutate();
  }

  if ((editing && product.isLoading) || categories.isLoading) {
    return <LoadingState message="Loading product editor..." />;
  }
  if ((editing && product.error) || categories.error) {
    return (
      <div>
        <ErrorState error={product.error ?? categories.error} />
        <button
          className="button"
          onClick={() => {
            void product.refetch();
            void categories.refetch();
          }}
          type="button"
        >
          Retry
        </button>
      </div>
    );
  }

  const backendError = save.error instanceof ApiError ? save.error : null;
  const displayedImage =
    previewUrl ??
    resolveMediaUrl(uploadedMedia?.url ?? product.data?.image?.url ?? null);

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Products</p>
          <h1>{editing ? `Edit ${product.data?.name ?? "product"}` : "New product"}</h1>
          <p>Save accepted drafts, then use the orderability panel to finish configuration.</p>
        </div>
        <Link className="button button-secondary button-link" to="/staff/menu/products">
          Back to products
        </Link>
      </div>
      {conflict ? (
        <ConflictNotice
          onDiscard={() => product.data && resetFromProduct(product.data)}
          onReload={() => void refreshProduct()}
        />
      ) : null}
      {product.data?.isDeleted ? (
        <div className="notice notice--warning">
          This product is deleted. Restore it from the product list before editing.
        </div>
      ) : null}
      {currentOrderability ? (
        <OrderabilityPanel orderability={currentOrderability} />
      ) : null}
      <form className="product-editor" onSubmit={submit}>
        <section className="panel editor-section">
          <h2>Basic information</h2>
          <div className="form-grid form-grid--wide">
            <label htmlFor="product-category">Category</label>
            <select
              disabled={product.data?.isDeleted}
              id="product-category"
              onChange={(event) => {
                setForm({ ...form, categoryId: event.target.value });
                setIsDirty(true);
              }}
              value={form.categoryId}
            >
              <option value="">Choose a category</option>
              {categories.data?.items.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}{category.isDeleted ? " (deleted)" : ""}
                </option>
              ))}
            </select>
            <FieldError message={validation.categoryId || fieldError(save.error, "categoryId")} />
            <label htmlFor="product-name">Name</label>
            <input
              disabled={product.data?.isDeleted}
              id="product-name"
              maxLength={160}
              onChange={(event) => {
                setForm({ ...form, name: event.target.value });
                setIsDirty(true);
              }}
              value={form.name}
            />
            <FieldError message={validation.name || fieldError(save.error, "name")} />
            <label htmlFor="product-short-description">Short description</label>
            <textarea
              disabled={product.data?.isDeleted}
              id="product-short-description"
              maxLength={300}
              onChange={(event) => {
                setForm({ ...form, shortDescription: event.target.value });
                setIsDirty(true);
              }}
              rows={2}
              value={form.shortDescription}
            />
            <FieldError
              message={
                validation.shortDescription ||
                fieldError(save.error, "shortDescription")
              }
            />
            <label htmlFor="product-description">Description</label>
            <textarea
              disabled={product.data?.isDeleted}
              id="product-description"
              maxLength={2000}
              onChange={(event) => {
                setForm({ ...form, description: event.target.value });
                setIsDirty(true);
              }}
              rows={5}
              value={form.description}
            />
            <FieldError message={validation.description || fieldError(save.error, "description")} />
            <label htmlFor="product-ingredients">Ingredients</label>
            <textarea
              disabled={product.data?.isDeleted}
              id="product-ingredients"
              maxLength={1000}
              onChange={(event) => {
                setForm({ ...form, ingredients: event.target.value });
                setIsDirty(true);
              }}
              rows={3}
              value={form.ingredients}
            />
            <FieldError message={validation.ingredients || fieldError(save.error, "ingredients")} />
          </div>
        </section>
        <section className="panel editor-section">
          <h2>Price and measurements</h2>
          <p className="hint-copy">
            TJS is entered and displayed to two decimal places; the UI performs no
            floating-point price calculations.
          </p>
          <div className="metric-grid">
            <label>
              Base price (TJS)
              <input
                disabled={product.data?.isDeleted}
                inputMode="decimal"
                min={0}
                onChange={(event) => {
                  setForm({ ...form, basePrice: event.target.value });
                  setIsDirty(true);
                }}
                step="0.01"
                type="number"
                value={form.basePrice}
              />
              <FieldError message={validation.basePrice || fieldError(save.error, "basePrice")} />
            </label>
            <NumberField
              disabled={Boolean(product.data?.isDeleted)}
              error={validation.defaultWeightGrams || fieldError(save.error, "defaultWeightGrams")}
              label="Default weight (g)"
              onChange={(value) => {
                setForm({ ...form, defaultWeightGrams: value });
                setIsDirty(true);
              }}
              value={form.defaultWeightGrams}
            />
            <NumberField
              disabled={Boolean(product.data?.isDeleted)}
              error={
                validation.defaultVolumeMilliliters ||
                fieldError(save.error, "defaultVolumeMilliliters")
              }
              label="Default volume (ml)"
              onChange={(value) => {
                setForm({ ...form, defaultVolumeMilliliters: value });
                setIsDirty(true);
              }}
              value={form.defaultVolumeMilliliters}
            />
            <NumberField
              disabled={Boolean(product.data?.isDeleted)}
              error={validation.defaultCalories || fieldError(save.error, "defaultCalories")}
              label="Default calories"
              onChange={(value) => {
                setForm({ ...form, defaultCalories: value });
                setIsDirty(true);
              }}
              value={form.defaultCalories}
            />
            <label>
              Display order
              <input
                disabled={product.data?.isDeleted}
                min={0}
                onChange={(event) => {
                  setForm({ ...form, displayOrder: Number(event.target.value) });
                  setIsDirty(true);
                }}
                type="number"
                value={form.displayOrder}
              />
              <FieldError message={validation.displayOrder || fieldError(save.error, "displayOrder")} />
            </label>
          </div>
        </section>
        <section className="panel editor-section">
          <h2>Image</h2>
          <div className="image-editor">
            {displayedImage ? (
              <img alt={`Preview for ${form.name || "product"}`} src={displayedImage} />
            ) : (
              <div className="image-placeholder image-placeholder--large">No image assigned</div>
            )}
            <div>
              <label htmlFor="product-image">JPEG, PNG, or WebP, up to 5 MB</label>
              <input
                accept="image/jpeg,image/png,image/webp"
                disabled={mediaUpload.isPending || product.data?.isDeleted}
                id="product-image"
                onChange={(event) => selectImage(event.target.files?.[0])}
                type="file"
              />
              {selectedFile ? <p>{selectedFile.name}</p> : null}
              {uploadedMedia ? (
                <p>
                  {uploadedMedia.originalFileName} - {uploadedMedia.width} x{" "}
                  {uploadedMedia.height}px
                </p>
              ) : product.data?.image ? (
                <p>
                  {product.data.image.originalFileName} -{" "}
                  {product.data.image.width ?? "?"} x{" "}
                  {product.data.image.height ?? "?"}px
                </p>
              ) : null}
              {fileError ? <p className="field-error" role="alert">{fileError}</p> : null}
              {mediaUpload.error ? <ErrorState error={mediaUpload.error} /> : null}
              <div aria-live="polite" className="form-actions">
                <button
                  className="button button-secondary"
                  disabled={!selectedFile || mediaUpload.isPending}
                  onClick={() => selectedFile && mediaUpload.mutate(selectedFile)}
                  type="button"
                >
                  {mediaUpload.isPending ? "Uploading..." : "Upload image"}
                </button>
                {editing && form.imageId ? (
                  <button
                    className="text-button text-button--danger"
                    disabled={removeImage.isPending}
                    onClick={() => removeImage.mutate()}
                    type="button"
                  >
                    {removeImage.isPending ? "Removing..." : "Remove from product"}
                  </button>
                ) : null}
              </div>
              <p className="hint-copy">
                Replacing or removing an image never deletes the old media record.
              </p>
            </div>
          </div>
        </section>
        <section className="panel editor-section">
          <h2>Availability and visibility</h2>
          <div className="toggle-grid">
            <label className="checkbox-field">
              <input
                checked={form.isAvailable}
                disabled={product.data?.isDeleted}
                onChange={(event) => {
                  setForm({ ...form, isAvailable: event.target.checked });
                  setIsDirty(true);
                }}
                type="checkbox"
              />
              Available to order
            </label>
            <label className="checkbox-field">
              <input
                checked={form.isVisible}
                disabled={product.data?.isDeleted}
                onChange={(event) => {
                  setForm({ ...form, isVisible: event.target.checked });
                  setIsDirty(true);
                }}
                type="checkbox"
              />
              Visible on the public menu
            </label>
          </div>
        </section>
        {backendError && !conflict ? <ErrorState error={backendError} /> : null}
        <div className="sticky-form-actions">
          <button
            className="button"
            disabled={
              save.isPending ||
              Boolean(product.data?.isDeleted) ||
              Object.keys(validation).length > 0
            }
            type="submit"
          >
            {save.isPending ? "Saving..." : "Save product"}
          </button>
          <span aria-live="polite">{isDirty ? "Unsaved changes" : "All changes saved"}</span>
        </div>
      </form>
      {editing && product.data && !product.data.isDeleted ? (
        <ProductOptionsEditor product={product.data} onChanged={refreshProduct} />
      ) : editing ? null : (
        <div className="notice">Save the product before configuring option groups.</div>
      )}
      {product.data ? (
        <p className="metadata-copy">
          Created {formatDate(product.data.createdAt)} - Updated{" "}
          {formatDate(product.data.updatedAt)}
        </p>
      ) : null}
    </section>
  );
}

function FieldError({ message }: { message?: string }) {
  return message ? (
    <span className="field-error" role="alert">
      {message}
    </span>
  ) : null;
}

function NumberField({
  label,
  value,
  error,
  disabled,
  onChange,
}: {
  label: string;
  value: string;
  error?: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <label>
      {label}
      <input
        disabled={disabled}
        min={0}
        onChange={(event) => onChange(event.target.value)}
        type="number"
        value={value}
      />
      <FieldError message={error} />
    </label>
  );
}
