namespace VERIFY.DTOs.Responses
{
    //enlisting the usersummary from the getting http call right  
    public class UserSummary
    {
        public int id { get; set; }
        public string? name { get; set; }
        public string? email { get; set; }
        public string? role { get; set; }
    }
}
