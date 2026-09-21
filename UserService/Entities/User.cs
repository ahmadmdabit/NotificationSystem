using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using DAL.Entity;

namespace UserService.Entities;

[Table("Users")]
public sealed class User : IEntity<User, long>
{
    [Key]
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public byte[] PasswordHash { get; set; } = [];
    public byte[] PasswordSalt { get; set; } = [];
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Static metadata: Evaluated at JIT load time, zero instance overhead
    public static string TableName => "Users";
    public static string KeyColumnName => nameof(Id);
}