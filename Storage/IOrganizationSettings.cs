namespace Tokenometer;

internal interface IOrganizationSettings
{
    void Save(string organizationId);

    string? Load();

    void Clear();
}
