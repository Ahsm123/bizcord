-- Transactional outbox (#66): events are written here in the same transaction as the change they describe,
-- and OutboxPublisher sends them to RabbitMQ afterwards. id is the event id that consumers use for deduplication.
create table outbox(
    id         uuid primary key,
    type       text        not null,
    payload    jsonb       not null,
    created_at timestamptz not null default now(),
    sent_at    timestamptz
);

create index outbox_unsent_idx on outbox (created_at) where sent_at is null;
