using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADMIN.Data.Dto;
using ADMIN.Repository;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace ADMIN.Services
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ServiceResult<T> Ok(T data, string message = "") => new() { Success = true, StatusCode = 200, Data = data, Message = message };
        public static ServiceResult<T> NotFound(string message = "Not found") => new() { Success = false, StatusCode = 404, Message = message };
        public static ServiceResult<T> BadRequest(string message = "Bad request") => new() { Success = false, StatusCode = 400, Message = message };
        public static ServiceResult<T> Forbid(string message = "Forbidden") => new() { Success = false, StatusCode = 403, Message = message };
        public static ServiceResult<T> Error(string message, int statusCode = 500, List<string>? errors = null) => new() { Success = false, StatusCode = statusCode, Message = message, Errors = errors };
    }

    public class RequestService : IRequestService
    {
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _requestLocks = new();
        private readonly IRequestRepository _repository;
        private readonly IMapper _mapper;
        private readonly ILogger<RequestService> _logger;

        public RequestService(IRequestRepository repository, IMapper mapper, ILogger<RequestService> logger)
        {
            _repository = repository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<RequestDetailDto>> VerifyRequestAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ApiResponse<RequestDetailDto>.ErrorResponse("Invalid RequestId. RequestId must be greater than 0.", 400);

            var semaphore = _requestLocks.GetOrAdd(requestId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);
                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Request not found", 404);
                }

                if (request.VerifiedByAdmin)
                {
                    _logger.LogWarning("Request {RequestId} is already verified", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("This request has already been verified", 400);
                }

                request.VerifierId = userid;
                request.VerifiedByAdmin = true;
                request.VerifiedAt = DateTime.UtcNow;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Request {RequestId} verified by admin {userid}", requestId, userid);

                var responseDto = _mapper.Map<RequestDetailDto>(request);
                return ApiResponse<RequestDetailDto>.SuccessResponse(responseDto, "Request verified successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying request {RequestId}", requestId);
                return ApiResponse<RequestDetailDto>.ErrorResponse("An error occurred while verifying the request", 500, new List<string> { ex.Message });
            }
            finally
            {
                semaphore.Release();
                _requestLocks.TryRemove(requestId, out _);
            }
        }

        public async Task<ApiResponse<RequestDetailDto>> GrantUserRightsAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ApiResponse<RequestDetailDto>.ErrorResponse("Invalid RequestId. RequestId must be greater than 0.", 400);

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Request not found", 404);
                }

                if (!request.VerifiedByAdmin)
                {
                    _logger.LogWarning("Attempted to grant rights to request {RequestId} which is not verified", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Cannot grant rights: Request must be verified by admin first", 400);
                }

                if (request.RightToAdd)
                {
                    _logger.LogWarning("Request {RequestId} user already has right to add", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("User already has the right to add other users", 400);
                }

                if (request.VerifierId != userid)
                {
                    _logger.LogWarning("Admin {AdminId} attempted to grant rights to request {RequestId} verified by another admin {VerifierId}", userid, requestId, request.VerifierId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Forbidden", 403);
                }

                request.RightToAdd = true;
                request.RightsGrantedAt = DateTime.UtcNow;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Rights granted to request {RequestId} user {UserId} by admin {AdminId}", requestId, request.RequestUserId, userid);

                var responseDto = _mapper.Map<RequestDetailDto>(request);
                return ApiResponse<RequestDetailDto>.SuccessResponse(responseDto, "User rights granted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting rights to request {RequestId}", requestId);
                return ApiResponse<RequestDetailDto>.ErrorResponse("An error occurred while granting user rights", 500, new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<RequestDetailDto>> RevokeUserRightsAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ApiResponse<RequestDetailDto>.ErrorResponse("Invalid RequestId. RequestId must be greater than 0.", 400);

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Request not found", 404);
                }

                if (!request.RightToAdd)
                {
                    _logger.LogWarning("Request {RequestId} user does not have right to add", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("User does not have the right to add other users", 400);
                }

                if (request.VerifierId != userid)
                {
                    _logger.LogWarning("Admin {AdminId} attempted to revoke rights to request {RequestId} verified by another admin {VerifierId}", userid, requestId, request.VerifierId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Forbidden", 403);
                }

                request.RightToAdd = false;
                request.VerifiedAt = DateTime.UtcNow;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Rights revoked for request {RequestId} user {UserId} by admin {AdminId}", requestId, request.RequestUserId, userid);

                var responseDto = _mapper.Map<RequestDetailDto>(request);
                return ApiResponse<RequestDetailDto>.SuccessResponse(responseDto, "User rights revoked successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking rights for request {RequestId}", requestId);
                return ApiResponse<RequestDetailDto>.ErrorResponse("An error occurred while revoking user rights", 500, new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<RequestDetailDto>> RevokeVerificationAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ApiResponse<RequestDetailDto>.ErrorResponse("Invalid RequestId. RequestId must be greater than 0.", 400);

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Request not found", 404);
                }

                if (!request.VerifiedByAdmin)
                {
                    _logger.LogWarning("Request {RequestId} is already unverified", requestId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("This request is already unverified", 400);
                }

                if (request.VerifierId != userid)
                {
                    _logger.LogWarning("Admin {AdminId} attempted to revoke verification for request {RequestId} verified by another admin {VerifierId}", userid, requestId, request.VerifierId);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Forbidden", 403);
                }

                request.VerifiedByAdmin = false;
                request.VerifierId = 0;
                request.VerifiedAt = null;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Verification revoked for request {RequestId} user {UserId} by admin {AdminId}", requestId, request.RequestUserId, userid);

                var responseDto = _mapper.Map<RequestDetailDto>(request);
                return ApiResponse<RequestDetailDto>.SuccessResponse(responseDto, "Request verification revoked successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking verification for request {RequestId}", requestId);
                return ApiResponse<RequestDetailDto>.ErrorResponse("An error occurred while revoking request verification", 500, new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<RequestDetailDto>> GetRequestDetailsAsync(int id)
        {
            if (id <= 0)
                return ApiResponse<RequestDetailDto>.ErrorResponse("Invalid RequestId", 400);

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(id);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", id);
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Request not found", 404);
                }

                var responseDto = _mapper.Map<RequestDetailDto>(request);
                return ApiResponse<RequestDetailDto>.SuccessResponse(responseDto, "Request details retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request details for id {RequestId}", id);
                return ApiResponse<RequestDetailDto>.ErrorResponse("An error occurred while retrieving request details", 500);
            }
        }

        public async Task<ApiResponse<List<RequestDetailDto>>> GetUserRequestsAsync(int userId)
        {
            if (userId <= 0)
                return ApiResponse<List<RequestDetailDto>>.ErrorResponse("Invalid UserId", 400);

            try
            {
                var requests = await _repository.GetRequestsByVerifierIdAsync(userId);
                var responseDtos = _mapper.Map<List<RequestDetailDto>>(requests);

                return ApiResponse<List<RequestDetailDto>>.SuccessResponse(responseDtos, $"Retrieved {responseDtos.Count} request(s) for user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for user {UserId}", userId);
                return ApiResponse<List<RequestDetailDto>>.ErrorResponse("An error occurred while retrieving user requests", 500);
            }
        }

        public async Task<ApiResponse<List<RequestDetailDto>>> GetPendingRequestsAsync()
        {
            try
            {
                var pendingRequests = await _repository.GetPendingRequestsAsync();
                var responseDtos = _mapper.Map<List<RequestDetailDto>>(pendingRequests);
                return ApiResponse<List<RequestDetailDto>>.SuccessResponse(responseDtos, $"Retrieved {responseDtos.Count} pending request(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending requests");
                return ApiResponse<List<RequestDetailDto>>.ErrorResponse("An error occurred while retrieving pending requests", 500);
            }
        }

        public async Task<ApiResponse<List<RequestDetailDto>>> GetVerifiedRequestsAsync()
        {
            try
            {
                var verifiedRequests = await _repository.GetVerifiedRequestsAsync();
                var responseDtos = _mapper.Map<List<RequestDetailDto>>(verifiedRequests);
                return ApiResponse<List<RequestDetailDto>>.SuccessResponse(responseDtos, $"Retrieved {responseDtos.Count} verified request(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving verified requests");
                return ApiResponse<List<RequestDetailDto>>.ErrorResponse("An error occurred while retrieving verified requests", 500);
            }
        }

        public async Task<ApiResponse<object>> GetDashboardAsync()
        {
            try
            {
                var pendingCount = await _repository.GetPendingCountAsync();
                var verifiedCount = await _repository.GetVerifiedCountAsync();
                
                return ApiResponse<object>.SuccessResponse(new
                {
                    pendingCount,
                    verifiedCount,
                    message = "Admin request dashboard for showcase"
                }, "Request dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request dashboard");
                return ApiResponse<object>.ErrorResponse("An error occurred while retrieving dashboard", 500);
            }
        }
    }
}
