# Failure points

Week 44, Task 01 for FileApi: where it can fail, what happens then, and what we do about it. FileApi orchestrates the
[upload saga](../../docs/upload-saga.md), so most of its failure points are about the other services in the saga.
**Handled** means there is a measure and a test, **Open** means we know about it and haven't fixed it.

| Dependency                                  | Direction | Type  |
|---------------------------------------------|-----------|-------|
| Postgres (file-db)                          | outgoing  | sync  |
| RabbitMQ                                    | outgoing  | async |
| Vault                                       | outgoing  | sync, at startup |
| WorkspaceApi, via ReserveQuota and ReleaseQuota | outgoing | async |
| Message, via PostMessage                    | outgoing  | async |
| Gateway (Ocelot)                            | incoming  | sync  |

## Postgres

- **Handled:** A slow database can't hang a request: every HTTP request fails after 2 seconds with 504, like in
  WorkspaceApi (#65).
- **Handled:** If Postgres is down for a short while, a reply from Workspace or Message is retried after 1, 2 and 4
  seconds.
- **Handled (#82):** If Postgres is down for longer, the reply ends in EasyNetQ's error queue and the saga stops moving.
  The timeout job finds the upload after 30 seconds without progress and moves it on (see Workspace and Message below).
  If the job can't reach Postgres either, it logs the error and tries again 10 seconds later.

## RabbitMQ

- **Handled (#82):** `POST api/v1/uploads` stores the upload and then publishes `ReserveQuota`. If the publish fails,
  the client gets 500, but the upload is stored as `Pending`. The timeout job rejects it after 30 seconds, so the saga
  still ends.
- **Open:** When the timeout job rejects an upload, it publishes `ReleaseQuota` afterwards. If RabbitMQ is down at that
  moment, the release is lost and the space stays reserved in Workspace. An outbox, like WorkspaceApi has (#66), would
  fix it.
- **Open:** If RabbitMQ is down when FileApi starts, the message worker stops the whole service (#103).

## Vault

- **Handled (#73):** If Vault is sealed, unreachable or the token is wrong, FileApi stops at startup with a message that
  says what to fix ([docs/vault.md](../../docs/vault.md)).

## WorkspaceApi, via ReserveQuota and ReleaseQuota

- **Handled (#80):** Not enough space: Workspace answers `QuotaExceededEvent`, and the upload is `Rejected`. Nothing was
  reserved, so there is nothing to compensate.
- **Handled (#82):** Workspace is down or never answers: after 30 seconds the timeout job rejects the upload and sends
  `ReleaseQuota`. The reject only happens if `PostMessage` hasn't been sent, so a `QuotaReservedEvent` arriving at the
  same moment can't end with a posted message for a rejected upload.
- **Handled (#82):** When Workspace comes back, it may get the release before the reserve, because they are on different
  queues. Workspace remembers the release, so the late reserve reserves nothing.

## Message, via PostMessage

`PostMessage` is the pivot of the saga: once it is sent, the message may be posted, so the saga only goes forward.

- **Handled (#80):** Message can't post: it answers `PostMessageFailedEvent`, FileApi sends `ReleaseQuota` and the
  upload is `Rejected`. The release is sent before the reject, so a failed reject is retried with the release again,
  which Workspace ignores.
- **Handled (#82):** Message never answers: the timeout job sends `PostMessage` again every 30 seconds. Message posts
  once per `SagaId`, so the message is never posted twice.
- **Open:** There is no upper limit. If Message stays down, the upload stays `Pending` and the job keeps asking. Nobody
  is alerted.

## Replies and the timeout job

- **Handled (#80):** A repeated or late reply can't change an upload that is already `Confirmed` or `Rejected`, because
  every status change is a conditional update on `status = 'Pending'`. Late replies are logged and ignored.
- **Handled (#82):** With more than one FileApi instance, two timeout jobs can find the same upload. The conditional
  update lets only one of them reject it, and sending `PostMessage` twice is safe.

## Gateway (Ocelot)

- **Open:** If the gateway or a client retries `POST api/v1/uploads`, a second upload and a second saga are started,
  because there is no idempotency key like `POST /workspaces` has (#67).
- **Open:** FileApi doesn't limit incoming requests.

## External services

FileApi calls no external services. It only stores the metadata of a file, not the file (see
[docs/decisions.md](../../docs/decisions.md)).
