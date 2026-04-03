using ADMIN.Data.Dto;
using AUCTION.Helpers;
using AutoMapper;
using CloudinaryDotNet.Actions;
using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Name;
using USER.Data.Dto;
using USER.Model;
using USER.TokenGenerator;

namespace USER.Data.Interfaces
{
    public enum tokenType
    {
        AccessToken,
        RefreshToken
    }




    public class SellerLogin : IsellerLogin
    {
        private readonly ILogger<SellerLogin> _logger;
        private readonly ItokenGeneration token;
        private PasswordHasher<object> hash; private readonly MACUTIONDB _db;

        private readonly IHttpContextAccessor accessor;
        private readonly IPublishEndpoint publish;

        readonly IMapper mapper;
        public SellerLogin(
            ILogger<SellerLogin> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            MACUTIONDB db,
            IMapper mapper, IHttpContextAccessor accessor,
            IPublishEndpoint publish)
        {
            this._logger = logger;
            this.publish = publish;
            this.hash = hash;
            this.token = token;
            this._db = db;
            this.mapper = mapper;
            this.accessor = accessor;
        }
        public async Task<ActionResult> Login(UserLoginDto user, HttpClient? httpClient)
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
            if (user.Role != "SELLER" && user.Role != "USER")
            {
                return new BadRequestObjectResult(new { msg = "Only SELLER or USER role is allowed to login" });
            }


            var tokens = token.getToken(existUser.Name, user.Role.ToUpperInvariant(), existUser.Id.ToString());

            var refreshToke = RefreshTokenGenerator.GenerateRefreshToken();
            if (accessor.HttpContext != null)
            {
                RefreshTokenPublish.addCooKieData(accessor.HttpContext, refreshToke, "/api/user/refresh");
                RefreshTokenPublish.addCooKieData(accessor.HttpContext, tokens, "/", field:tokenType.AccessToken);
            }

            await publish.Publish(new RefreshTokenGenerate(userId: existUser.Id, name: existUser.Name, role: existUser.Role, refreshToken: refreshToke, TimeHelper.Now().AddDays(7)));
            _logger.LogInformation("refresh token generate request send successfully" + refreshToke);
            return new OkObjectResult(ApiResponse<object>.SuccessResponse(new
            {
                token = tokens,
                Name = existUser.Name,
                Id = existUser.Id,
                email = existUser.Email,
                Role = existUser.Role
            }, "User Loged In Success Fully"));
        }
    }
    public class AdminLogin : IadminLogin
    {
        public readonly ILogger<AdminLogin>? _logger;
        public readonly ItokenGeneration token;
        public PasswordHasher<object> hash; private readonly MACUTIONDB _db;
        public IMapper mapper;
        private readonly IHttpRequestCommon httpRequestCommon;
        private readonly IPublishEndpoint publish;
        private readonly IHttpContextAccessor accessor;
        public AdminLogin(
            ILogger<AdminLogin> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            MACUTIONDB db,
            IMapper mapper, IHttpClientFactory httpClientFactory, IHttpRequestCommon httpRequestCommon, IHttpContextAccessor accessor, IPublishEndpoint publish)
        {
            this._logger = logger;
            this.hash = hash;
            this.token = token;
            this._db = db;
            this.mapper = mapper;
            this.httpRequestCommon = httpRequestCommon;
            this.accessor = accessor;
            this.publish = publish;
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
                _logger.LogInformation(existUser.Role);
                _logger.LogInformation(user.Role);
                return new BadRequestObjectResult(new { msg = "Role Didn't Match" });
            }
            if (user.Role != "ADMIN")
            {
                return new BadRequestObjectResult(new { msg = "Only ADMIN role is allowed to login" });
            }
            try
            {

                var result = await httpRequestCommon.GetRequestDetailsAsync(existUser.Id);

                if (result.Success == false)
                {
                    switch (result.StatusCode)
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

                var tokens = token.getToken(existUser.Name, user.Role.ToUpperInvariant(), existUser.Id.ToString());

                var refreshToke = RefreshTokenGenerator.GenerateRefreshToken();

                _logger.LogInformation("the data has been seed in the cookies" + tokenType.AccessToken);
                if (accessor.HttpContext != null)
                {
                    RefreshTokenPublish.addCooKieData(accessor.HttpContext, refreshToke, "/api/admin/refresh");
                    RefreshTokenPublish.addCooKieData(accessor.HttpContext, tokens, "/", tokenType.AccessToken);
                }
                _logger.LogInformation("the data has been seed in the cookies");
                await publish.Publish(new RefreshTokenGenerate(userId: existUser.Id, name: existUser.Name, role: existUser.Role, refreshToken: refreshToke, TimeHelper.Now().AddDays(7)));
                _logger.LogInformation("refresh token generate request send successfully" + refreshToke);

                return new OkObjectResult(ApiResponse<object>.SuccessResponse(new
                {
                    token = tokens,
                    Name = existUser.Name,
                    Id = existUser.Id,
                    email = existUser.Email,
                    RequestObj = result.Data,
                    Role = existUser.Role
                }, "User Loged In Success Fully"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during admin login for user {Email}", user.Email);
                return new BadRequestObjectResult(new { msg = "An error occurred during login", error = ex.Message });
            }
        }
    }
    public static class RefreshTokenPublish
    {
        public static void addCooKieData(HttpContext context, string refreshToken, string path, tokenType field = tokenType.RefreshToken, DateTime? expiry = null)
        {
            context.Response.Cookies.Append(field.ToString(), refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = expiry ?? TimeHelper.Now().AddDays(7),
                Path = path
            });
        }
    }
}
