import { createBrowserRouter } from "react-router-dom";
import { ProtectedRoute } from "../components/ProtectedRoute";
import { StaffRoute } from "../components/StaffRoute";
import { ApplicationLayout } from "../layouts/ApplicationLayout";
import { StaffLayout } from "../layouts/StaffLayout";
import { CustomerLoginPage } from "../pages/CustomerLoginPage";
import { CustomerRegistrationPage } from "../pages/CustomerRegistrationPage";
import { CheckoutPage } from "../pages/CheckoutPage";
import { CartPage } from "../pages/CartPage";
import { HealthPage } from "../pages/HealthPage";
import { HomePage } from "../pages/HomePage";
import { NotFoundPage } from "../pages/NotFoundPage";
import { ProductDetailsPage } from "../pages/ProductDetailsPage";
import { OrderSuccessPage } from "../pages/OrderSuccessPage";
import { ProfilePage } from "../pages/ProfilePage";
import { StaffLoginPage } from "../pages/StaffLoginPage";
import { TelegramLinkPage } from "../pages/TelegramLinkPage";
import { AuditLogDetailPage } from "../pages/staff/audit/AuditLogDetailPage";
import { AuditLogPage } from "../pages/staff/audit/AuditLogPage";
import { CategoriesPage } from "../pages/staff/menu/CategoriesPage";
import { CategoryFormPage } from "../pages/staff/menu/CategoryFormPage";
import { MenuOverviewPage } from "../pages/staff/menu/MenuOverviewPage";
import { OptionGroupFormPage } from "../pages/staff/menu/OptionGroupFormPage";
import { OptionGroupsPage } from "../pages/staff/menu/OptionGroupsPage";
import { ProductFormPage } from "../pages/staff/menu/ProductFormPage";
import { ProductsPage } from "../pages/staff/menu/ProductsPage";
import { StaffDashboardPage } from "../pages/staff/StaffDashboardPage";
import { StaffProfilePage } from "../pages/staff/StaffProfilePage";
import { VerifyCodePage } from "../pages/VerifyCodePage";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <ApplicationLayout />,
    children: [
      {
        index: true,
        element: <HomePage />,
      },
      {
        path: "product/:id",
        element: <ProductDetailsPage />,
      },
      {
        path: "cart",
        element: <CartPage />,
      },
      {
        path: "checkout",
        element: (
          <ProtectedRoute accountType="customer">
            <CheckoutPage />
          </ProtectedRoute>
        ),
      },
      {
        path: "order-success/:id",
        element: (
          <ProtectedRoute accountType="customer">
            <OrderSuccessPage />
          </ProtectedRoute>
        ),
      },
      {
        path: "login",
        element: <CustomerLoginPage />,
      },
      {
        path: "login/telegram",
        element: <TelegramLinkPage />,
      },
      {
        path: "verify",
        element: <VerifyCodePage />,
      },
      {
        path: "register",
        element: <CustomerRegistrationPage />,
      },
      {
        path: "profile",
        element: (
          <ProtectedRoute accountType="customer">
            <ProfilePage />
          </ProtectedRoute>
        ),
      },
      {
        path: "staff/login",
        element: <StaffLoginPage />,
      },
      {
        path: "health",
        element: <HealthPage />,
      },
      {
        path: "*",
        element: <NotFoundPage />,
      },
    ],
  },
  {
    path: "/staff",
    element: (
      <StaffRoute>
        <StaffLayout />
      </StaffRoute>
    ),
    children: [
      {
        index: true,
        element: <StaffDashboardPage />,
      },
      {
        path: "profile",
        element: <StaffProfilePage />,
      },
      {
        path: "menu",
        element: (
          <StaffRoute capability="manageMenu">
            <MenuOverviewPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/categories",
        element: (
          <StaffRoute capability="manageMenu">
            <CategoriesPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/categories/new",
        element: (
          <StaffRoute capability="manageMenu">
            <CategoryFormPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/categories/:id",
        element: (
          <StaffRoute capability="manageMenu">
            <CategoryFormPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/products",
        element: (
          <StaffRoute capability="manageMenu">
            <ProductsPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/products/new",
        element: (
          <StaffRoute capability="manageMenu">
            <ProductFormPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/products/:id",
        element: (
          <StaffRoute capability="manageMenu">
            <ProductFormPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/option-groups",
        element: (
          <StaffRoute capability="manageMenu">
            <OptionGroupsPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/option-groups/new",
        element: (
          <StaffRoute capability="manageMenu">
            <OptionGroupFormPage />
          </StaffRoute>
        ),
      },
      {
        path: "menu/option-groups/:id",
        element: (
          <StaffRoute capability="manageMenu">
            <OptionGroupFormPage />
          </StaffRoute>
        ),
      },
      {
        path: "audit-log",
        element: (
          <StaffRoute capability="viewAuditLog">
            <AuditLogPage />
          </StaffRoute>
        ),
      },
      {
        path: "audit-log/:id",
        element: (
          <StaffRoute capability="viewAuditLog">
            <AuditLogDetailPage />
          </StaffRoute>
        ),
      },
      {
        path: "*",
        element: <NotFoundPage />,
      },
    ],
  },
]);
