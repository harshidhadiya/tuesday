using AUCTION.Helpers;

namespace USER.Model
{
    public class RefreshTable
    {
        public int Id{get;set;}
        public string name{get;set;}
        public string role{get;set;}
        public int userId{get;set;}
        public string refreshToken{get;set;}
        public DateTime expiryDate{get;set;}
        public DateTime createdAt{get;set;}=TimeHelper.Now();
    }
}