# Bounded Contexts

## User Profile

Profile Service:
    Owns the user record internally, everything the users see about another user,
    display name, avatar, status etc. Authn is most likely handled by 3rd party.


## Workspace + Channels

Workspace Service:
    Owns workspaces, like Discord servers, which have Channels inside them, and the membership and roles.
    This handles Authz since this might be per workspace scoped, and therefor lives here rather than with authn.

    Workspaces, channels, members, roles, permissions

## Notifications

Notification Service:
    Consumes domain events and pushes emails and unread messages etc.

## Messaging

Message Service:
    Owns messaging: posting, editing, deleting and owns the message history. 
    Store reference to the user, but not the user info. 

# Other containers

## Backend for Frontend

Aggregates responses from the services, validates the access tokens. 
Will have one for each user experience, so Web/Desktop + Mobile.

## Realtime Gateway

Consumes the domain events and pushes them to open connections.

Ex: MessageSent, ReactionAdded.
