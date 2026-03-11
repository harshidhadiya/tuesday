using System.Security.Cryptography;

namespace USER.Data.Dto.Response
{
    public class pendingVerificationResponse
    {
        public int Id{get;set;}
        public string Name{get;set;}
        public string Role{get;set;}
        public string Email{get;set;}
        public int RequestUserId{get;set;}
        public bool VerifiedByAdmin{get;set;}=false;
        public bool HasRightToAdd{get;set;}=false;
        public DateTime ?VerifiedAt{get;set;}
        public DateTime ?RightsGrantedAt{get;set;}
    }
}