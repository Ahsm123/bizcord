# Decisions

## East/west security for WorkspaceApi (#72)

**Internal endpoint.** `GET internal/channels/{channelId}/members/{userId}` lets Message ask Workspace if
a user can post in a channel, since Workspace owns membership. Clients can't call it, it answers for any user,
not the caller. Chosen over copying membership into Message via events, at the cost of posting depending on
WorkspaceApi being up.

**Roles.** Tokens have `role: user` for clients or `role: internal-service` for services, as the task asks.
Public endpoints require `user`, so unknown token types are denied by default. 

**WorkspaceApi validates tokens itself** (signature, issuer, audience, expiry) with a fallback policy, so a
direct call inside the compose network is rejected too. The acting user comes from `sub`, not `X-User-Id`.

**Known gaps.** Every validating service holds the signing key. The faucet issues any role,
so it must stay development only.
