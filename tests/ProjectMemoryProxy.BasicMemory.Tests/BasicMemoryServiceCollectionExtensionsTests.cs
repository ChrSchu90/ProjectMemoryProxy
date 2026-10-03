namespace ProjectMemoryProxy.BasicMemory.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.Core.Configuration;

/// <summary>
/// Tests for <see cref="BasicMemoryServiceCollectionExtensions"/>
/// </summary>
[TestClass]
public sealed class BasicMemoryServiceCollectionExtensionsTests
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests

    /// <summary>
    /// Verifies that the Basic Memory client can be constructed from the dependency injection container.
    /// </summary>
    [TestMethod]
    public async Task RegisterServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Options.Create(new ProjectMemoryProxyOptions
        {
            BasicMemoryEndpoint = new Uri("http://127.0.0.1:5101/mcp"),
            BasicMemoryConnectionTimeout = TimeSpan.FromSeconds(30)
        }));

        services.AddBasicMemory();
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.IsNotNull(serviceProvider.GetRequiredService<IBasicMemoryClient>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<BasicMemoryToolCatalog>());
        Assert.IsNotNull(serviceProvider.GetRequiredService<BasicMemoryMirroredToolCatalog>());
    }

    #endregion

    #region Private Methods

    #endregion

    #region Test Classes

    #endregion
}
