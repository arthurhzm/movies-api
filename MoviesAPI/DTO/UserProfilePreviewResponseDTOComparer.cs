namespace MoviesAPI.DTO;

public class UserProfilePreviewResponseDTOComparer : IEqualityComparer<UserProfilePreviewResponseDTO>
{
    public bool Equals(UserProfilePreviewResponseDTO? x, UserProfilePreviewResponseDTO? y)
    {
        if (x == null || y == null) return false;
        return x.UserId == y.UserId;
    }

    public int GetHashCode(UserProfilePreviewResponseDTO obj)
    {
        return obj.UserId.GetHashCode();
    }
}