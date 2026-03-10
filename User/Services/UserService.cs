using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Name;
using ADMIN.Data.Dto;
using USER.Data.Dto;
using USER.Model;
using USER.Repository;

namespace USER.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly ILogger<UserService> _logger;
        private readonly ItokenGeneration _token;
        private readonly PasswordHasher<object> _hash;
        private readonly IMapper _mapper;

        public UserService(
            IUserRepository repository,
            ILogger<UserService> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            IMapper mapper)
        {
            _repository = repository;
            _logger = logger;
            _hash = hash;
            _token = token;
            _mapper = mapper;
        }

        public async Task<ActionResult> CreateUserAsync(UserCreateDto user)
        {
            try
            {
                var existingUser = await _repository.GetByEmailAsync(user.Email);
                if (existingUser != null)
                {
                    return new BadRequestObjectResult("User already exists with this email");
                }

                var userData = _mapper.Map<UserTable>(user);
                await _repository.AddAsync(userData);

                var generatedToken = _token.getToken(
                    userData.Name,
                    userData.Role.ToUpperInvariant(),
                    userData.Id.ToString()
                );

                return new CreatedResult("/api/user/getprofile", new { name = userData.Name, role = userData.Role, token = generatedToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating user");
                return new ObjectResult(new { message = "An error occurred while creating the user.", detail = ex.Message, stackTrace = ex.StackTrace })
                {
                    StatusCode = 500
                };
            }
        }

        public async Task<ActionResult> ChangePasswordAsync(int userId, changePasswordDto pass_obj)
        {
            try
            {
                var currentUser = await _repository.GetByIdAsync(userId);
                if (currentUser == null)
                    return new BadRequestObjectResult("Current Id relate User Not Exist");
                
                var hashedPassword = _hash.HashPassword(new object(), pass_obj.Password);
                currentUser.HashPassword = hashedPassword;
                
                await _repository.UpdateAsync(currentUser);
                return new NoContentResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing password");
                return new ObjectResult(new { message = "An error occurred while changing the password.", detail = ex.Message, stackTrace = ex.StackTrace })
                {
                    StatusCode = 500
                };
            }
        }

        public async Task<ActionResult> ChangeProfileAsync(int userId, changeProfileDto docs)
        {
            var currentUser = await _repository.GetByIdAsync(userId);

            if (currentUser == null)
            {
                return new BadRequestObjectResult("Current Id relate User Not Exist");
            }

            if (!string.IsNullOrWhiteSpace(docs.Name))
                currentUser.Name = docs.Name;

            if (!string.IsNullOrWhiteSpace(docs.Email))
                currentUser.Email = docs.Email;

            if (docs.Phone != null && !string.IsNullOrWhiteSpace(docs.Phone))
                currentUser.Phone = docs.Phone;

            if (!string.IsNullOrWhiteSpace(docs.Address))
                currentUser.Address = docs.Address;

            if (!string.IsNullOrWhiteSpace(docs.ProfilePicture))
                currentUser.ProfilePicture = docs.ProfilePicture;

            await _repository.UpdateAsync(currentUser);

            string tokens = _token.getToken(
                currentUser.Name,
                currentUser.Role.ToUpperInvariant(),
                currentUser.Id.ToString()
            );

            return new OkObjectResult(new UserLoginResponseDto
            {
                Address = currentUser.Address,
                Email = currentUser.Email,
                Name = currentUser.Name,
                Phone = currentUser.Phone,
                ProfilePicture = currentUser.ProfilePicture,
                Role = currentUser.Role,
                Id = currentUser.Id,
                Token = tokens
            });
        }

        public async Task<ActionResult> GetProfileAsync(int userId)
        {
            var currentUser = await _repository.GetByIdAsync(userId);
            if (currentUser == null)
            {
                return new BadRequestObjectResult("Current Id relate User Not Exist");
            }
            return new OkObjectResult(new
            {
                Address = currentUser.Address,
                Email = currentUser.Email,
                Name = currentUser.Name,
                Phone = currentUser.Phone,
                ProfilePicture = currentUser.ProfilePicture,
                Role = currentUser.Role,
                Id = currentUser.Id
            });
        }

        public async Task<ActionResult> GetUserByIdAsync(int id)
        {
            if (id <= 0)
            {
                return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse("Invalid user id.", 400));
            }

            var user = await _repository.GetByIdAsync(id);
            if (user == null)
            {
                return new NotFoundObjectResult(ApiResponse<object>.ErrorResponse("User not found", 404));
            }

            return new OkObjectResult(ApiResponse<object>.SuccessResponse(new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role,
                phone = user.Phone,
                address = user.Address,
                profilePicture = user.ProfilePicture
            }, "User retrieved successfully"));
        }

        public async Task<ActionResult> GetUserDashboardAsync(int userId)
        {
            var currentUser = await _repository.GetByIdAsync(userId);
            if (currentUser == null)
                return new BadRequestObjectResult("User not found.");

            return new OkObjectResult(ApiResponse<object>.SuccessResponse(new
            {
                profile = new
                {
                    currentUser.Id,
                    currentUser.Name,
                    currentUser.Email,
                    currentUser.Role,
                    currentUser.Phone,
                    currentUser.Address,
                    currentUser.ProfilePicture
                },
                message = "User dashboard data for showcase"
            }, "User dashboard"));
        }
    }
}
