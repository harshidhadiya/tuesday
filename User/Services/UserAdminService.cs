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
using USER.Data.Dto.Response;
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



        public async Task<ServiceResult<List<verifiedAdminResponse>>> GetAllVerifiedRequestsAsync(int userId, int page, int size)
        {
            var requests = await _httpClient.GetAsync($"/api/admin-request/user/{userId}");
            if (!requests.IsSuccessStatusCode)
            {
                var error = await requests.Content.ReadFromJsonAsync<ApiResponse<object>>();
                int statusCode = error?.StatusCode > 0 ? error.StatusCode : (int)requests.StatusCode;
                return ServiceResult<List<verifiedAdminResponse>>.Fail(error?.Message ?? $"Request failed with status {requests.StatusCode}", statusCode);
            }

            var response = await requests.Content.ReadFromJsonAsync<ApiResponse<List<Responce_of_verified_by_me>>>();
            
            if (response?.Data == null || !response.Data.Any())
            {
                return ServiceResult<List<verifiedAdminResponse>>.NotFound(response?.Message ?? "No Users Verified By You");
            }

            var datas = await _repository.GetAllUsersAsync();

            var joinedData = datas.Join(
                response.Data,
                c => c.Id,
                p => p.RequestUserId,
                (c, p) => new verifiedAdminResponse { Name = c.Name, Address = c.Address, Phone = c.Phone, imgurl = c.ProfilePicture, isVerified = p.VerifiedByAdmin, verifiedAt = p.VerifiedAt, email = c.Email }
            ).Skip((page - 1) * size).Take(size).ToList();

            return ServiceResult<List<verifiedAdminResponse>>.Ok(joinedData, $"{joinedData.Count()} fetched successfully");
        }

        public async Task<ServiceResult<List<pendingVerificationResponse>>> GetAllPendingRequestsAsync(int page=1, int size=10)
        {
            var requests = await _httpClient.GetAsync("/api/admin-request/pending");
            if (!requests.IsSuccessStatusCode)
            {
                var error = await requests.Content.ReadFromJsonAsync<ApiResponse<object>>();
                int statusCode = error?.StatusCode > 0 ? error.StatusCode : (int)requests.StatusCode;
                return ServiceResult<List<pendingVerificationResponse>>.Fail(error?.Message ?? $"Failed to retrieve pending requests: {requests.StatusCode}", statusCode);
            }

            var response = await requests.Content.ReadFromJsonAsync<ApiResponse<List<RequestDetailDto>>>();

            if (response?.Data == null || !response.Data.Any())
            {
                return ServiceResult<List<pendingVerificationResponse>>.NotFound(response?.Message ?? "No pending requests found");
            }

            var requestUserIds = response.Data.Select(r => r.RequestUserId).ToList();

            var usersFromDb = await _repository.GetUsersByIdsAsync(requestUserIds,page,size);

            var users = usersFromDb.Join(
                response!.Data!,
                u => u.Id,
                r => r.RequestUserId,
                (u, r) => new pendingVerificationResponse
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    RequestUserId = r.RequestUserId,
                    VerifiedByAdmin = r.VerifiedByAdmin,
                    HasRightToAdd = r.HasRightToAdd,
                    VerifiedAt = r.VerifiedAt,
                    RightsGrantedAt = r.RightsGrantedAt
                })
                .ToList();
            if(users==null || users.Count()==0)
            return ServiceResult<List<pendingVerificationResponse>>.NotFound("No any pending verifications");

            
            return ServiceResult<List<pendingVerificationResponse>>.Ok(users,"Successfully found users");
        }

       
    }
}
