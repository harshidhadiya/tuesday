using ADMIN.Data.Dto;
using AutoMapper;
using Microsoft.AspNetCore.Http.HttpResults;
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
            var verifyPass = hash.VerifyHashedPassword(new object(), existUser.HashPassword, user.Password);
            if (verifyPass == PasswordVerificationResult.Failed)
            {
                return new BadRequestObjectResult(new { msg = "Incorrecte Password" });
            }
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
                },"User Loged In Success Fully"));
        }
    }
     public class AdminLogin : IadminLogin
    {
       public readonly ILogger<AdminLogin> ?_logger;
        public  readonly ItokenGeneration token;
        public  PasswordHasher<object> hash; private readonly MACUTIONDB _db;
        public  IMapper mapper;
        public AdminLogin(
            ILogger<AdminLogin> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            MACUTIONDB db,
            IMapper mapper,IHttpClientFactory httpClientFactory)
        {
            this._logger = logger;
            this.hash = hash;
            this.token = token;
            this._db = db;
            this.mapper = mapper;
        }
        public async Task<ActionResult> Login(UserLoginDto user, HttpClient httpClient)
        {
            var existUser = await _db.USERS.AsNoTracking().FirstOrDefaultAsync(y => y.Email == user.Email);
            if (existUser == null)
            {
                return new BadRequestObjectResult(new { msg = "User Not Exist with this email" });
            }
            // I changed this: Password verification was commented out, meaning anyone could login with any password. I uncommented it.
            var verifyPass = hash.VerifyHashedPassword(new object(), existUser.HashPassword, user.Password);
            if (verifyPass == PasswordVerificationResult.Failed)
            {
                return new BadRequestObjectResult(new { msg = "Incorrecte Password" });
            }
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
                var responce = await httpClient.GetAsync($"/api/admin-request/details/{existUser.Id}");
                
                // Read the response content once
                // I changed this: Updated ApiResponse<object> to ApiResponse<RequestDetailDto> according to actual Admin endpoint response
                var content = await responce.Content.ReadFromJsonAsync<ApiResponse<RequestDetailDto>>();
                
                // Check if response is successful
                if (!responce.IsSuccessStatusCode)
                {
                    return new ObjectResult(new { message = content?.Message ?? $"Request failed: {responce.StatusCode}", errors = content?.Errors })
                    {
                        StatusCode = content?.StatusCode > 0 ? content.StatusCode : (int)responce.StatusCode
                    };
                }

                if (content?.Data == null)
                {
                    return new BadRequestObjectResult(new { msg = content?.Message ?? "Verification details are missing from the response." });
                }

                if (!content.Data.VerifiedByAdmin)
                {
                    return new BadRequestObjectResult(new { msg = "Your admin account has not been verified yet. Please wait for verification." });
                }
                return new OkObjectResult(ApiResponse<object>.SuccessResponse(new 
                { 
                    token = token.getToken(existUser.Name, user.Role.ToUpperInvariant(), existUser.Id.ToString()), 
                    Name = existUser.Name, 
                    Id = existUser.Id, 
                    RequestObj = content?.Data 
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
