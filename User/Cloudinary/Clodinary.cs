
using System.Security.Cryptography;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;


namespace USER.CloudinaryService
{
    public class ClodinaryService
    {
        public Cloudinary clodinary;
        public ClodinaryService(IConfiguration config)
        {
            var account=new Account
            {
                ApiKey=config["CLOUDINARY_API_KEY"],
                ApiSecret=config["CLOUDINARY_API_SECRET"],
                Cloud=config["CLOUDINARY_CLOUD_NAME"]
            };
            clodinary=new Cloudinary(account);
        }
       public async Task<(string? url,string? publicId)> singleUpload(IFormFile file)
        {
            if (file.Length <=0)
            {
                return (null,null);
            }
            using var stream=file.OpenReadStream();
            var data=new ImageUploadParams
            {
                File=new FileDescription(file.FileName,stream),
                Folder="Upload/profileUrl"
            };
            try
            {
                
            var result= await clodinary.UploadAsync(data);
           return (result.SecureUrl.ToString(),result.PublicId);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                
            }
            return (null, null);
        } 
        public async Task<List<(string? url,string? publicId)>> multipleUploads(List<IFormFile> files)
        {
            var images=new List<(string? url,string? publicId)>();
            if (files == null || files.Count==0)
            {
                return images;
            }
            foreach (var item in files)
            {
                var result= await singleUpload(item);
                images.Add((result.url,result.publicId));
            }
            return images;
        }


        public async Task deleteFile(string publicId)
        {
            try
            {
                var deleteParams= new DeletionParams(publicId);
                await clodinary.DestroyAsync(deleteParams);
            }
            catch (System.Exception ex)
            {
                
                    Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
      
    }
}