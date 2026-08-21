namespace Tokenometer;

internal interface ISignInState
{
    bool IsSignedIn { get; }

    void MarkSignedIn();

    void Clear();
}
