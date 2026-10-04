namespace ProjectMemoryProxy.Persistence.Entities;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Represents a link from binding IDs to a basic-memory project.
/// </summary>
[Table("ContextBindings"), Index(nameof(BindingType), nameof(BindingName), IsUnique = true)]
internal sealed class ContextBindingEntity
{
    #region Static Fields

    #endregion

    #region Private Fields

    #endregion

    #region Constructors

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the opaque identifier.
    /// </summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the routing project owner.
    /// </summary>
    public long RoutingProjectId { get; set; }

    /// <summary>
    /// Gets or sets the routing project owner.
    /// </summary>
    [ForeignKey(nameof(RoutingProjectId)), InverseProperty(nameof(ProjectRoutingEntity.Bindings))]
    public ProjectRoutingEntity RoutingProject { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of the binding
    /// </summary>
    [Required]
    public BindingType BindingType { get; set; }

    /// <summary>
    /// Gets or sets the binding name.
    /// </summary>
    [Required, MaxLength(255)]
    public string BindingName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the binding status.
    /// </summary>
    [Required]
    public Status Status { get; set; } = Status.Active;

    /// <summary>
    /// Gets or sets when the binding was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the binding was last updated.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion

    #region Configuration
    
    #endregion
}
