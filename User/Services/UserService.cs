using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Name;
using USER.Data.Dto;
using USER.Model;
using USER.Repository;
using USER.Data.Dto.Response;
using USER.Messaging;

namespace USER.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly ILogger<UserService> _logger;
        private readonly ItokenGeneration _token;
        private readonly PasswordHasher<object> _hash;
        private readonly IMapper _mapper;
        private readonly IRabbitMqPublisher _publisher;

        public UserService(
            IUserRepository repository,
            ILogger<UserService> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            IMapper mapper, IRabbitMqPublisher _publisher)
        {
            _repository = repository;
            this._publisher = _publisher;
            _logger = logger;
            _hash = hash;
            _token = token;
            _mapper = mapper;
        }

        public async Task<ServiceResult<UserDetail>> CreateUserAsync(UserCreateDto user)
        {
            var existingUser = await _repository.GetByEmailAsync(user.Email);

            if (existingUser != null)
                return ServiceResult<UserDetail>.Fail("User already exists with this email");

            var userData = _mapper.Map<UserTable>(user);

            var response = await _repository.AddAsync(userData);
            if (response == null)
                return ServiceResult<UserDetail>.Fail("User Not create successfully");

            if (response.Role == "ADMIN")
            {
                var requestBody = new { RequestUserId = userData.Id, Name = userData.Name, Email = userData.Email };

               await _publisher.Publish<object>("request.created", requestBody);
            }
            var data = _mapper.Map<UserDetail>(response);
            return ServiceResult<UserDetail>.Ok(data, "User created successfully");

        }

        public async Task<ServiceResult<UserDetail>> ChangeProfileAsync(int userId, changeProfileDto docs)
        {

            var currentUser = await _repository.changeFields(docs, userId);
            if (currentUser == null)
            {
                return ServiceResult<UserDetail>.NotFound("User Is Not Found Here");
            }



            var response = _mapper.Map<UserDetail>(currentUser);
            return ServiceResult<UserDetail>.Ok(response, "Profile updated successfully");

        }

        public async Task<ServiceResult<UserDetail>> GetProfileAsync(int userId)
        {

            var currentUser = await _repository.GetByIdAsync(userId);

            if (currentUser == null)
                return ServiceResult<UserDetail>.NotFound("User not found");
            var response = _mapper.Map<UserDetail>(currentUser);
            return ServiceResult<UserDetail>.Ok(response, "User profile retrieved");

        }
    }
}