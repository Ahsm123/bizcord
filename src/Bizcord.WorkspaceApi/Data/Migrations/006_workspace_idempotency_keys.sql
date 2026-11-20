-- Idempotent POST /workspaces (#67): a retried request with the same key gets the workspace from the first time.
-- The key is per user, so two users can't hit each other's keys. Deleting the workspace deletes its key too.
create table workspace_idempotency_keys(
    user_id         uuid        not null,
    idempotency_key text        not null,
    workspace_id    uuid        not null references workspaces (id) on delete cascade,
    created_at      timestamptz not null default now(),
    primary key (user_id, idempotency_key)
);
