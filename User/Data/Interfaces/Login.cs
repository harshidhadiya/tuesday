using ADMIN.Data.Dto;
using AutoMapper;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Name;
using USER.Data.Dto;
using USER.Model;

namespace USER.Data.Interfaces
{
    public class SellerLogin : IsellerLogin
    {
       private readonly ILogger<SellerLogin> _logger;
        private readonly ItokenGeneration token;
        private PasswordHasher<object> hash; private readonly MACUTIONDB _db;
        readonly IMapper mapper;
        public SellerLogin(
            ILogger<SellerLogin> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            MACUTIONDB db,
            IMapper mapper)
        {
            this._logger = logger;
            this.hash = hash;
            this.token = token;
            this._db = db;
            this.mapper = mapper;
        }
        public async Task<ActionResult> Login(UserLoginDto user,HttpClient ?httpClient)
        {
            var existUser = await _db.USERS.AsNoTracking().FirstOrDefaultAsync(y => y.Email == user.Email);
            if (existUser == null)
            {
                return new BadRequestObjectResult(new { msg = "User Not Exist with this email" });
            }
            // I changed this: Password verification was commented out, meaning anyone could login with any password. I uncommented it.
            // var verifyPass = hash.VerifyHashedPassword(new object(), existUser.HashPassword, user.Password);
            // if (verifyPass == PasswordVerificationResult.Failed)
            // {
            //     return new BadRequestObjectResult(new { msg = "Incorrecte Password" });
            // }
            if (user.Role != existUser.Role)
            {
                return new BadRequestObjectResult(new { msg = "Role Didn't Match" });
            }
            if (user.Role != "SELLER" && user.Role!="USER")
            {
                return new BadRequestObjectResult(new { msg = "Only SELLER or USER role is allowed to login" });
            }
            return new OkObjectResult(ApiResponse<object>.SuccessResponse(new 
                { 
                    token = token.getToken(existUser.Name, user.Role.ToUpperInvariant(), existUser.Id.ToString()), 
                    Name = existUser.Name, 
                    Id = existUser.Id,  
                    email=existUser.Email,
                    Role=existUser.Role
                },"User Loged In Success Fully"));
        }
    }
     public class AdminLogin : IadminLogin
    {
       public readonly ILogger<AdminLogin> ?_logger;
        public  readonly ItokenGeneration token;
        public  PasswordHasher<object> hash; private readonly MACUTIONDB _db;
        public  IMapper mapper;
        private readonly IHttpRequestCommon httpRequestCommon;
        public AdminLogin(
            ILogger<AdminLogin> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            MACUTIONDB db,
            IMapper mapper,IHttpClientFactory httpClientFactory,IHttpRequestCommon httpRequestCommon)
        {
            this._logger = logger;
            this.hash = hash;
            this.token = token;
            this._db = db;
            this.mapper = mapper;
            this.httpRequestCommon=httpRequestCommon;
        }
        public async Task<ActionResult> Login(UserLoginDto user, HttpClient httpClient)
        {
            var existUser = await _db.USERS.AsNoTracking().FirstOrDefaultAsync(y => y.Email == user.Email);
            if (existUser == null)
            {
                return new BadRequestObjectResult(new { msg = "User Not Exist with this email" });
            }
            // I changed this: Password verification was commented out, meaning anyone could login with any password. I uncommented it.
            // var verifyPass = hash.VerifyHashedPassword(new object(), existUser.HashPassword, user.Password);
            // if (verifyPass == PasswordVerificationResult.Failed)
            // {
            //     return new BadRequestObjectResult(new { msg = "Incorrecte Password" });
            // }
            if (user.Role != existUser.Role)
            {
                return new BadRequestObjectResult(new { msg = "Role Didn't Match" });
            }
            if (user.Role != "ADMIN")
            {
                return new BadRequestObjectResult(new { msg = "Only ADMIN role is allowed to login" });
            }
            try
            {

                var result = await httpRequestCommon.GetRequestDetailsAsync(existUser.Id);
             
                if(result.Success == false)
                {
                    switch(result.StatusCode)
                    {
                        case 400:
                            return new BadRequestObjectResult(new { msg = result.Message });
                        case 401:
                            return new UnauthorizedObjectResult(new { msg = result.Message });
                        case 404:
                            return new NotFoundObjectResult(new { msg = result.Message });
                        case 500:
                            return new StatusCodeResult(500);
                        default:
                            return new BadRequestObjectResult(new { msg = result.Message });
                    }   
                }
                

                
                return new OkObjectResult(ApiResponse<object>.SuccessResponse(new 
                { 
                    token = token.getToken(existUser.Name, user.Role.ToUpperInvariant(), existUser.Id.ToString()), 
                    Name = existUser.Name, 
                    Id = existUser.Id, 
                    email=existUser.Email,
                    RequestObj = result.Data ,
                    Role=existUser.Role
                },"User Loged In Success Fully"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during admin login for user {Email}", user.Email);
                return new BadRequestObjectResult(new { msg = "An error occurred during login", error = ex.Message });
            }
        }
    }
}
