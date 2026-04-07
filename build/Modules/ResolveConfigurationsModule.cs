using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using Shouldly;
using System.Xml.Linq;

namespace Build.Modules;

/// <summary>
///     Resolve solution configurations required to compile the add-in for all supported Revit versions.
/// </summary>
public sealed class ResolveConfigurationsModule : Module<string[]> {
    protected override Task<string[]?> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken) {
        var configurations = LoadConfigurations(context, cancellationToken)
            .Where(configuration => configuration.StartsWith("Release.R", StringComparison.OrdinalIgnoreCase))
            .Where(configuration => !configuration.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        configurations.ShouldNotBeEmpty("No release configurations have been found in Directory.Build.props");

        return Task.FromResult<string[]?>(configurations);
    }

    private static IEnumerable<string> LoadConfigurations(IPipelineContext context, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var propsFile = context.Git().RootDirectory.GetFile("Directory.Build.props");
        propsFile.Exists.ShouldBeTrue("Directory.Build.props not found.");

        var document = XDocument.Load(propsFile.Path);

        return document
            .Descendants("Configurations")
            .SelectMany(element => (element.Value ?? string.Empty)
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(configuration => !configuration.StartsWith("$(", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
