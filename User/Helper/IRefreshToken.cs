using System.Net;
using ADMIN.Data.Dto;
using AUCTION.Helpers;
using CloudinaryDotNet.Actions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Name;
using Serilog;
using USER.Data.Interfaces;
using USER.Model;
using USER.TokenGenerator;

namespace USER.Helper
{
    public interface IRefreshToken
    {
        public Task<(string ?accessToken,string ?refreshToken)> getToken(string token);
        public Task<IActionResult> getResponse(HttpContext context,string path);
        public Task<IActionResult> Logout(HttpContext context,string path);
    }

    public class RefreshToken : IRefreshToken
    {
        private readonly MACUTIONDB _db;
        private readonly ItokenGeneration generator;
        private readonly ILogger<RefreshToken> logger;
        public RefreshToken(MACUTIONDB db,ItokenGeneration tokenGenerator,ILogger<RefreshToken> logger)
        {
            _db=db;
            this.generator=tokenGenerator;
            this.logger=logger;
        }

        public async Task<IActionResult> getResponse(HttpContext context, string path)
        {
             if(!context.Request.Cookies.TryGetValue(tokenType.RefreshToken.ToString(),out var data))
             return new UnauthorizedObjectResult(null);

           logger.LogInformation("this is your token"+data);
           var datas=await this.getToken(data);


           if(datas.accessToken==null || datas.refreshToken==null)
           return new UnauthorizedObjectResult(null);
   
   
   logger.LogInformation("the data has been seed in the cookies"+tokenType.AccessToken);

        //    this from the refreshtoken publish to add the data in the cookies and send the response to the client and this is the static as your eye see that 
            RefreshTokenPublish.addCooKieData(context, datas.refreshToken, path, tokenType.RefreshToken);
            RefreshTokenPublish.addCooKieData(context,datas.accessToken,"/",field:tokenType.AccessToken);
           return new OkObjectResult(ApiResponse<object>.SuccessResponse(new {token=datas.accessToken}));   
        }

        public async Task<(string ?accessToken,string ?refreshToken)>getToken(string token)
        {


            var decoded=System.Net.WebUtility.UrlDecode(token).Replace(" ","+");
            logger.LogInformation("decoded tokens"+decoded);
            var data =await _db.refreshTables.Where(x=>x.refreshToken==decoded).FirstOrDefaultAsync();
            if(data==null || data.expiryDate < TimeHelper.Now()){
                logger.LogInformation("current refresh token relate doesn't any data existed in the database ");
            return (null,null);
}

            var accessToken=generator.getToken(data.name,data.role,data.userId.ToString());
            string refreshToken=RefreshTokenGenerator.GenerateRefreshToken();

          
            data.expiryDate=TimeHelper.Now().AddDays(7);
            data.refreshToken=refreshToken;
            _db.refreshTables.Update(data);
            await _db.SaveChangesAsync();

            logger.LogInformation("refresh token successfully generated and the saved in the database"+refreshToken+" and access token "+accessToken);
            return (accessToken,refreshToken);
        }
        public async Task<IActionResult> Logout(HttpContext context,string path)
        {
            if(!context.Request.Cookies.TryGetValue(tokenType.RefreshToken.ToString(),out var data))
             return new UnauthorizedObjectResult(null);

           var decoded=System.Net.WebUtility.UrlDecode(data).Replace(" ","+");
           var tokenData =await _db.refreshTables.Where(x=>x.refreshToken==decoded).FirstOrDefaultAsync();
           if(tokenData!=null){
            _db.refreshTables.Remove(tokenData);
            await _db.SaveChangesAsync();
           }
           context.Response.Cookies.Delete(tokenType.RefreshToken.ToString(),new CookieOptions{
            Path=path
           });
           context.Response.Cookies.Delete(tokenType.AccessToken.ToString(),new CookieOptions{
            Path="/"
           });
       
           return new OkObjectResult(ApiResponse<object>.SuccessResponse(null,"Logged out successfully"));
        }
    }

}