using System;
using System.ComponentModel.DataAnnotations;

namespace UI.Models
{
    public class NotificationHistoryModel
    {
        [Required]
        public long NotificationId { get; set; }
        [Required]
        public long UserId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}