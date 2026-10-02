namespace ProjectMemoryProxy.Persistence.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a link from binding IDs to a basic-memory project.
/// </summary>
[Table("ContextBindings"), Owned, Index(nameof(BindingType), nameof(BindingName), IsUnique = true)]
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
    /// Gets or sets the type of the binding
    /// </summary>
    [Required]
    public BindingType BindingType { get; set; }

    /// <summary>
    /// Gets or sets the binding name.
    /// </summary>
    [Required]
    public string BindingName { get; set; } = null!;
    
    #endregion

    #region Public Methods

    #endregion

    #region Private Methods

    #endregion

    #region Configuration
    
    #endregion
}
