using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ADMIN.Data.Dto;
using ADMIN.DTOs.Responses;
using ADMIN.Model;
using ADMIN.Repositories;
using AutoMapper;

namespace ADMIN.Services
{
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

        public async Task<ServiceResult<RequestDetailResponse>> VerifyRequestAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ServiceResult<RequestDetailResponse>.BadRequest("Invalid RequestId. RequestId must be greater than 0.");

            var semaphore = _requestLocks.GetOrAdd(requestId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);
                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ServiceResult<RequestDetailResponse>.NotFound("Request not found");
                }

                if (request.VerifiedByAdmin)
                {
                    _logger.LogWarning("Request {RequestId} is already verified", requestId);
                    return ServiceResult<RequestDetailResponse>.BadRequest("This request has already been verified");
                }

                request.VerifierId = userid;
                request.VerifiedByAdmin = true;
                request.VerifiedAt = DateTime.UtcNow;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Request {RequestId} verified by admin {userid}", requestId, userid);

                var responseDto = _mapper.Map<RequestDetailResponse>(request);
                return ServiceResult<RequestDetailResponse>.Ok(responseDto, "Request verified successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying request {RequestId}", requestId);
                return ServiceResult<RequestDetailResponse>.Error("An error occurred while verifying the request", 500, new List<string> { ex.Message });
            }
            finally
            {
                semaphore.Release();
                _requestLocks.TryRemove(requestId, out _);
            }
        }

        public async Task<ServiceResult<RequestDetailResponse>> GrantUserRightsAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ServiceResult<RequestDetailResponse>.BadRequest("Invalid RequestId. RequestId must be greater than 0.");

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ServiceResult<RequestDetailResponse>.NotFound("Request not found");
                }

                if (!request.VerifiedByAdmin)
                {
                    _logger.LogWarning("Attempted to grant rights to request {RequestId} which is not verified", requestId);
                    return ServiceResult<RequestDetailResponse>.BadRequest("Cannot grant rights: Request must be verified by admin first");
                }

                if (request.RightToAdd)
                {
                    _logger.LogWarning("Request {RequestId} user already has right to add", requestId);
                    return ServiceResult<RequestDetailResponse>.BadRequest("User already has the right to add other users");
                }

                if (request.VerifierId != userid)
                {
                    _logger.LogWarning("Admin {AdminId} attempted to grant rights to request {RequestId} verified by another admin {VerifierId}", userid, requestId, request.VerifierId);
                    return ServiceResult<RequestDetailResponse>.Forbid("Forbidden");
                }

                request.RightToAdd = true;
                request.RightsGrantedAt = DateTime.UtcNow;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Rights granted to request {RequestId} user {UserId} by admin {AdminId}", requestId, request.RequestUserId, userid);

                var responseDto = _mapper.Map<RequestDetailResponse>(request);
                return ServiceResult<RequestDetailResponse>.Ok(responseDto, "User rights granted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting rights to request {RequestId}", requestId);
                return ServiceResult<RequestDetailResponse>.Error("An error occurred while granting user rights", 500, new List<string> { ex.Message });
            }
        }

        public async Task<ServiceResult<RequestDetailResponse>> RevokeUserRightsAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ServiceResult<RequestDetailResponse>.BadRequest("Invalid RequestId. RequestId must be greater than 0.");

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ServiceResult<RequestDetailResponse>.NotFound("Request not found");
                }

                if (!request.RightToAdd)
                {
                    _logger.LogWarning("Request {RequestId} user does not have right to add", requestId);
                    return ServiceResult<RequestDetailResponse>.BadRequest("User does not have the right to add other users");
                }

                if (request.VerifierId != userid)
                {
                    _logger.LogWarning("Admin {AdminId} attempted to revoke rights to request {RequestId} verified by another admin {VerifierId}", userid, requestId, request.VerifierId);
                    return ServiceResult<RequestDetailResponse>.Forbid("Forbidden");
                }

                request.RightToAdd = false;
                request.VerifiedAt = DateTime.UtcNow;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Rights revoked for request {RequestId} user {UserId} by admin {AdminId}", requestId, request.RequestUserId, userid);

                var responseDto = _mapper.Map<RequestDetailResponse>(request);
                return ServiceResult<RequestDetailResponse>.Ok(responseDto, "User rights revoked successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking rights for request {RequestId}", requestId);
                return ServiceResult<RequestDetailResponse>.Error("An error occurred while revoking user rights", 500, new List<string> { ex.Message });
            }
        }

        public async Task<ServiceResult<RequestDetailResponse>> RevokeVerificationAsync(int requestId, int userid)
        {
            if (requestId <= 0)
                return ServiceResult<RequestDetailResponse>.BadRequest("Invalid RequestId. RequestId must be greater than 0.");

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(requestId);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", requestId);
                    return ServiceResult<RequestDetailResponse>.NotFound("Request not found");
                }

                if (!request.VerifiedByAdmin)
                {
                    _logger.LogWarning("Request {RequestId} is already unverified", requestId);
                    return ServiceResult<RequestDetailResponse>.BadRequest("This request is already unverified");
                }

                if (request.VerifierId != userid)
                {
                    _logger.LogWarning("Admin {AdminId} attempted to revoke verification for request {RequestId} verified by another admin {VerifierId}", userid, requestId, request.VerifierId);
                    return ServiceResult<RequestDetailResponse>.Forbid("Forbidden");
                }

                request.VerifiedByAdmin = false;
                request.VerifierId = 0;
                request.VerifiedAt = null;
                request.RightToAdd = false;
                request.RightsGrantedAt = null;

                await _repository.UpdateRequestAsync(request);

                _logger.LogInformation("Verification revoked for request {RequestId} user {UserId} by admin {AdminId}", requestId, request.RequestUserId, userid);

                var responseDto = _mapper.Map<RequestDetailResponse>(request);
                return ServiceResult<RequestDetailResponse>.Ok(responseDto, "Request verification revoked successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking verification for request {RequestId}", requestId);
                return ServiceResult<RequestDetailResponse>.Error("An error occurred while revoking request verification", 500, new List<string> { ex.Message });
            }
        }

        public async Task<ServiceResult<RequestDetailResponse>> GetRequestDetailsAsync(int id)
        {
            if (id <= 0)
                return ServiceResult<RequestDetailResponse>.BadRequest("Invalid RequestId");

            try
            {
                var request = await _repository.GetRequestByUserIdAsync(id);

                if (request == null)
                {
                    _logger.LogWarning("Request not found: {RequestId}", id);
                    return ServiceResult<RequestDetailResponse>.NotFound("Request not found");
                }

                var responseDto = _mapper.Map<RequestDetailResponse>(request);
                return ServiceResult<RequestDetailResponse>.Ok(responseDto, "Request details retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request details for id {RequestId}", id);
                return ServiceResult<RequestDetailResponse>.Error("An error occurred while retrieving request details", 500);
            }
        }

        public async Task<ServiceResult<List<RequestDetailResponse>>> GetUserRequestsAsync(int userId)
        {
            if (userId <= 0)
                return ServiceResult<List<RequestDetailResponse>>.BadRequest("Invalid UserId");

            try
            {
                var requests = await _repository.GetRequestsByVerifierIdAsync(userId);
                var responseDtos = _mapper.Map<List<RequestDetailResponse>>(requests);

                return ServiceResult<List<RequestDetailResponse>>.Ok(responseDtos, $"Retrieved {responseDtos.Count} request(s) for user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving requests for user {UserId}", userId);
                return ServiceResult<List<RequestDetailResponse>>.Error("An error occurred while retrieving user requests", 500);
            }
        }

        public async Task<ServiceResult<List<RequestDetailResponse>>> GetPendingRequestsAsync()
        {
            try
            {
                var pendingRequests = await _repository.GetPendingRequestsAsync();
                var responseDtos = _mapper.Map<List<RequestDetailResponse>>(pendingRequests);
                return ServiceResult<List<RequestDetailResponse>>.Ok(responseDtos, $"Retrieved {responseDtos.Count} pending request(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending requests");
                return ServiceResult<List<RequestDetailResponse>>.Error("An error occurred while retrieving pending requests", 500);
            }
        }

        public async Task<ServiceResult<List<RequestDetailResponse>>> GetVerifiedRequestsAsync(int id = 0)
        {
            List<RequestTable> response = new List<RequestTable>();
            try
            {
                if (id == 0)
                    response = await _repository.GetVerifiedRequestsAsync();
                else
                    response = await _repository.GetRequestsByVerifierIdAsync(id);
                var responseDtos = _mapper.Map<List<RequestDetailResponse>>(response);
                return ServiceResult<List<RequestDetailResponse>>.Ok(responseDtos, $"Retrieved {responseDtos.Count} verified request(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving verified requests");
                return ServiceResult<List<RequestDetailResponse>>.Error("An error occurred while retrieving verified requests", 500);
            }
        }
        public async Task<ServiceResult<List<RequestDetailResponse>>> getAllFilterRequest(Filter filter)
        {
            if(filter.mineId==0)
            return ServiceResult<List<RequestDetailResponse>>.Error("Your Id Is Not Found");
            var existUser=await _repository.GetRequestByUserIdAsync(filter.mineId);
            if (existUser==null)
            {
                return ServiceResult<List<RequestDetailResponse>>.NotFound("Your Current Id related Request we couldn't Find out");
            }
            var response=await _repository.getFilteredData(filter);

            if (response==null || response.Count()==0)
            {
               return ServiceResult<List<RequestDetailResponse>>.Ok(new List<RequestDetailResponse>(),"Not Any Relate Filter Data Found");
            }
            return ServiceResult<List<RequestDetailResponse>>.Ok(_mapper.Map<List<RequestDetailResponse>>(response),"successfully Retrived Data");
        }


       
    }
}
