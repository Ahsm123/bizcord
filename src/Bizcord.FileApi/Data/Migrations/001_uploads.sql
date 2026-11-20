-- An upload is also the state of its upload saga (docs/upload-saga.md), so its id is the saga id
create table uploads(
    id                uuid primary key,
    uploader_id       uuid        not null,
    workspace_id      uuid        not null,
    channel_id        uuid        not null,
    file_name         text        not null,
    size_bytes        bigint      not null check (size_bytes > 0),
    message           text        not null,
    status            text        not null check (status in ('Pending', 'Confirmed', 'Rejected')),
    -- True once PostMessage has been sent, the pivot of the saga: after it the saga only goes forward
    message_requested boolean     not null default false,
    rejection_reason  text,
    created_at        timestamptz not null,
    updated_at        timestamptz not null
);

-- The timeout job only looks for pending uploads that have not moved for a while
create index uploads_pending_idx on uploads (updated_at) where status = 'Pending';
