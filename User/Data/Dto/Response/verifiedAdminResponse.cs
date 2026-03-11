using System.Security.Cryptography;

namespace USER.Data.Dto.Response
{
    public class verifiedAdminResponse
    {
        public string Name {get;set;}
        public string  Address {get;set;} 
        public string Phone {get;set;} 
        public string imgurl {get;set;}
        public bool isVerified {get;set;}
        public DateTime? verifiedAt {get;set;}
        public string email{get;set;}
    }
}