# Bounded Contexts

## User Profile

Profile Service:
    Owns the user record internally, everything the users see about another user,
    display name, avatar, status etc. 
    Authn is handled by an external identity provider (idp) and not by any of these services.

**Not:** **authn, workspace membership or roles.

## Workspace + Channels

Workspace Service:
    Owns workspaces, like Discord servers, which have Channels inside them, and the membership and roles.
    Channels are not their own service since they only exist inside a workspace and use the same members and roles.
    This handles authz because the roles are per workspace, so it lives here instead of with authn in the idp.

Workspaces, channels, members, roles, permissions, and each workspace's storage quota
(see the [upload saga](upload-saga.md)).

**Not:** messages, profile data, authn.

## Notifications

Notification Service:
    Consumes domain events and pushes emails and unread messages etc.

**Not:** owns no messages or user data, only reacts to events. Live updates to open clients are the Realtime Gateway's job.

## Messaging

Message Service:
    Owns messaging: posting, editing, deleting and owns the message history. 
    Store reference to the user, but not the user info. 
    Also owns reactions, since a reaction always belongs to a message.

**Not:** user info, workspaces or channels.

# Other containers

## API Gateway

The single entry point for clients. Routes each request to the right service and validates the access token.
After validating the token, the gateway passes the user ID to the services in a X-User-Id header, taken from the token's `sub` claim.
The services trust that header and don't validate the token again, which is only safe when services are only reachable through the gateway.
In development the gateway also has a faucet that issues tokens, as a stand-in for the idp.

**Not:** owns no data and no business logic, and doesn't decide what a user may do in a workspace (Workspace does).

## Backend for Frontend (BFF)

Aggregates responses from the services.
Will have one for each user experience, so Web/Desktop + Mobile.

**Not:** owns no data and no business logic. Doesn't validate tokens, the API Gateway does.

## Realtime Gateway

Consumes the domain events and pushes them to open connections.

Ex: MessageSent, ReactionAdded.

**Not:** owns no data, doesn't send emails (Notification does).

## Data ownership

Each service has its own database. No tables are shared and no service reads another service's database.
Other services only reference Workspace data by the ID, and Workspace does the same with users:
- members.user_id is just an ID and not a foreign key, since users belong to Profile.

Workspace has these tables:
- `workspaces` (including the storage limit and used space), `members`, `channels`, `channel_activity`,
  `quota_reservations`

Example: `channel_activity` stores when a channel last had a message. Workspace owns it but the activity happens in Message.
Message doesnt write to the table, but publishes an `MessagePostedEvent`, and Workspace updates the row when it consumes tha event.
The row is created with a channel and after that its only updated from that event.

Example: the storage quota belongs to the workspace, so Workspace owns it, even though the uploads live in File.
File asks Workspace to reserve space with a `ReserveQuota` command, and Workspace answers with an event.
`quota_reservations` has one row per upload saga, so a repeated command doesn't reserve the space twice.
