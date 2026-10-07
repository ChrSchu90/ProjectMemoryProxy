namespace ProjectMemoryProxy.Core.Tests.Routing;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Tests for <see cref="ContextId"/>
/// </summary>
[TestClass]
public sealed class ContextIdTests
{
    #region Private Fields

    #endregion

    #region Tests Constructors

    #endregion

    #region Test Preparation

    #endregion

    #region Tests


    /// <summary>
    /// Verifies that binding components are formatted into the canonical technical context identifier.
    /// </summary>
    [TestMethod]
    [DataRow(BindingType.ChatGptProject, "chatty-mcp-and-aiharborvm", "chatgpt-project:chatty-mcp-and-aiharborvm")]
    [DataRow(BindingType.GitRepository, "github.com/ChrSchu90/ProjectMemoryProxy", "git:github.com/ChrSchu90/ProjectMemoryProxy")]
    public void CreateReturnsCanonicalContextId(BindingType bindingType, string bindingName, string expected)
    {
        var contextId = ContextId.Create(bindingType, bindingName);

        Assert.AreEqual(bindingType, contextId.BindingType);
        Assert.AreEqual(bindingName, contextId.BindingName);
        Assert.AreEqual(expected, contextId.ToString());
    }

    /// <summary>
    /// Verifies that invalid binding names are rejected when creating a canonical context identifier.
    /// </summary>
    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(" repository")]
    [DataRow("repository ")]
    public void CreateRejectsInvalidBindingNames(string bindingName)
    {
        Assert.ThrowsExactly<ArgumentException>(() => ContextId.Create(BindingType.GitRepository, bindingName));
    }

    /// <summary>
    /// Verifies that a canonical ChatGPT project context is parsed into its expected type and name.
    /// </summary>
    [TestMethod]
    public void TryParseParsesChatGptProjectContext()
    {
        var success = ContextId.TryParse("chatgpt-project:chatty-mcp-and-aiharborvm", out var contextId);

        Assert.IsTrue(success);
        Assert.IsNotNull(contextId);
        Assert.AreEqual(BindingType.ChatGptProject, contextId.BindingType);
        Assert.AreEqual("chatty-mcp-and-aiharborvm", contextId.BindingName);
    }

    /// <summary>
    /// Verifies that a canonical Git repository context is parsed into its expected type and name.
    /// </summary>
    [TestMethod]
    public void TryParseParsesGitRepositoryContext()
    {
        var success = ContextId.TryParse("git:github.com/ChrSchu90/ProjectMemoryProxy", out var contextId);

        Assert.IsTrue(success);
        Assert.IsNotNull(contextId);
        Assert.AreEqual(BindingType.GitRepository, contextId.BindingType);
        Assert.AreEqual("github.com/ChrSchu90/ProjectMemoryProxy", contextId.BindingName);
    }

    /// <summary>
    /// Verifies that only the first colon separates the context type from the opaque binding name.
    /// </summary>
    [TestMethod]
    public void TryParsePreservesColonInsideBindingName()
    {
        var success = ContextId.TryParse("git:git.example.com:2222/owner/repository", out var contextId);

        Assert.IsTrue(success);
        Assert.IsNotNull(contextId);
        Assert.AreEqual("git.example.com:2222/owner/repository", contextId.BindingName);
    }

    /// <summary>
    /// Verifies that parsing and formatting a canonical context identifier preserves the original value.
    /// </summary>
    [TestMethod]
    public void ToStringReturnsCanonicalContextId()
    {
        const string value = "git:github.com/ChrSchu90/ProjectMemoryProxy";

        Assert.IsTrue(ContextId.TryParse(value, out var contextId));
        Assert.AreEqual(value, contextId!.ToString());
    }

    /// <summary>
    /// Verifies that invalid or non-canonical context identifiers are rejected.
    /// </summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("git:")]
    [DataRow(":repository")]
    [DataRow("unknown:value")]
    [DataRow("GIT:github.com/owner/repository")]
    [DataRow(" git:github.com/owner/repository")]
    [DataRow("git:github.com/owner/repository ")]
    [DataRow("git: github.com/owner/repository")]
    public void TryParseRejectsInvalidContextIds(string? value)
    {
        var success = ContextId.TryParse(value, out var contextId);

        Assert.IsFalse(success);
        Assert.IsNull(contextId);
    }

    #endregion

    #region Private Methods

    #endregion

    #region Test Classes

    #endregion
}
