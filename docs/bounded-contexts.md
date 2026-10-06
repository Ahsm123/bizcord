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

Workspaces, channels, members, roles, permissions

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

## Backend for Frontend (BFF)

Aggregates responses from the services, validates the access tokens. 
After validating the token, the BFF passes the user ID to the services in a X-User-Id header,
the services trust that header and doesnt validate the token again, only safe because services are only reachable through the BFF.
Will have one for each user experience, so Web/Desktop + Mobile.

**Not:** owns no data and no business logic.

## Realtime Gateway

Consumes the domain events and pushes them to open connections.

Ex: MessageSent, ReactionAdded.

**Not:** owns no data, doesn't send emails (Notification does).

## Data ownership

Each service has its own database. No tables are shared and no service reads another service's database.
Other services only reference Workspace data by the ID, and Workspace does the same with users:
- members.user_id is just an ID and not a foreign key, since users belong to Profile.

Workspace has these tables:
- `workspaces`, `members`, `channels`, `channel_activity`

Example: `channel_activity` stores when a channel last had a message. Workspace owns it but the activity happens in Message.
Message doesnt write to the table, but publishes an `MessagePostedEvent`, and Workspace updates the row when it consumes tha event.
The row is created with a channel and after that its only updated from that event.
