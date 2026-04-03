using FluentAssertions;
using AutoMapper;
using USER.Services;
using USER.Repository;
using USER.Data.Interfaces;
using USER.Data.Dto.Response;
using USER.Model;
using Moq;
using Name;
using USER.Data.Dto;
using ADMIN.Data.Dto;

public class UserAdminServiceTests
{
    private readonly Mock<IUserRepository> repo;
    private readonly Mock<IHttpClientFactory> httpFactory;
    private readonly Mock<ItokenGeneration> token;
    private readonly Mock<IMapper> mapper;
    private readonly Mock<IHttpRequestCommon> httpRequestCommon;

    private readonly UserAdminService service;

    public UserAdminServiceTests()
    {
        repo = new();
        httpFactory = new();
        token = new();
        mapper = new();
        httpRequestCommon = new();

        var httpClient = new HttpClient();
        httpFactory.Setup(x => x.CreateClient("DefaultClient"))
                   .Returns(httpClient);

        service = new UserAdminService(
            repo.Object,
            httpFactory.Object,
            token.Object,
            mapper.Object,
            httpRequestCommon.Object
        );
    }

    [Fact]
    public async Task GetProfileAsync_Should_Return_NotFound_When_User_Not_Exist()
    {
        var userId = 1;



        var result = await service.GetProfileAsync(userId);

        result.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task GetProfileAsync_Should_Handle_Error_StatusCodes(int statusCode)
    {
        var userId = 1;
        var user = new UserTable { Id = userId };

        repo.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        ApiResponse<RequestDetailDto> apiResult = statusCode switch
        {
            400 => ApiResponse<RequestDetailDto>.ErrorResponse("error", 400),
            401 => ApiResponse<RequestDetailDto>.ErrorResponse("error", 401),
            404 => ApiResponse<RequestDetailDto>.ErrorResponse("error", 404),
            500 => ApiResponse<RequestDetailDto>.ErrorResponse("error", 500),
            _ => ApiResponse<RequestDetailDto>.ErrorResponse("error", statusCode)
        };

        httpRequestCommon.Setup(x => x.GetRequestDetailsAsync(userId))
            .ReturnsAsync(apiResult);

        var result = await service.GetProfileAsync(userId);

        if (statusCode == 401)
            result.StatusCode.Should().Be(401); // Forbidden
        else
            result.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task GetProfileAsync_Should_Return_Ok_When_All_Valid()
    {
        var userId = 1;

        var user = new UserTable { Id = userId };
        var mapped = new AdminDetail();

        var externalData = new RequestDetailDto { Name = "test" };

        repo.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        httpRequestCommon.Setup(x => x.GetRequestDetailsAsync(userId))
            .ReturnsAsync(ApiResponse<RequestDetailDto>.SuccessResponse(externalData));

        mapper.Setup(x => x.Map<AdminDetail>(user))
            .Returns(mapped);

        var result = await service.GetProfileAsync(userId);

        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data.obj.Should().BeEquivalentTo(externalData);
    }
}


