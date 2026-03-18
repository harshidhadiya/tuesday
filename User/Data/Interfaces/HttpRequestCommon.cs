using ADMIN.Data.Dto;
using Microsoft.AspNetCore.Mvc;
using USER.Data.Dto;
namespace USER.Data.Interfaces
{
    public interface IHttpRequestCommon
    {
        Task<ApiResponse<RequestDetailDto>> GetRequestDetailsAsync(int id);
    }
    public class HttpRequestCommon:IHttpRequestCommon
    {
        HttpClient httpClient;
        public HttpRequestCommon(IHttpClientFactory factory)
        {
            this.httpClient = factory.CreateClient("DefaultClient");
        }
        public async Task<ApiResponse<RequestDetailDto>> GetRequestDetailsAsync(int id)
        {
          var responce = await httpClient.GetAsync($"/api/admin-request/details/{id}");
                
                // Read the response content once
                // I changed this: Updated ApiResponse<object> to ApiResponse<RequestDetailDto> according to actual Admin endpoint response
                var content = await responce.Content.ReadFromJsonAsync<ApiResponse<RequestDetailDto>>();
                
                // Check if response is successful
                if (!responce.IsSuccessStatusCode)
                {
                    return ApiResponse<RequestDetailDto>.ErrorResponse(content?.Message ?? $"Request failed: {responce.StatusCode}",(int)responce.StatusCode,content?.Errors);
                    
                }

                if (content?.Data == null)
                {
                    return ApiResponse<RequestDetailDto>.ErrorResponse("Verification details are missing from the response.",(int)responce.StatusCode,content?.Errors);
                }

                
                return ApiResponse<RequestDetailDto>.SuccessResponse(content?.Data,"Request completed successfully");
        }

        
    }
}