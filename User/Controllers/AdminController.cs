using ADMIN.Data.Dto;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Name;
using USER.Data.Dto;
using USER.Data.Interfaces;
using USER.Messaging;
using USER.Model;

namespace USER.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly HttpClient _httpClient;
        private readonly ItokenGeneration _token;
        private readonly PasswordHasher<object> _hash;
        private readonly MACUTIONDB _db;
        private readonly IMapper _mapper;
        private readonly IadminLogin _adminLogin;
        private readonly IRabbitMqPublisher _publisher;

        public AdminController(
            ILogger<UserController> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            MACUTIONDB db,
            IMapper mapper,
            IHttpClientFactory httpClientFactory,
            IadminLogin adminLogin,
            IRabbitMqPublisher publisher)
        {
            _logger = logger;
            _hash = hash;
            _token = token;
            _db = db;
            _mapper = mapper;
            _httpClient = httpClientFactory.CreateClient("DefaultClient");
            _adminLogin = adminLogin;
            _publisher = publisher;
        }
        [HttpPost("request/signup")]
        public async Task<ActionResult> requestSignup(UserCreateDto request)
        {
            if (request.Role != "ADMIN")
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("Cannot request signup for USER role", 400));
            }

            var existingUser = await _db.USERS.FirstOrDefaultAsync(x => x.Email == request.Email);
            if (existingUser != null)
            {
                return BadRequest("User already exists with this email");
            }
            var userData = _mapper.Map<UserTable>(request);
            _db.USERS.Add(userData);
            await _db.SaveChangesAsync();

           
            var requestBody = new { RequestUserId = userData.Id ,Name=userData.Name,Email=userData.Email};
            try
            {
                _publisher.Publish<object>("request.created",requestBody);           
                return Ok(ApiResponse<object>.SuccessResponse(new{id=userData.Id,Name=userData.Name,Email=userData.Email,phone=userData.Phone,profilepic=userData.ProfilePicture
                ,tokens=_token.getToken(userData.Name,"ADMIN",userData.Id.ToString())}, "User created and request submitted successfully"));
            }
            catch (Exception)
            {
                var data = await _db.USERS.FindAsync(userData.Id);
                if (data != null)
                {
                    _db.USERS.Remove(data);
                    await _db.SaveChangesAsync();
                }
            }
            return BadRequest(ApiResponse<object>.ErrorResponse("Request creation failed. User created but admin request could not be created.", 400));
        }

        [HttpPost("Login")]
        public async Task<ActionResult> Login(UserLoginDto user)
        {
            return await _adminLogin.Login(user, _httpClient);
        }

        [HttpGet("getallverifiedrequests")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> GetAllVerifiedRequests()
        {
            var currentUserId = HttpContext.Items["id"]?.ToString();
            if (!int.TryParse(currentUserId, out var userId))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid User ID in token", 400));
            }
            var requests = await _httpClient.GetAsync($"/api/Request/user/{userId}");
            if (!requests.IsSuccessStatusCode)
            {
                var error=await requests.Content.ReadFromJsonAsync<ApiResponse<object>>();
             
                return BadRequest(ApiResponse<object>.ErrorResponse($"Failed to retrieve verified requests: {error?.Message}", (int)requests.StatusCode));
           }
            
            var responce = await requests.Content.ReadFromJsonAsync<ApiResponse<List<Responce_of_verified_by_me>>>();
            if (responce==null)
            {
                return NoContent();
            }
            var datas=await _db.USERS.ToListAsync();
            if(responce.Data == null || responce.Data.Count == 0)
            {
                return Ok(ApiResponse<object>.SuccessResponse(datas, "All verified requests retrieved successfully " + responce?.Message));
            }
            var joinedData=   datas.Join(responce.Data,x=>x.Id,y=>y.RequestUserId,(c,p)=>new{Name=c.Name,Address=c.Address,Phone=c.Phone,imgurl=c.ProfilePicture,isVerified=p.VerifiedByAdmin,verifiedAt=p.VerifiedAt,email=c.Email});
            return Ok(ApiResponse<object>.SuccessResponse(joinedData, "All verified requests retrieved successfully " + responce?.Message));
        }
// WE DON'T REQUIRE THIS ALREADY IN THE  REQUSET CONTROLLER
        [HttpGet("pendingrequests")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> GetAllPendingRequests()
        {
            var requests = await _httpClient.GetAsync("/api/Request/pending");
            if (!requests.IsSuccessStatusCode)
            {
                var error=await requests.Content.ReadFromJsonAsync<ApiResponse<object>>();
                return BadRequest(ApiResponse<object>.ErrorResponse($"Failed to retrieve pending requests: {error?.Message}", (int)requests.StatusCode));
            }

            var responce = await requests.Content.ReadFromJsonAsync<ApiResponse<List<RequestDetailDto>>>();

            // Fetch user IDs from the requests
            var requestUserIds = responce.Data?.Select(r => r.RequestUserId).ToList() ?? new List<int>();
            
            var usersFromDb = await _db.USERS
                .Where(u => requestUserIds.Contains(u.Id))
                .ToListAsync();

            var users = usersFromDb.Join(
                responce.Data,
                u => u.Id,
                r => r.RequestUserId,
                (u, r) => new 
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    RequestId = r.Id,
                    VerifiedByAdmin = r.VerifiedByAdmin,
                    HasRightToAdd = r.HasRightToAdd,
                    CreatedAt = r.CreatedAt,
                    VerifiedAt = r.VerifiedAt,
                    RightsGrantedAt = r.RightsGrantedAt
                })
                .ToList();

            return Ok(ApiResponse<object>.SuccessResponse(users, "All pending requests retrieved successfully"));
        }
         
        


        [HttpGet("dashboard")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> GetAdminDashboard()
        {
            var currentUserId = HttpContext.Items["id"]?.ToString();
            if (!int.TryParse(currentUserId, out var userId))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid User ID in token", 400));

            var pendingResponse = await _httpClient.GetAsync("/api/Request/pending");
            var verifiedByMeResponse = await _httpClient.GetAsync($"/api/Request/user/{userId}");

            var pendingCount = 0;
            List<RequestDetailDto>? verifiedByMe = null;
            if (pendingResponse.IsSuccessStatusCode)
            {
                var pendingData = await pendingResponse.Content.ReadFromJsonAsync<ApiResponse<List<RequestDetailDto>>>();
                pendingCount = pendingData?.Data?.Count ?? 0;
            }
            if (verifiedByMeResponse.IsSuccessStatusCode)
            {
                var verifiedData = await verifiedByMeResponse.Content.ReadFromJsonAsync<ApiResponse<List<RequestDetailDto>>>();
                verifiedByMe = verifiedData?.Data;
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                pendingRequestCount = pendingCount,
                verifiedByMeCount = verifiedByMe?.Count ?? 0,
                verifiedByMe,
                message = "Admin dashboard data for showcase"
            }, "Admin dashboard"));
        }


    }
}
