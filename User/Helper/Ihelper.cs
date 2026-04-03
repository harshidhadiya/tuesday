using MassTransit;
using Messaging.Contracts;
using USER.CloudinaryService;

namespace Helper
{

    public interface Ihelper
    {
        Task<(string? url, string? publicId)> ProfileImageUpdate(IFormFile? file, string? publicId = null);
        
    }

    public class Helpers :Ihelper
    {
        private  readonly IClodinaryService cloudinary;
        private readonly ISendEndpointProvider sendEndpoint;
          public Helpers(IClodinaryService clodinaryService,ISendEndpointProvider sendEndpoint)
        {
            this.cloudinary=clodinaryService;
            this.sendEndpoint=sendEndpoint;
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