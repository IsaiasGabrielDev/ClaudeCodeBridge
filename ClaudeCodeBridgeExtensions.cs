using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeCodeBridge;

/// <summary>
/// Extension methods for registering ClaudeCodeBridge into a DI container.
/// </summary>
public static class ClaudeCodeBridgeExtensions
{
    /// <summary>
    /// Registers <see cref="ClaudeAgentService"/> and its dependencies.
    ///
    /// Minimal setup (no DI host, e.g. WinForms):
    /// <code>
    ///   var services = new ServiceCollection();
    ///   services.AddClaudeCodeBridge(opts =>
    ///   {
    ///       opts.ClaudeExecutable = @"C:\...\claude.cmd";
    ///       opts.ProjectDirectory = @"C:\MyAgentProject";
    ///   });
    ///   var provider = services.BuildServiceProvider();
    ///   var agent = provider.GetRequiredService&lt;IClaudeAgentService&gt;();
    /// </code>
    ///
    /// With appsettings.json — section "Claude":
    /// <code>
    ///   services.AddClaudeCodeBridge(configuration.GetSection("Claude"));
    /// </code>
    /// </summary>
    public static IServiceCollection AddClaudeCodeBridge(
        this IServiceCollection services,
        Action<ClaudeOptions> configure)
    {
        services.Configure(configure);
        services.AddLogging();
        services.AddSingleton<IClaudeAgentService, ClaudeAgentService>();
        return services;
    }

    /// <summary>Overload that reads configuration from an <see cref="IConfigurationSection"/>.</summary>
    public static IServiceCollection AddClaudeCodeBridge(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        services.Configure<ClaudeOptions>(section);
        services.AddLogging();
        services.AddSingleton<IClaudeAgentService, ClaudeAgentService>();
        return services;
    }
}
