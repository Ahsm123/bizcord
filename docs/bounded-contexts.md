# Bounded Contexts

## User Profile

Profile Service:
    Owns the user record internally, everything the users see about another user,
    display name, avatar, status etc. Authn is most likely handled by 3rd party.

**Not:** authn, workspace membership or roles.

## Workspace + Channels

Workspace Service:
    Owns workspaces, like Discord servers, which have Channels inside them, and the membership and roles.
    This handles Authz since this might be per workspace scoped, and therefor lives here rather than with authn.

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

## Backend for Frontend

Aggregates responses from the services, validates the access tokens. 
Will have one for each user experience, so Web/Desktop + Mobile.

**Not:** owns no data and no business logic.

## Realtime Gateway

Consumes the domain events and pushes them to open connections.

Ex: MessageSent, ReactionAdded.

**Not:** owns no data, doesn't send emails (Notification does).
