create table workspaces(
    id   uuid primary key,
    name text not null
);

create table members(
    workspace_id uuid not null,
    user_id uuid not null,
    role text not null,
    primary key (workspace_id, user_id),
);

create table channels(
    id uuid primary key,
    workspace_id uuid not null,
    name text not null
);






