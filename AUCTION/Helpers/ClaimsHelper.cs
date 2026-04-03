using System.Security.Claims;

namespace AUCTION.Helpers;

public static class ClaimsHelper
{
    public static int GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                 ?? user.FindFirst("sub")
                 ?? user.FindFirst("userId") ??  user.FindFirst("id");
        var data=user.Identity?.AuthenticationType=="Bearer";
        if (claim == null || !int.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("User ID not found in token");

        return id;
    }

    

}
