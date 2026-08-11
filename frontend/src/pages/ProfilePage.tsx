import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { getProfile, updateProfile } from "../api/profile";
import { useAuth } from "../app/AuthProvider";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import { useOrderNotifications } from "../hooks/useOrderNotifications";
import { formatDate } from "../utils/format";

export function ProfilePage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { logout } = useAuth();
  const connectionState = useOrderNotifications();
  const [name, setName] = useState("");
  const profile = useQuery({
    queryKey: ["profile"],
    queryFn: ({ signal }) => getProfile(signal),
    refetchInterval: connectionState === "Connected" ? false : 15_000,
  });
  const updateMutation = useMutation({
    mutationFn: () =>
      updateProfile({
        name,
        rowVersion: profile.data?.rowVersion ?? "",
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["profile"], updated);
      setName(updated.name);
    },
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: ["profile"] });
    },
  });
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => navigate("/", { replace: true }),
  });

  useEffect(() => {
    if (profile.data) {
      setName(profile.data.name);
    }
  }, [profile.data]);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    updateMutation.mutate();
  }

  return (
    <section className="page customer-profile-page">
      <div className="page-heading profile-heading">
        <div>
          <p className="eyebrow">Personal cabinet</p>
          <h1>Your profile</h1>
          <p>Manage your name and keep track of every Mood Pickup order.</p>
        </div>
        <span className={`connection-state connection-state--${connectionState.toLowerCase()}`}>
          Live updates: {connectionState}
        </span>
      </div>

      {profile.isLoading ? <LoadingState message="Loading your profile…" /> : null}
      {profile.error ? <ErrorState error={profile.error} /> : null}

      {profile.data ? (
        <div className="profile-grid">
          <div className="profile-main-card">
            <div className="profile-section-heading">
              <div>
                <p className="eyebrow">Profile information</p>
                <h2>{profile.data.name}</h2>
              </div>
              <span className="profile-telegram-status">
                Telegram: {profile.data.telegramLinked ? "Connected" : "Not connected"}
              </span>
            </div>

            <form className="profile-name-form" onSubmit={submit}>
              <label htmlFor="profile-name">Customer name</label>
              <div className="profile-name-controls">
                <input
                  autoComplete="name"
                  id="profile-name"
                  maxLength={100}
                  minLength={2}
                  onChange={(event) => setName(event.target.value)}
                  required
                  value={name}
                />
                <button
                  className="button"
                  disabled={
                    updateMutation.isPending ||
                    name.trim().length < 2 ||
                    name.trim() === profile.data.name
                  }
                  type="submit"
                >
                  {updateMutation.isPending ? "Saving…" : "Save name"}
                </button>
              </div>
              <p className="field-help">Only your name can be edited in this sprint.</p>
            </form>

            {updateMutation.error ? <ErrorState error={updateMutation.error} /> : null}
            {updateMutation.isSuccess ? (
              <p className="menu-feedback" role="status">Your name was updated.</p>
            ) : null}

            <dl className="profile-account-details">
              <div><dt>Phone number</dt><dd>{profile.data.phoneNumber}</dd></div>
              <div><dt>Phone verified</dt><dd>{profile.data.phoneVerified ? "Verified" : "Not verified"}</dd></div>
              <div><dt>Telegram</dt><dd>{profile.data.telegramLinked ? "Linked" : "Not linked"}</dd></div>
              <div><dt>Registration date</dt><dd>{formatDate(profile.data.registrationDate)}</dd></div>
            </dl>
          </div>

          <aside className="profile-summary-card" aria-label="Order summary">
            <p className="eyebrow">Your orders</p>
            <div className="profile-order-counts">
              <div><strong>{profile.data.activeOrderCount}</strong><span>Active</span></div>
              <div><strong>{profile.data.completedOrderCount}</strong><span>Completed</span></div>
            </div>
            <Link className="button button-link" to="/profile/orders">View order history</Link>
            {logoutMutation.error ? <ErrorState error={logoutMutation.error} /> : null}
            <button
              className="button button-secondary"
              disabled={logoutMutation.isPending}
              onClick={() => logoutMutation.mutate()}
              type="button"
            >
              {logoutMutation.isPending ? "Signing out…" : "Sign out"}
            </button>
          </aside>
        </div>
      ) : null}
    </section>
  );
}
