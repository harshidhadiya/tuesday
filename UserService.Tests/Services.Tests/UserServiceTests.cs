
using AutoMapper;
using FluentAssertions;
using Helper;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using USER.CloudinaryService;
using USER.Data.Dto;
using USER.Data.Dto.Response;
using USER.Model;
using USER.Repository;
using USER.Services;
namespace UserService_Tests.Services.Tests
{

    public class UserServiceTests
    {
        private readonly UserService service;
        private readonly Mock<ILogger<UserService>> logger;
        private readonly Mock<IMapper> mapper;
        private readonly Mock<IUserRepository> repo;
        private readonly Mock<IPublishEndpoint> publish;
        private readonly Mock<IClodinaryService> cloudinary;
        private readonly Mock<Ihelper> helper;

        public UserServiceTests()
        {
            logger = new();
            mapper = new();
            repo = new();
            publish = new();
            cloudinary = new();
            helper = new();
            service = new UserService(repo.Object, logger.Object, mapper.Object, publish.Object, cloudinary.Object, helper.Object);

        }
        [Theory]
        [InlineData("user@gmail.com", false, 200, 1)]
        [InlineData("user1@gmail.com", true, 200, 2)]
        public async Task ChangeProfileAsync_ReturnOwnDetail_whenStatusIsOk(string email, bool haveFile, int statuseCode, int userId)
        {
            // Given
            var file = haveFile ? FileHelper.CreateFakeFile() : null;
            var data = new changeProfileDto
            {
                Name = "harshid",
                Email = email,
                file = file
            };
            var currentUser = new UserTable { Name = data.Name, Email = data.Email, Id = userId };
            var (url, publicId) = ("url", "publicId");
            var response = new OwnDetail { Name = data.Name, Email = data.Email, id = userId };

            repo.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(currentUser);
            helper.Setup(x => x.ProfileImageUpdate(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync((url, publicId));
            repo.Setup(x => x.changeFields(data, userId)).ReturnsAsync(currentUser);
            mapper.Setup(x => x.Map<OwnDetail>(currentUser)).Returns(response);

            var result = await service.ChangeProfileAsync(userId, data);

            result.Should().NotBeNull();
            result.Should().BeAssignableTo<ServiceResult<OwnDetail>>();
            result.StatusCode.Should().Be(statuseCode);
            result.Data.Should().NotBeNull();
            result.Data.Should().BeAssignableTo<OwnDetail>();
            result.Data.Should().BeEquivalentTo(response);


            if (haveFile)
                helper.Verify(x => x.ProfileImageUpdate(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Once);

            repo.Verify(x => x.GetByIdAsync(userId), Times.Once);
            repo.Verify(x => x.changeFields(data, userId), Times.Once);
            mapper.Verify(x => x.Map<OwnDetail>(currentUser), Times.Once);


        }
        [Fact]
        public async Task ChangeProfileAsync_Should_Return_NotFound_When_User_Does_Not_Exist()
        {
            var userId = 1;
            var data = new changeProfileDto();


            var result = await service.ChangeProfileAsync(userId, data);

            result.StatusCode.Should().Be(404);

            repo.Verify(x => x.changeFields(It.IsAny<changeProfileDto>(), It.IsAny<int>()), Times.Never);
        }
        [Fact]
        public async Task ChangeProfileAsync_Should_Delete_Image_When_Update_Fails_With_File()
        {
            var userId = 1;
            var file = FileHelper.CreateFakeFile();

            var data = new changeProfileDto { file = file };

            var existingUser = new UserTable { Id = userId };

            repo.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            helper.Setup(x => x.ProfileImageUpdate(It.IsAny<IFormFile>(), It.IsAny<string>()))
                  .ReturnsAsync(("url", "publicId"));



            var result = await service.ChangeProfileAsync(userId, data);

            result.StatusCode.Should().Be(500);

            cloudinary.Verify(x => x.deleteFile("publicId"), Times.Once);
        }
        [Fact]
        public async Task ChangeProfileAsync_Should_Not_Delete_Image_When_No_File_And_Update_Fails()
        {
            var userId = 1;

            var data = new changeProfileDto { file = null };

            var existingUser = new UserTable { Id = userId };

            repo.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);


            var result = await service.ChangeProfileAsync(userId, data);

            result.StatusCode.Should().Be(500);

            cloudinary.Verify(x => x.deleteFile(It.IsAny<string>()), Times.Never);
        }
        [Fact]
        public async Task GetProfileAsync_Should_Return_Ok_When_User_Exists()
        {
            // Arrange
            var userId = 1;

            var user = new UserTable { Id = userId, Name = "harshid" };

            var mapped = new UserDetail { id = userId, Name = "harshid" };

            repo.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            mapper.Setup(x => x.Map<UserDetail>(user))
                  .Returns(mapped);

            // Act
            var result = await service.GetProfileAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(200);
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEquivalentTo(mapped);

            repo.Verify(x => x.GetByIdAsync(userId), Times.Once);
            mapper.Verify(x => x.Map<UserDetail>(user), Times.Once);
        }

        [Fact]
        public async Task GetProfileAsync_Should_Return_NotFound_When_User_Does_Not_Exist()
        {
            // Arrange
            var userId = 1;


            // Act
            var result = await service.GetProfileAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(404);
            result.Data.Should().BeNull();

            repo.Verify(x => x.GetByIdAsync(userId), Times.Once);
            mapper.Verify(x => x.Map<UserDetail>(It.IsAny<UserTable>()), Times.Never);
        }


        // user already exist 
        [Fact]
        public async Task CreateUserAsync_Should_Return_Fail_When_User_Exists()
        {
            var dto = new UserCreateDto { Email = "test@gmail.com" };

            repo.Setup(x => x.GetByEmailAsync(dto.Email))
                .ReturnsAsync(new UserTable());

            var result = await service.CreateUserAsync(dto);

            result.StatusCode.Should().Be(400);

            repo.Verify(x => x.AddAsync(It.IsAny<UserTable>()), Times.Never);
        }



        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateUserAsync_Should_Create_User_With_File(bool haveFile)
        {
            var file = haveFile ? FileHelper.CreateFakeFile() : null;

            var dto = new UserCreateDto { Email = "test@gmail.com", file = file };

            var userEntity = new UserTable { Id = 1 };
            var response = new UserTable { Id = 1, Role = "USER", Email = dto.Email };
            var mapped = new OwnDetail { id = 1, Email = response.Email };

            if (haveFile)
                cloudinary.Setup(x => x.singleUpload(file))
                    .ReturnsAsync(("url", "publicId"));

            mapper.Setup(x => x.Map<UserTable>(dto))
                .Returns(userEntity);

            repo.Setup(x => x.AddAsync(userEntity))
                .ReturnsAsync(response);

            mapper.Setup(x => x.Map<OwnDetail>(response))
                .Returns(mapped);

            var result = await service.CreateUserAsync(dto);

            result.StatusCode.Should().Be(200);
            result.Data.Should().NotBeNull();
            result.Data.Email.Should().Be(dto.Email);
            if (haveFile)
            {
                cloudinary.Verify(x => x.singleUpload(file), Times.Once);
                userEntity.publicPictureId.Should().Be("publicId");
                userEntity.ProfilePicture.Should().Be("url");
            }
            else
                cloudinary.Verify(x => x.singleUpload(file), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_Should_Delete_File_When_AddAsync_Fails()
        {
            var file = FileHelper.CreateFakeFile();

            var dto = new UserCreateDto { Email = "test@gmail.com", file = file };

            var userEntity = new UserTable();

            repo.Setup(x => x.GetByEmailAsync(dto.Email))
                .ReturnsAsync((UserTable)null);

            cloudinary.Setup(x => x.singleUpload(file))
                .ReturnsAsync(("url", "publicId"));

            mapper.Setup(x => x.Map<UserTable>(dto))
                .Returns(userEntity);

            repo.Setup(x => x.AddAsync(userEntity))
                .ReturnsAsync((UserTable)null);

            var result = await service.CreateUserAsync(dto);

            result.StatusCode.Should().Be(400);

            cloudinary.Verify(x => x.deleteFile("publicId"), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_Should_Publish_Event_When_Role_Is_Admin()
        {
            var dto = new UserCreateDto { Email = "test@gmail.com" };

            var userEntity = new UserTable { Id = 1, Name = "harshid", Email = dto.Email };
            var response = new UserTable { Id = 1, Role = "ADMIN", Name = "harshid", Email = dto.Email };

            repo.Setup(x => x.GetByEmailAsync(dto.Email))
                .ReturnsAsync((UserTable)null);

            mapper.Setup(x => x.Map<UserTable>(dto))
                .Returns(userEntity);

            repo.Setup(x => x.AddAsync(userEntity))
                .ReturnsAsync(response);

            mapper.Setup(x => x.Map<OwnDetail>(response))
                .Returns(new OwnDetail());

            var result = await service.CreateUserAsync(dto);

            result.StatusCode.Should().Be(200);

            publish.Verify(x => x.Publish(It.IsAny<AdminRegistrationRequested>(), default), Times.Once);
        }
    }
}