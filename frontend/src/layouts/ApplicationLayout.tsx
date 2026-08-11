import { Link, NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../app/AuthProvider";
import { CartGlobalStatus } from "../components/CartGlobalStatus";
import { selectCartTotalQuantity } from "../features/cart/cartSlice";
import { useAppSelector } from "../store";

export function ApplicationLayout() {
  const { session } = useAuth();
  const cartQuantity = useAppSelector(selectCartTotalQuantity);

  return (
    <div className="app-shell customer-shell">
      <a className="skip-link" href="#main-content">
        Skip to menu
      </a>
      <header className="app-header customer-header">
        <div className="app-header__inner customer-header__inner">
          <NavLink aria-label="Mood Pickup home" className="app-title" to="/">
            <span aria-hidden="true" className="app-title__mark">
              MP
            </span>
            <span>
              Mood Pickup
              <small>coffee · food · pause</small>
            </span>
          </NavLink>
          <nav className="app-nav customer-nav" aria-label="Primary navigation">
            <NavLink to="/" end>
              Menu
            </NavLink>
            <NavLink
              aria-label={`Cart with ${cartQuantity} ${
                cartQuantity === 1 ? "item" : "items"
              }`}
              className="customer-cart-link"
              to="/cart"
            >
              <span>Cart</span>
              <strong aria-hidden="true">{cartQuantity}</strong>
            </NavLink>
            {session?.accountType === "customer" ? (
              <>
                <NavLink to="/orders">My orders</NavLink>
                <NavLink to="/profile">Profile</NavLink>
              </>
            ) : (
              <NavLink to="/login">Sign in</NavLink>
            )}
            {session?.accountType === "employee" ? (
              <NavLink to="/staff">Staff</NavLink>
            ) : (
              <NavLink className="customer-nav__quiet" to="/staff/login">
                Staff
              </NavLink>
            )}
          </nav>
        </div>
      </header>
      <CartGlobalStatus />
      <main id="main-content">
        <Outlet />
      </main>
      <footer className="customer-footer">
        <div className="customer-footer__inner">
          <div>
            <strong>Mood Pickup</strong>
            <p>A calmer way to choose your next coffee break.</p>
          </div>
          <nav aria-label="Footer navigation">
            <Link to="/">Menu</Link>
            <Link to="/cart">Cart</Link>
            {session?.accountType === "customer" ? (
              <Link to="/orders">My orders</Link>
            ) : null}
            <Link to="/login">Customer sign in</Link>
            <Link to="/health">System health</Link>
          </nav>
        </div>
      </footer>
    </div>
  );
}
