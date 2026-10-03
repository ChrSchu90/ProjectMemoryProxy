namespace ProjectMemoryProxy.Persistence.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ProjectMemoryProxy.Core.Routing;

/// <summary>
/// Represents a routing to a basic-memory project.
/// </summary>
[Table("ProjectRoutings"), Index(nameof(MemoryProjectId), IsUnique = true), Index(nameof(UpdatedAt))]
internal sealed class ProjectRoutingEntity
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
    /// Gets or sets the basic-memory project identifier.
    /// </summary>
    [Required]
    public Guid MemoryProjectId { get; set; }

    /// <summary>
    /// Gets or sets the project routing status.
    /// </summary>
    [Required]
    public Status Status { get; set; } = Status.Active;

    /// <summary>
    /// Gets or sets when the project routing was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when the project routing was last updated.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the project bindings of the project routing.
    /// </summary>
    [ForeignKey(nameof(ContextBindingEntity.RoutingProjectId))]
    public ICollection<ContextBindingEntity> Bindings { get; set; } = new List<ContextBindingEntity>();
    
    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion

    #region Configuration
    
    #endregion
}
