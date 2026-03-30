using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using USER.Data.Dto;
using USER.Model;
using USER.Repository;
using Xunit.Sdk;

namespace UserService_Tests.RepositoryTests
{
    public class UserRepositoryTest
    {
        private IUserRepository repo;
        private MACUTIONDB database;
        public int id { get; set; }
        public UserRepository getRepo()
        {
            var options = new DbContextOptionsBuilder<MACUTIONDB>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var database = new MACUTIONDB(options);
            return new UserRepository(database);

        }
        public UserTable getData(int id = 1, string addres = "gotcha", string email = "nothing@gmail.com")
        => new UserTable
        {
            Id = 1,
            Address = "gotcha",
            Email = "nothing@gmail.com",
            HashPassword = "1234455",
            Name = "harshid",
            Phone = "1234456788",
            Role = "USER",
            ProfilePicture = "",
            publicPictureId = ""
        };
        public async Task<UserTable> addData(int id = 1, string addres = "gotcha", string email = "nothing@gmail.com") => await repo.AddAsync(getData(id, addres, email));
        [Fact]
        public async Task AddAsync_ShoulReturn_UserTable()
        {
            // Given
            repo = getRepo();
            var data = getData();
            // When
            var result = await repo.AddAsync(data);
            // Then

            result.Should().NotBeNull();
            result.Name.Should().Be(data.Name);
            result.Id.Should().NotBe(0);

        }
        [Fact]
        public async Task GetByEmailAsync_ShouldReturn_UserTable_whenRelatedMail()
        {
            repo = getRepo();
            var data = await addData();
            var result = await repo.GetByEmailAsync(data.Email);
            result.Should().NotBeNull();
            result.Email.Should().Be(data.Email);

        }
        [Fact]
        public async Task GetByEmailAsync_ShouldReturn_null_whenRelateMailData_not_exist()
        {
            repo = getRepo();
            var result = await repo.GetByEmailAsync("get@gmail.com");
            result.Should().BeNull();
        }
        [Fact]
        public async Task GetByIdAsync_ShouldReturn_UserTable_whenRelatedIdContains()
        {
            // Given
            repo = getRepo();
            var data = await addData();

            // When
            var result = await repo.GetByIdAsync(data.Id);
            // Then
            result.Should().NotBeNull();
            result.Id.Should().Be(data.Id);
            result.Name.Should().Be(data.Name);
        }
        [Fact]
        public async Task GetByIdAsync_ShouldReturn_Null_whenRelatedIdContains()
        {
            repo = getRepo();
            var result = await repo.GetByIdAsync(2);
            result.Should().BeNull();

        }
        [Fact]
        public async Task UpdateAsync_ShouldReturn_Update_UserTable_when_Related_Id_Exist()
        {
            // Given
            repo = getRepo();
            var updateName = "newData";
            var data = await addData();
            data!.Name = updateName;
            // When
            var updateData = await repo.UpdateAsync(data);
            // Then
            updateData.Should().NotBeNull();
            updateData.Name.Should().Be(updateName);
        }
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(8)]
        public async Task ChangeFields_ShouldUpdateCorrectFieldOrReturnNull(int sit)
        {
            // Arrange
             repo = getRepo();
            
            UserTable currentData = null!;
            int id = 5;

            if (sit != 8)
            {
                
                currentData = await addData();
                id = currentData.Id;
            }

            var dataChange = new
            {
                Address = "newAddress",
                Password = "123456764",
                Email = "new@gmail.com",
                Name = "newname",
                Phone = "9856457823",
                ProfilePicture = "data.com",
                PublicId = "dude",
            };

            var data = new changeProfileDto
            {
                Address = sit == 1 ? dataChange.Address : null,
                Password = sit == 2 ? dataChange.Password : null,
                Email = sit == 3 ? dataChange.Email : null,
                Name = sit == 4 ? dataChange.Name : null,
                Phone = sit == 5 ? dataChange.Phone : null,
                ProfilePicture = sit == 6 ? dataChange.ProfilePicture : null,
                publicId = sit == 6 ? dataChange.PublicId : null,
            };
            // Act
            var result = await repo.changeFields(data, id);

            // Assert
            if (sit == 8)
            {
                result.Should().BeNull("sit==8 means no record should be returned");
                return;
            }

            result.Should().NotBeNull("for sit!=8 we expect the entity to exist");

            // Verify only the updated field
            switch (sit)
            {
                case 1:
                    result.Address.Should().Be(data.Address);
                    break;
                case 2:
                    result.HashPassword.Should().NotBeNullOrEmpty(data.Password);
                    break;
                case 3:
                    result.Email.Should().Be(data.Email);
                    break;
                case 4:
                    result.Name.Should().Be(data.Name);
                    break;
                case 5:
                    result.Phone.Should().Be(data.Phone);
                    break;
                case 6:
                    result.ProfilePicture.Should().Be(data.ProfilePicture);
                    result.publicPictureId.Should().Be(data.publicId);
                    break;
                default:
                    break;
            }

        }

        [Fact]
        public async Task RemoveAsync_ShouldReturn_UserTable_WhenThat_Exist()
        {
            repo = getRepo();
            var data = await addData();

            data.Should().NotBeNull();
            var user = await repo.RemoveAsync(data);
            user.Should().NotBeNull();
            user.Id.Should().Be(data.Id);
        }
        [Fact]  
        public async Task RemoveAsync_ThrowException_WhenThat_ID_NOT_Exist()
        {
            repo = getRepo();
            var data = getData();
            // var data=await repo.GetByIdAsync(1);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await repo.RemoveAsync(data));
        }
    }
}