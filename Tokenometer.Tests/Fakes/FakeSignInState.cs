using Tokenometer;

namespace Tokenometer.Tests.Fakes;

internal sealed class FakeSignInState : ISignInState
{
    public FakeSignInState(bool initiallySignedIn = false) => IsSignedIn = initiallySignedIn;

    public bool IsSignedIn { get; private set; }

    public void MarkSignedIn() => IsSignedIn = true;

    public void Clear() => IsSignedIn = false;
}
