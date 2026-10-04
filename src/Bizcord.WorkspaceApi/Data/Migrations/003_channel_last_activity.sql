-- Hot write path (every message), kept apart from the rarely-changing channel config
create table channel_activity(
    channel_id       uuid primary key references channels (id) on delete cascade,
    last_activity_at timestamptz not null default now()
);

insert into channel_activity (channel_id) select id from channels;
