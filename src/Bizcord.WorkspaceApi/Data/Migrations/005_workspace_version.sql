-- Optimistic concurrency (#68): every save of a workspace must match the version it was loaded with and bumps it,
-- so two requests that change the same workspace at the same time can't overwrite each other
alter table workspaces add column version int not null default 1;
