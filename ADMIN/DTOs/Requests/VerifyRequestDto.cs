using System.ComponentModel.DataAnnotations;

namespace ADMIN.DTOs.Requests
{
    public class VerifyRequestDto
    {
        [Required(ErrorMessage = "RequestId is required")]
        public int RequestId { get; set; }
    }

    public class GrantUserRightsDto
    {
        public int RequestId { get; set; }
        public int ApprovedByAdminId { get; set; }
    }

    public class CreateRequestDto
    {
        public int RequestUserId { get; set; }
    }
}
