using System.Collections.Concurrent;

namespace Bizcord.MessageStub;

// Remembers which sagas already have a message, so a repeated PostMessage posts nothing new.
// In memory is enough for a stub. A restart forgets it, which a real Message service with a database would not.
public class PostedMessages
{
    private readonly ConcurrentDictionary<Guid, Guid> _messageIds = new();
    private readonly ConcurrentDictionary<Guid, bool> _announced = new();

    // The same saga always gets the same message id
    public Guid MessageIdFor(Guid sagaId) => _messageIds.GetOrAdd(sagaId, _ => Guid.NewGuid());

    // True the first time for a saga. Call Forget if publishing then fails, so a retry announces it again.
    public bool TryClaimAnnouncement(Guid sagaId) => _announced.TryAdd(sagaId, true);

    public void Forget(Guid sagaId) => _announced.TryRemove(sagaId, out _);
}
