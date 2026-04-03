namespace USER.Practice
{
    public static  class PracticeEndpoint 
    {
        public static void MapPracticeEndpoint(this IEndpointRouteBuilder builder)
        {
            builder.MapGet("/hello", () =>
            {
                return Results.Ok("hello");
            });
        }
    }
}