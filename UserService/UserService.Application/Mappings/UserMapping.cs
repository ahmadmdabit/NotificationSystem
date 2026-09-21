namespace UserService.Application.Mappings;

/// <summary>
/// Mapping methods for User domain entity to DTOs.
/// </summary>
public static class UserMapping
{
    public static DTOs.UserReadDto ToReadDto(this Domain.Entities.User user)
    {
        return new DTOs.UserReadDto
        {
            Id = user.Id,
            Username = user.Username,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public static DTOs.UserDto ToDto(this Domain.Entities.User user)
    {
        return new DTOs.UserDto
        {
            Id = user.Id,
            Username = user.Username,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
