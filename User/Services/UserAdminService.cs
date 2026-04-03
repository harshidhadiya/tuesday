using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ADMIN.Data.Dto;
using AutoMapper;
using Name;
using USER.Data.Dto.Response;
using USER.Repository;
using USER.Data.Interfaces;

namespace USER.Services
{
    public class UserAdminService : IUserAdminService
    {
        private readonly IUserRepository _repository;
        private readonly HttpClient _httpClient;
        private readonly ItokenGeneration _token;
        private readonly IMapper _mapper;
        private readonly IHttpRequestCommon httpRequestCommon;

        public UserAdminService(
            IUserRepository repository,
            IHttpClientFactory httpClientFactory,
            ItokenGeneration token,
            IMapper mapper,IHttpRequestCommon httpRequestCommon)
        {
            _repository = repository;
            _httpClient = httpClientFactory.CreateClient("DefaultClient");
            _token = token;
            _mapper = mapper;
            this.httpRequestCommon = httpRequestCommon;
        }



       

       
        public async Task<ServiceResult<AdminDetail>> GetProfileAsync(int userId)
        {

            var currentUser = await _repository.GetByIdAsync(userId);

            if (currentUser == null)
                return ServiceResult<AdminDetail>.NotFound("User not found");

                var result = await httpRequestCommon.GetRequestDetailsAsync(userId);
               
                if(result.Success == false)
                {
                    switch(result.StatusCode)
                    {
                        case 400:
                            return ServiceResult<AdminDetail>.Fail(result.Message,result.StatusCode);
                        case 401:
                            return ServiceResult<AdminDetail>.Fail(result.Message,result.StatusCode);
                        case 403:
                            return ServiceResult<AdminDetail>.Fail(result.Message,result.StatusCode);
                        case 404:
                            return ServiceResult<AdminDetail>.NotFound(result.Message);
                        case 500:
                            return ServiceResult<AdminDetail>.Fail(result.Message,result.StatusCode);
                        default:
                            return ServiceResult<AdminDetail>.Fail(result.Message,result.StatusCode);
                    }   
                }
                
            var response = _mapper.Map<AdminDetail>(currentUser);
            response.obj = result.Data;
            return ServiceResult<AdminDetail>.Ok(response, "User profile retrieved");

        }

       
    }
}
