using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using DAL.Entity;

namespace NotificationService.Entities;

[Table("Notifications")]
public sealed class Notification : IEntity<Notification, long>
{
    [Key]
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public static string TableName => "Notifications";
    public static string KeyColumnName => nameof(Id);
}