using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ADMIN.Data.Dto;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Name;
using USER.Data.Dto;
using USER.Messaging;
using USER.Model;
using USER.Repository;

namespace USER.Services
{
    public class UserAdminService : IUserAdminService
    {
        private readonly IUserRepository _repository;
        private readonly HttpClient _httpClient;
        private readonly ItokenGeneration _token;
        private readonly IMapper _mapper;
        private readonly IRabbitMqPublisher _publisher;

        public UserAdminService(
            IUserRepository repository,
            IHttpClientFactory httpClientFactory,
            ItokenGeneration token,
            IMapper mapper,
            IRabbitMqPublisher publisher)
        {
            _repository = repository;
            _httpClient = httpClientFactory.CreateClient("DefaultClient");
            _token = token;
            _mapper = mapper;
            _publisher = publisher;
        }

        public async Task<ActionResult> RequestSignupAsync(UserCreateDto request)
        {
            if (request.Role != "ADMIN")
            {
                return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse("Cannot request signup for USER role", 400));
            }

            var existingUser = await _repository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return new BadRequestObjectResult("User already exists with this email");
            }

            var userData = _mapper.Map<UserTable>(request);
            await _repository.AddAsync(userData);

            var requestBody = new { RequestUserId = userData.Id, Name = userData.Name, Email = userData.Email };
            try
            {
                _publisher.Publish<object>("request.created", requestBody);
                return new OkObjectResult(ApiResponse<object>.SuccessResponse(new
                {
                    id = userData.Id,
                    Name = userData.Name,
                    Email = userData.Email,
                    phone = userData.Phone,
                    profilepic = userData.ProfilePicture,
                    tokens = _token.getToken(userData.Name, "ADMIN", userData.Id.ToString())
                }, "User created and request submitted successfully"));
            }
            catch (Exception)
            {
                var data = await _repository.GetByIdAsync(userData.Id);
                if (data != null)
                {
                    await _repository.RemoveAsync(data);
                }
                return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse("Request creation failed. User created but admin request could not be created.", 400));
            }
        }

        public async Task<ActionResult> GetAllVerifiedRequestsAsync(int userId)
        {
            var requests = await _httpClient.GetAsync($"/api/Request/user/{userId}");
            if (!requests.IsSuccessStatusCode)
            {
                var error = await requests.Content.ReadFromJsonAsync<ApiResponse<object>>();
                return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse($"Failed to retrieve verified requests: {error?.Message}", (int)requests.StatusCode));
            }

            var response = await requests.Content.ReadFromJsonAsync<ApiResponse<List<Responce_of_verified_by_me>>>();
            if (response == null)
            {
                return new NoContentResult();
            }

            var datas = await _repository.GetAllUsersAsync();
            if (response.Data == null || response.Data.Count == 0)
            {
                return new OkObjectResult(ApiResponse<object>.SuccessResponse(datas, "All verified requests retrieved successfully " + response?.Message));
            }

            var joinedData = datas.Join(
                response.Data,
                c => c.Id,
                p => p.RequestUserId,
                (c, p) => new { Name = c.Name, Address = c.Address, Phone = c.Phone, imgurl = c.ProfilePicture, isVerified = p.VerifiedByAdmin, verifiedAt = p.VerifiedAt, email = c.Email }
            );

            return new OkObjectResult(ApiResponse<object>.SuccessResponse(joinedData, "All verified requests retrieved successfully " + response?.Message));
        }

        public async Task<ActionResult> GetAllPendingRequestsAsync()
        {
            var requests = await _httpClient.GetAsync("/api/Request/pending");
            if (!requests.IsSuccessStatusCode)
            {
                var error = await requests.Content.ReadFromJsonAsync<ApiResponse<object>>();
                return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse($"Failed to retrieve pending requests: {error?.Message}", (int)requests.StatusCode));
            }

            var response = await requests.Content.ReadFromJsonAsync<ApiResponse<List<RequestDetailDto>>>();

            var requestUserIds = response?.Data?.Select(r => r.RequestUserId).ToList() ?? new List<int>();

            var usersFromDb = await _repository.GetUsersByIdsAsync(requestUserIds);

            var users = usersFromDb.Join(
                response!.Data!,
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

            return new OkObjectResult(ApiResponse<object>.SuccessResponse(users, "All pending requests retrieved successfully"));
        }

        public async Task<ActionResult> GetAdminDashboardAsync(int userId)
        {
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

            return new OkObjectResult(ApiResponse<object>.SuccessResponse(new
            {
                pendingRequestCount = pendingCount,
                verifiedByMeCount = verifiedByMe?.Count ?? 0,
                verifiedByMe,
                message = "Admin dashboard data for showcase"
            }, "Admin dashboard"));
        }
    }
}
