namespace QAAutomation.API.Models;

/// <summary>
/// Many-to-many join: which users have access to which projects.
/// Admins implicitly have access to all projects; this table is used for non-admin users.
/// </summary>
public class UserProjectAccess
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}
