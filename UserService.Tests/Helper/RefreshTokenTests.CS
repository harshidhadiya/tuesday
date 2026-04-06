using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using USER.Helper;
using USER.Model;
using Name;
using USER.Data.Interfaces;

public class RefreshTokenTests
{
    // ✅ DB Setup
    private MACUTIONDB GetDb()
    {
        var options = new DbContextOptionsBuilder<MACUTIONDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MACUTIONDB(options);
    }

    // ✅ FIXED: Cookie via Headers (IMPORTANT)
    private DefaultHttpContext GetContext(string? token = null)
    {
        var context = new DefaultHttpContext();

        if (token != null)
        {
            context.Request.Headers["Cookie"] =
                $"{tokenType.RefreshToken}={token}";
        }

        return context;
    }

    // ===========================
    // ✅ getToken TESTS
    // ===========================

    [Fact]
    public async Task GetToken_Should_Return_New_Tokens_When_Valid()
    {
        var db = GetDb();

        db.refreshTables.Add(new RefreshTable
        {
            userId = 1,
            name = "Harshid",
            role = "USER",
            refreshToken = "valid-token",
            expiryDate = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var tokenMock = new Mock<ItokenGeneration>();
        tokenMock.Setup(x => x.getToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Returns("access-token");

        var service = new RefreshToken(db, tokenMock.Object, NullLogger<RefreshToken>.Instance);

        var result = await service.getToken("valid-token");

        result.accessToken.Should().NotBeNull();
        result.refreshToken.Should().NotBeNull();
    }

    [Fact]
    public async Task GetToken_Should_Return_Null_When_NotFound()
    {
        var db = GetDb();

        var service = new RefreshToken(
            db,
            new Mock<ItokenGeneration>().Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.getToken("invalid");

        result.accessToken.Should().BeNull();
        result.refreshToken.Should().BeNull();
    }

    [Fact]
    public async Task GetToken_Should_Return_Null_When_Expired()
    {
        var db = GetDb();

        db.refreshTables.Add(new RefreshTable
        {
            userId = 1,
            name = "Harshid",
            role = "USER",
            refreshToken = "expired",
            expiryDate = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var service = new RefreshToken(
            db,
            new Mock<ItokenGeneration>().Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.getToken("expired");

        result.accessToken.Should().BeNull();
        result.refreshToken.Should().BeNull();
    }

    // ===========================
    // ✅ getResponse TESTS
    // ===========================

    [Fact]
    public async Task GetResponse_Should_Return_Unauthorized_When_No_Cookie()
    {
        var db = GetDb();
        var context = GetContext();

        var service = new RefreshToken(
            db,
            new Mock<ItokenGeneration>().Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.getResponse(context, "/path");

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetResponse_Should_Return_Unauthorized_When_Invalid_Token()
    {
        var db = GetDb();
        var context = GetContext("invalid");

        var service = new RefreshToken(
            db,
            new Mock<ItokenGeneration>().Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.getResponse(context, "/path");

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetResponse_Should_Return_Ok_When_Valid()
    {
        var db = GetDb();

        db.refreshTables.Add(new RefreshTable
        {
            userId = 1,
            name = "Harshid",
            role = "USER",
            refreshToken = "valid",
            expiryDate = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var tokenMock = new Mock<ItokenGeneration>();
        tokenMock.Setup(x => x.getToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Returns("new-access");

        var context = GetContext("valid");

        var service = new RefreshToken(
            db,
            tokenMock.Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.getResponse(context, "/refresh");

        result.Should().BeOfType<OkObjectResult>();
    }

    // ===========================
    // ✅ Logout TESTS
    // ===========================

    [Fact]
    public async Task Logout_Should_Return_Unauthorized_When_No_Cookie()
    {
        var db = GetDb();
        var context = GetContext();

        var service = new RefreshToken(
            db,
            new Mock<ItokenGeneration>().Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.Logout(context, "/logout");

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Logout_Should_Remove_Token_When_Exists()
    {
        var db = GetDb();

        db.refreshTables.Add(new RefreshTable
        {
            userId = 1,
            name = "Harshid",
            role = "USER",
            refreshToken = "valid",
            expiryDate = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var context = GetContext("valid");

        var service = new RefreshToken(
            db,
            new Mock<ItokenGeneration>().Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.Logout(context, "/logout");

        result.Should().BeOfType<OkObjectResult>();
        db.refreshTables.Should().BeEmpty();
    }

    [Fact]
    public async Task Logout_Should_Return_Ok_When_Token_Not_In_DB()
    {
        var db = GetDb();
        var context = GetContext("not-exist");

        var service = new RefreshToken(
            db,
            new Mock<ItokenGeneration>().Object,
            NullLogger<RefreshToken>.Instance
        );

        var result = await service.Logout(context, "/logout");

        result.Should().BeOfType<OkObjectResult>();
    }
}