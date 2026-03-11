namespace USER.Data.Dto.Response
{
    public class UserDetail
    {
        public int id{get;set;}
        public String Name { get; set; }
        public String Email { get; set; }
        public String Phone { get; set; }
        public String Address { get; set; }
        public String imageUrl{get;set;}="";
        public String Role { get; set; } = "SELLER";
        public String? ProfilePicture { get; set; }
        public String token{get;set;}
    }
}