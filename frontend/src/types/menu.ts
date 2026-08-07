export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface MenuIssue {
  code: string;
  message: string;
  productOptionGroupId?: string;
}

export interface Orderability {
  isOrderable: boolean;
  issues: MenuIssue[];
}

export interface MenuMutationResponse<T> {
  resource: T;
  orderability: Orderability;
}

export interface AdminCategory {
  id: string;
  name: string;
  description?: string;
  displayOrder: number;
  isVisible: boolean;
  isDeleted: boolean;
  productCount: number;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface CategoryInput {
  name: string;
  description: string | null;
  displayOrder: number;
  isVisible: boolean;
}

export interface AdminMediaFile {
  id: string;
  storageProvider: string;
  storageKey: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  width?: number;
  height?: number;
  url?: string;
  isDeleted: boolean;
}

export interface AdminProductListItem {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  imageUrl?: string;
  basePrice: number;
  isAvailable: boolean;
  isVisible: boolean;
  isDeleted: boolean;
  isOrderable: boolean;
  availabilityIssues: MenuIssue[];
  displayOrder: number;
  updatedAt: string;
  rowVersion: string;
}

export interface AdminProduct {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  shortDescription?: string;
  description?: string;
  ingredients?: string;
  basePrice: number;
  defaultWeightGrams?: number;
  defaultVolumeMilliliters?: number;
  defaultCalories?: number;
  imageId?: string;
  image?: AdminMediaFile;
  isAvailable: boolean;
  isVisible: boolean;
  isDeleted: boolean;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  orderability: Orderability;
  optionGroups: AdminProductOptionGroup[];
}

export interface ProductInput {
  categoryId: string;
  name: string;
  shortDescription: string | null;
  description: string | null;
  ingredients: string | null;
  basePrice: number;
  defaultWeightGrams: number | null;
  defaultVolumeMilliliters: number | null;
  defaultCalories: number | null;
  imageId: string | null;
  isAvailable: boolean;
  isVisible: boolean;
  displayOrder: number;
}

export type OptionSelectionType = "Single" | "Multiple";

export interface AdminOptionGroup {
  id: string;
  name: string;
  description?: string;
  selectionType: OptionSelectionType;
  defaultIsRequired: boolean;
  defaultMinimumSelections: number;
  defaultMaximumSelections?: number;
  displayOrder: number;
  isActive: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  values: AdminOptionValue[];
}

export interface OptionGroupInput {
  name: string;
  description: string | null;
  selectionType: OptionSelectionType;
  defaultIsRequired: boolean;
  defaultMinimumSelections: number;
  defaultMaximumSelections: number | null;
  displayOrder: number;
  isActive: boolean;
}

export interface AdminOptionValue {
  id: string;
  optionGroupId: string;
  name: string;
  description?: string;
  displayOrder: number;
  isActive: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface OptionValueInput {
  name: string;
  description: string | null;
  displayOrder: number;
  isActive: boolean;
}

export interface AdminProductOptionGroup {
  id: string;
  optionGroupId: string;
  optionGroupName: string;
  selectionType: OptionSelectionType;
  isRequired: boolean;
  minimumSelections: number;
  maximumSelections: number;
  displayOrder: number;
  isActive: boolean;
  optionGroupIsActive: boolean;
  optionGroupIsDeleted: boolean;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  values: AdminProductOptionValue[];
}

export interface AdminProductOptionValue {
  id: string;
  optionValueId: string;
  optionValueName: string;
  priceModifier: number;
  isDefault: boolean;
  isAvailable: boolean;
  displayOrder: number;
  volumeMilliliters?: number;
  calories?: number;
  optionValueIsActive: boolean;
  optionValueIsDeleted: boolean;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface PublicCategory {
  id: string;
  name: string;
  description?: string;
  displayOrder: number;
}

export interface PublicProductListItem {
  id: string;
  categoryId: string;
  name: string;
  shortDescription?: string;
  imageUrl?: string;
  priceFrom: number;
  currency: string;
  weightGrams?: number;
  volumeMilliliters?: number;
  calories?: number;
  isAvailable: boolean;
  isOrderable: boolean;
  availabilityIssues: MenuIssue[];
}

export interface PublicProductDetail {
  id: string;
  categoryId: string;
  name: string;
  description?: string;
  ingredients?: string;
  imageUrl?: string;
  basePrice: number;
  priceFrom: number;
  currency: string;
  weightGrams?: number;
  volumeMilliliters?: number;
  calories?: number;
  isAvailable: boolean;
  isOrderable: boolean;
  availabilityIssues: MenuIssue[];
  optionGroups: PublicProductOptionGroup[];
}

export interface PublicProductOptionGroup {
  id: string;
  name: string;
  description?: string;
  selectionType: OptionSelectionType;
  isRequired: boolean;
  minimumSelections: number;
  maximumSelections: number;
  displayOrder: number;
  values: PublicProductOptionValue[];
}

export interface PublicProductOptionValue {
  id: string;
  optionValueId: string;
  name: string;
  description?: string;
  priceModifier: number;
  isDefault: boolean;
  isAvailable: boolean;
  displayOrder: number;
  volumeMilliliters?: number;
  calories?: number;
}
