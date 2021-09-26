using DAL.Entity;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationService.Entities
{
    [Table("NotificationHistories")]
    public class NotificationHistory : BaseEntity
    {
        [Key]
        public long NotificationId { get; set; }
        [Key]
        public long UserId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}