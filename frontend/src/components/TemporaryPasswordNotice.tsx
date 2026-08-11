import { useState } from "react";

export function TemporaryPasswordNotice({
  password,
  revokedSessionCount,
  onDismiss,
}: {
  password: string;
  revokedSessionCount?: number;
  onDismiss?: () => void;
}) {
  const [copied, setCopied] = useState(false);

  async function copyPassword() {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(password);
    } else {
      const input = document.createElement("textarea");
      input.value = password;
      input.setAttribute("readonly", "");
      input.style.position = "fixed";
      input.style.opacity = "0";
      document.body.appendChild(input);
      input.select();
      document.execCommand("copy");
      input.remove();
    }
    setCopied(true);
  }

  return (
    <section aria-live="polite" className="temporary-password-notice">
      <div>
        <p className="eyebrow">Shown only once</p>
        <h2>Temporary password</h2>
        <p>
          Copy this password and send it securely to the employee. It cannot be
          retrieved after you leave this page.
        </p>
        {revokedSessionCount !== undefined ? (
          <p>
            Existing sessions were revoked ({revokedSessionCount} active
            session{revokedSessionCount === 1 ? "" : "s"}).
          </p>
        ) : null}
      </div>
      <code className="temporary-password-value">{password}</code>
      <div className="inline-actions">
        <button className="button" onClick={() => void copyPassword()} type="button">
          {copied ? "Copied" : "Copy password"}
        </button>
        {onDismiss ? (
          <button className="button button-secondary" onClick={onDismiss} type="button">
            Dismiss
          </button>
        ) : null}
      </div>
    </section>
  );
}
