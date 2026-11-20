namespace Bizcord.MessageStub;

// How the stub answers PostMessage, so every path of the upload saga can be tried (docs/upload-saga.md)
public enum StubBehavior
{
    Succeed,
    Fail,
    NoReply
}

public class MessageStubOptions
{
    public StubBehavior Behavior { get; set; } = StubBehavior.Succeed;
}
