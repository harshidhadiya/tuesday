namespace USER.Data.Dto.Response
{
    public class userProfileResponce
    {
        public int id { get; set; }
        public String Name { get; set; }
        public String Email { get; set; }
        public String Phone { get; set; }
        public String Address { get; set; }
        public String imageUrl { get; set; } = "";
        public String Role { get; set; } = "SELLER";

    }
}