using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using USER.Data.Interfaces;
using USER.Model;
using USER.Data.Dto;
using MassTransit;
using AutoMapper;
using Name;
using Messaging.Contracts;
using ADMIN.Data.Dto;

public class LoginTests
{
  
    private MACUTIONDB GetDb()
    {
        var options = new DbContextOptionsBuilder<MACUTIONDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MACUTIONDB(options);
    }

    private IHttpContextAccessor GetHttpContext()
    {
        var context = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(context);
        return accessor.Object;
    }

    private UserTable CreateUser(string role, string email)
    {
        return new UserTable
        {
            Id = 1,
            Email = email,
            Name = "Test User",
            Role = role,

            Address = "Surat",
            Phone = "9999999999",
            HashPassword = "hashed-password"
        };
    }


    [Fact]
    public async Task Seller_Login_Should_Return_Ok_When_Valid()
    {
        var db = GetDb();

        db.USERS.Add(CreateUser("SELLER", "user@test.com"));
        await db.SaveChangesAsync();

        var tokenMock = new Mock<ItokenGeneration>();
        tokenMock.Setup(x => x.getToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Returns("jwt");

        var publishMock = new Mock<IPublishEndpoint>();

        var service = new SellerLogin(
            NullLogger<SellerLogin>.Instance,
            new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
            tokenMock.Object,
            db,
            new Mock<IMapper>().Object,
            GetHttpContext(),
            publishMock.Object
        );

        var result = await service.Login(new UserLoginDto
        {
            Email = "user@test.com",
            Role = "SELLER"
        }, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();

        publishMock.Verify(x => x.Publish(It.IsAny<RefreshTokenGenerate>(), default), Times.Once);
    }

    [Fact]
    public async Task Seller_Login_Should_Fail_When_User_Not_Found()
    {
        var service = new SellerLogin(
            NullLogger<SellerLogin>.Instance,
            new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
            new Mock<ItokenGeneration>().Object,
            GetDb(),
            new Mock<IMapper>().Object,
            GetHttpContext(),
            new Mock<IPublishEndpoint>().Object
        );

        var result = await service.Login(new UserLoginDto
        {
            Email = "notfound@test.com",
            Role = "SELLER"
        }, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Seller_Login_Should_Fail_When_Role_Mismatch()
    {
        var db = GetDb();

        db.USERS.Add(CreateUser("USER", "user@test.com"));
        await db.SaveChangesAsync();

        var service = new SellerLogin(
            NullLogger<SellerLogin>.Instance,
            new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
            new Mock<ItokenGeneration>().Object,
            db,
            new Mock<IMapper>().Object,
            GetHttpContext(),
            new Mock<IPublishEndpoint>().Object
        );

        var result = await service.Login(new UserLoginDto
        {
            Email = "user@test.com",
            Role = "SELLER"
        }, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Seller_Login_Should_Fail_When_Invalid_Role()
    {
        var db = GetDb();

        db.USERS.Add(CreateUser("SELLER", "user@test.com"));
        await db.SaveChangesAsync();

        var service = new SellerLogin(
            NullLogger<SellerLogin>.Instance,
            new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
            new Mock<ItokenGeneration>().Object,
            db,
            new Mock<IMapper>().Object,
            GetHttpContext(),
            new Mock<IPublishEndpoint>().Object
        );

        var result = await service.Login(new UserLoginDto
        {
            Email = "user@test.com",
            Role = "ADMIN"
        }, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }


    [Fact]
    public async Task Admin_Login_Should_Return_Ok_When_Valid()
    {
        var db = GetDb();

        db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
        await db.SaveChangesAsync();

        var tokenMock = new Mock<ItokenGeneration>();
        tokenMock.Setup(x => x.getToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Returns("jwt");

        var httpCommon = new Mock<IHttpRequestCommon>();
        httpCommon.Setup(x => x.GetRequestDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync(ApiResponse<RequestDetailDto>.SuccessResponse(
                new RequestDetailDto
                {
                    RequestUserId = 1
                }
            ));

        var publishMock = new Mock<IPublishEndpoint>();

        var service = new AdminLogin(
            NullLogger<AdminLogin>.Instance,
            new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
            tokenMock.Object,
            db,
            new Mock<IMapper>().Object,
            new Mock<IHttpClientFactory>().Object,
            httpCommon.Object,
            GetHttpContext(),
            publishMock.Object
        );

        var result = await service.Login(new UserLoginDto
        {
            Email = "admin@test.com",
            Role = "ADMIN"
        }, new HttpClient());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();

        publishMock.Verify(x => x.Publish(It.IsAny<RefreshTokenGenerate>(), default), Times.Once);
    }

    [Fact]
    public async Task Admin_Login_Should_Fail_When_Service_Returns_400()
    {
        var db = GetDb();

        db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
        await db.SaveChangesAsync();

        var httpCommon = new Mock<IHttpRequestCommon>();
        httpCommon.Setup(x => x.GetRequestDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync(ApiResponse<RequestDetailDto>.ErrorResponse("error", 400));

        var service = new AdminLogin(
            NullLogger<AdminLogin>.Instance,
            new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
            new Mock<ItokenGeneration>().Object,
            db,
            new Mock<IMapper>().Object,
            new Mock<IHttpClientFactory>().Object,
            httpCommon.Object,
            GetHttpContext(),
            new Mock<IPublishEndpoint>().Object
        );

        var result = await service.Login(new UserLoginDto
        {
            Email = "admin@test.com",
            Role = "ADMIN"
        }, new HttpClient());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Admin_Login_Should_Handle_Exception()
    {
        var db = GetDb();

        db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
        await db.SaveChangesAsync();

        var httpCommon = new Mock<IHttpRequestCommon>();
        httpCommon.Setup(x => x.GetRequestDetailsAsync(It.IsAny<int>()))
                  .ThrowsAsync(new Exception("boom"));

        var service = new AdminLogin(
            NullLogger<AdminLogin>.Instance,
            new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
            new Mock<ItokenGeneration>().Object,
            db,
            new Mock<IMapper>().Object,
            new Mock<IHttpClientFactory>().Object,
            httpCommon.Object,
            GetHttpContext(),
            new Mock<IPublishEndpoint>().Object
        );

        var result = await service.Login(new UserLoginDto
        {
            Email = "admin@test.com",
            Role = "ADMIN"
        }, new HttpClient());

        result.Should().BeOfType<BadRequestObjectResult>();
    }
  

[Fact]
public async Task Admin_Login_Should_Fail_When_Role_Mismatch()
{
    var db = GetDb();

    db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
    await db.SaveChangesAsync();

    var service = new AdminLogin(
        NullLogger<AdminLogin>.Instance,
        new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
        new Mock<ItokenGeneration>().Object,
        db,
        new Mock<IMapper>().Object,
        new Mock<IHttpClientFactory>().Object,
        new Mock<IHttpRequestCommon>().Object,
        GetHttpContext(),
        new Mock<IPublishEndpoint>().Object
    );

    var result = await service.Login(new UserLoginDto
    {
        Email = "admin@test.com",
        Role = "SELLER" 
    }, new HttpClient());

    result.Should().BeOfType<BadRequestObjectResult>();
}

[Fact]
public async Task Admin_Login_Should_Fail_When_Not_Admin_Role()
{
    var db = GetDb();

    db.USERS.Add(CreateUser("USER", "admin@test.com")); 
    await db.SaveChangesAsync();

    var service = new AdminLogin(
        NullLogger<AdminLogin>.Instance,
        new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
        new Mock<ItokenGeneration>().Object,
        db,
        new Mock<IMapper>().Object,
        new Mock<IHttpClientFactory>().Object,
        new Mock<IHttpRequestCommon>().Object,
        GetHttpContext(),
        new Mock<IPublishEndpoint>().Object
    );

    var result = await service.Login(new UserLoginDto
    {
        Email = "admin@test.com",
        Role = "USER"
    }, new HttpClient());

    result.Should().BeOfType<BadRequestObjectResult>();
}

[Fact]
public async Task Admin_Login_Should_Return_Unauthorized_When_Service_401()
{
    var db = GetDb();

    db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
    await db.SaveChangesAsync();

    var httpCommon = new Mock<IHttpRequestCommon>();
    httpCommon.Setup(x => x.GetRequestDetailsAsync(It.IsAny<int>()))
        .ReturnsAsync(ApiResponse<RequestDetailDto>.ErrorResponse("unauthorized", 401));

    var service = new AdminLogin(
        NullLogger<AdminLogin>.Instance,
        new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
        new Mock<ItokenGeneration>().Object,
        db,
        new Mock<IMapper>().Object,
        new Mock<IHttpClientFactory>().Object,
        httpCommon.Object,
        GetHttpContext(),
        new Mock<IPublishEndpoint>().Object
    );

    var result = await service.Login(new UserLoginDto
    {
        Email = "admin@test.com",
        Role = "ADMIN"
    }, new HttpClient());

    result.Should().BeOfType<UnauthorizedObjectResult>();
}

[Fact]
public async Task Admin_Login_Should_Return_NotFound_When_Service_404()
{
    var db = GetDb();

    db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
    await db.SaveChangesAsync();

    var httpCommon = new Mock<IHttpRequestCommon>();
    httpCommon.Setup(x => x.GetRequestDetailsAsync(It.IsAny<int>()))
        .ReturnsAsync(ApiResponse<RequestDetailDto>.ErrorResponse("not found", 404));

    var service = new AdminLogin(
        NullLogger<AdminLogin>.Instance,
        new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
        new Mock<ItokenGeneration>().Object,
        db,
        new Mock<IMapper>().Object,
        new Mock<IHttpClientFactory>().Object,
        httpCommon.Object,
        GetHttpContext(),
        new Mock<IPublishEndpoint>().Object
    );

    var result = await service.Login(new UserLoginDto
    {
        Email = "admin@test.com",
        Role = "ADMIN"
    }, new HttpClient());

    result.Should().BeOfType<NotFoundObjectResult>();
}

[Fact]
public async Task Admin_Login_Should_Return_500_When_Service_500()
{
    var db = GetDb();

    db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
    await db.SaveChangesAsync();

    var httpCommon = new Mock<IHttpRequestCommon>();
    httpCommon.Setup(x => x.GetRequestDetailsAsync(It.IsAny<int>()))
        .ReturnsAsync(ApiResponse<RequestDetailDto>.ErrorResponse("server error", 500));

    var service = new AdminLogin(
        NullLogger<AdminLogin>.Instance,
        new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
        new Mock<ItokenGeneration>().Object,
        db,
        new Mock<IMapper>().Object,
        new Mock<IHttpClientFactory>().Object,
        httpCommon.Object,
        GetHttpContext(),
        new Mock<IPublishEndpoint>().Object
    );

    var result = await service.Login(new UserLoginDto
    {
        Email = "admin@test.com",
        Role = "ADMIN"
    }, new HttpClient());

    result.Should().BeOfType<StatusCodeResult>()
          .Which.StatusCode.Should().Be(500);
}

[Fact]
public async Task Admin_Login_Should_Work_When_HttpContext_Is_Null()
{
    var db = GetDb();

    db.USERS.Add(CreateUser("ADMIN", "admin@test.com"));
    await db.SaveChangesAsync();

    var tokenMock = new Mock<ItokenGeneration>();
    tokenMock.Setup(x => x.getToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns("jwt");

    var httpCommon = new Mock<IHttpRequestCommon>();
    httpCommon.Setup(x => x.GetRequestDetailsAsync(It.IsAny<int>()))
        .ReturnsAsync(ApiResponse<RequestDetailDto>.SuccessResponse(new RequestDetailDto()));

    var accessorMock = new Mock<IHttpContextAccessor>();
    accessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null); // NULL

    var publishMock = new Mock<IPublishEndpoint>();

    var service = new AdminLogin(
        NullLogger<AdminLogin>.Instance,
        new Microsoft.AspNetCore.Identity.PasswordHasher<object>(),
        tokenMock.Object,
        db,
        new Mock<IMapper>().Object,
        new Mock<IHttpClientFactory>().Object,
        httpCommon.Object,
        accessorMock.Object,
        publishMock.Object
    );

    var result = await service.Login(new UserLoginDto
    {
        Email = "admin@test.com",
        Role = "ADMIN"
    }, new HttpClient());

    result.Should().BeOfType<OkObjectResult>();
}
}