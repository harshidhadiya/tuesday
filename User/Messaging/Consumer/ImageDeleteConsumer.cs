using Messaging.Contracts;
using MassTransit;
using USER.Repository;
using USER.CloudinaryService;

namespace USER.Messaging.Consumer
{
    public class ImageDeleteConsumer : IConsumer<productDeleteImage>
    {
        private readonly IServiceScopeFactory serviceScope;
        public ImageDeleteConsumer(IServiceScopeFactory serviceScope)
        {
            this. serviceScope=serviceScope;
        }
        public async Task Consume(ConsumeContext<productDeleteImage> context)
        {
            using var scope=serviceScope.CreateScope();
            var repository=scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var cloudinary=scope.ServiceProvider.GetRequiredService<ClodinaryService>();
            var data=context.Message.publicId;
            if(string.IsNullOrEmpty(data))
            return;

            await cloudinary.deleteFile(data);
        }
    }
}