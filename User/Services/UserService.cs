using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Name;
using USER.Data.Dto;
using USER.Model;
using USER.Repository;
using USER.Data.Dto.Response;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Mvc;
using USER.CloudinaryService;
using MassTransit.Testing;
using RabbitMQ.Client;
using MassTransit.RabbitMqTransport.Configuration;
using USER.Messaging.Consumer;

namespace USER.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly ILogger<UserService> _logger;
        private readonly ItokenGeneration _token;
        private readonly PasswordHasher<object> _hash;
        private readonly IMapper _mapper;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ClodinaryService cloudinary;
        private readonly ISendEndpointProvider sendEndpoint;

        public UserService(
            IUserRepository repository,
            ILogger<UserService> logger,
            PasswordHasher<object> hash,
            ItokenGeneration token,
            IMapper mapper,
            IPublishEndpoint publishEndpoint, ClodinaryService cloudinary,ISendEndpointProvider sendEndpoint)
        {
            _repository = repository;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
            _hash = hash;
            _token = token;
            _mapper = mapper;
            this.cloudinary = cloudinary;
            this.sendEndpoint=sendEndpoint;
        }

        public async Task<ServiceResult<UserDetail>> CreateUserAsync(UserCreateDto user)
        {

            var existingUser = await _repository.GetByEmailAsync(user.Email);

            if (existingUser != null)
                return ServiceResult<UserDetail>.Fail("User already exists with this email");
            (string? url, string? publicId) = (null, null);
            if (user.file != null)
            {
                var result = await cloudinary.singleUpload(user.file);
                if (result.url != null)
                    url = result.url;
                if (result.publicId != null)
                    publicId = result.publicId;
            }

            var userData = _mapper.Map<UserTable>(user);
            if (publicId != null && publicId != null)
            {
                userData.ProfilePicture = url;
                userData.publicPictureId = publicId;
            }


            var response = await _repository.AddAsync(userData);
            if (response == null)
                return ServiceResult<UserDetail>.Fail("User Not create successfully");

            if (response.Role == "ADMIN")
            {
                await _publishEndpoint.Publish(new AdminRegistrationRequested(
                    RequestUserId: userData.Id,
                    Name: userData.Name,
                    Email: userData.Email));
            }
            var data = _mapper.Map<UserDetail>(response);
            return ServiceResult<UserDetail>.Ok(data, "User created successfully");

        }

        public async Task<ServiceResult<UserDetail>> ChangeProfileAsync(int userId, changeProfileDto docs)
        {
            var existOrNot = await _repository.GetByIdAsync(userId);
            if (existOrNot == null)
                return ServiceResult<UserDetail>.Fail("User Not Exist");


            if (docs.file != null)
            {
                var result = await ProfileImageUpdate((IFormFile)docs.file, existOrNot.publicPictureId);
                if (result.publicId != null)
                    docs.publicId = result.publicId;
                if (result.url != null)
                    docs.ProfilePicture = result.url;
            }


            var currentUser = await _repository.changeFields(docs, userId);
            if (currentUser == null)
                return ServiceResult<UserDetail>.NotFound("User Is Not Found Here");




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
        public async Task<(string? url, string? publicId)> ProfileImageUpdate(IFormFile? file, string? publicId = null)
        {
            if (file==null)
            {
                return (null,null);
            }
            if (publicId != null)
            {
                var endpoint=await sendEndpoint.GetSendEndpoint(new Uri("queue:user-messaging-consumer-image-delete-consumer"));
                await endpoint.Send(new productDeleteImage(publicId= new String(publicId)));
            }
            var detail = await cloudinary.singleUpload(file);

            return (detail.url, detail.publicId);
        }
    }
}