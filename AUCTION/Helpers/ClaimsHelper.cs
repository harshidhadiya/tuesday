using System.Security.Claims;

namespace AUCTION.Helpers;

public static class ClaimsHelper
{
    public static int GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                 ?? user.FindFirst("sub")
                 ?? user.FindFirst("userId") ??  user.FindFirst("id");

        if (claim == null || !int.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("User ID not found in token");

        return id;
    }

    public static int GetVerifyId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("verifyId");
        if (claim == null || !int.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("VerifyId not found in token");
        return id;
    }

    public static string GetRole(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Role)?.Value ?? "USER";

    public static bool IsAdmin(ClaimsPrincipal user)
        => GetRole(user).Equals("ADMIN", StringComparison.OrdinalIgnoreCase);

    // Your UserService sets isVerified = "true" in JWT when a user is verified
    public static bool IsVerified(ClaimsPrincipal user)
        => user.HasClaim("isVerified", "true");
}
