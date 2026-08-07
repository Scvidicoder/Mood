export function ConflictNotice({
  onReload,
  onDiscard,
}: {
  onReload: () => void;
  onDiscard: () => void;
}) {
  return (
    <div className="conflict-notice" role="alert">
      <strong>Another employee changed this resource.</strong>
      <p>
        Your draft has not been overwritten. Reload the newest server version,
        or discard your local edits.
      </p>
      <div className="inline-actions">
        <button className="button" onClick={onReload} type="button">
          Reload latest
        </button>
        <button className="button button-secondary" onClick={onDiscard} type="button">
          Discard local changes
        </button>
      </div>
    </div>
  );
}
