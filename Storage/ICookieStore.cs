namespace Tokenometer;

internal interface ICookieStore
{
    void Save(string cookieHeader);

    string? Load();

    void Clear();
}
