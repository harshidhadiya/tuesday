using AutoMapper;
using Helper;
using MassTransit;
using Messaging.Contracts;
using USER.CloudinaryService;
using USER.Data.Dto;
using USER.Data.Dto.Response;
using USER.Model;
using USER.Repository;
namespace USER.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly ILogger<UserService> _logger;
        private readonly IMapper _mapper;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IClodinaryService cloudinary;
        private readonly Ihelper helper;
        public UserService(
            IUserRepository repository,
            ILogger<UserService> logger,
            IMapper mapper,
            IPublishEndpoint publishEndpoint, IClodinaryService cloudinary,Ihelper helper)
        {
            _repository = repository;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
            
            _mapper = mapper;
            this.cloudinary = cloudinary;
            this.helper=helper;
        }

        public async Task<ServiceResult<OwnDetail>> CreateUserAsync(UserCreateDto user)
        {

            var existingUser = await _repository.GetByEmailAsync(user.Email);

            if (existingUser != null)
                return ServiceResult<OwnDetail>.Fail("User already exists with this email");
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
            if (url != null && publicId != null)
            {
                userData.ProfilePicture = url;
                userData.publicPictureId = publicId;
            }


            var response = await _repository.AddAsync(userData);
            if (response == null){
                if(publicId!=null)
                 await cloudinary.deleteFile(publicId);
                return ServiceResult<OwnDetail>.Fail("User Not create successfully");

       
}

            if (response.Role == "ADMIN")
            {
                await _publishEndpoint.Publish(new AdminRegistrationRequested(
                    RequestUserId: userData.Id,
                    Name: userData.Name,
                    Email: userData.Email));
            }
            var data = _mapper.Map<OwnDetail>(response);
            return ServiceResult<OwnDetail>.Ok(data, "User created successfully");

        }

        public async Task<ServiceResult<OwnDetail>> ChangeProfileAsync(int userId, changeProfileDto docs)
        {
            var existOrNot = await _repository.GetByIdAsync(userId);
            // here i changed fail to notfound
            if (existOrNot == null)
                return ServiceResult<OwnDetail>.NotFound("User Not Exist");


            if (docs.file != null)
            {
                var result = await helper.ProfileImageUpdate((IFormFile)docs.file, existOrNot.publicPictureId);
                if (result.publicId != null)
                    docs.publicId = result.publicId;
                if (result.url != null)
                    docs.ProfilePicture = result.url;
            }


            var currentUser = await _repository.changeFields(docs, userId);
            // I CHANGE HERE FAIL TO SERVER ERROR OKAY DONE 
            if (currentUser == null){
                if(docs.publicId!=null)
                await cloudinary.deleteFile(docs.publicId);
                return ServiceResult<OwnDetail>.ServerError("User Is Not Found Here");
}


            var response = _mapper.Map<OwnDetail>(currentUser);
            return ServiceResult<OwnDetail>.Ok(response, "Profile updated successfully");

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

