using Idefix.Core.Knowledge;

namespace Idefix.ApiTests;

public sealed class PluginScopeTests
{
    private static readonly string[] Available = ["Recht", "Finanzen", "Medizin"];

    [Fact]
    public void AllPluginsAreActiveByDefault()
    {
        Assert.Equal(["Finanzen", "Medizin", "Recht"], PluginScope.Resolve(Available, [], projectScope: null, []));
    }

    [Fact]
    public void GlobalProjectAndSessionScopesNarrowTheSelection()
    {
        var active = PluginScope.Resolve(
            Available,
            globallyDisabled: ["Medizin"],
            projectScope: ["Recht", "Medizin", "Finanzen"],
            sessionDisabled: ["finanzen"]);

        Assert.Equal(["Recht"], active);
    }

    [Fact]
    public void ProjectScopeOnlyIncludesListedPlugins()
    {
        Assert.Equal(["Finanzen"], PluginScope.Resolve(Available, [], ["Finanzen"], []));
    }
}
