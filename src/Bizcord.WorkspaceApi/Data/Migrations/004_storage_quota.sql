-- The quota is only changed through single atomic updates (see docs/decisions.md), never through the Workspace
-- aggregate, so two uploads at the same time can't both see free space and both be accepted.
alter table workspaces
    add column storage_limit_bytes bigint not null default 104857600, -- 100 MB
    add column storage_used_bytes  bigint not null default 0,
    add constraint workspaces_storage_used_valid check (storage_used_bytes between 0 and storage_limit_bytes);

-- One row per upload saga, so a repeated ReserveQuota or ReleaseQuota changes nothing the second time.
-- No foreign key: a rejected reservation can point at a workspace that doesn't exist.
create table quota_reservations(
    saga_id      uuid primary key,
    workspace_id uuid        not null,
    size_bytes   bigint      not null check (size_bytes >= 0),
    status       text        not null check (status in ('pending', 'reserved', 'exceeded', 'not_found', 'released')),
    created_at   timestamptz not null default now()
);
