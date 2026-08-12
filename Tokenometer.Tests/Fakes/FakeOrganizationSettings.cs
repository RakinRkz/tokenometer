using Tokenometer;

namespace Tokenometer.Tests.Fakes;

internal sealed class FakeOrganizationSettings : IOrganizationSettings
{
    private string? _organizationId;

    public FakeOrganizationSettings(string? initialOrganizationId = null) => _organizationId = initialOrganizationId;

    public void Save(string organizationId) => _organizationId = organizationId;

    public string? Load() => _organizationId;

    public void Clear() => _organizationId = null;
}
