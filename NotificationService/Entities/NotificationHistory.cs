using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using DAL.Entity;

namespace NotificationService.Entities;

[Table("NotificationHistories")]
public sealed class NotificationHistory : ICompositeEntity<NotificationHistory, long, long>
{
    [Key]
    public long NotificationId { get; set; }

    [Key]
    public long UserId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Static column mapping metadata
    public static string TableName => "NotificationHistories";
    public static string Key1ColumnName => nameof(NotificationId);
    public static string Key2ColumnName => nameof(UserId);

    // Instance accessors
    public long Key1 => NotificationId;
    public long Key2 => UserId;
}