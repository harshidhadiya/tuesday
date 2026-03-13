using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;       // for IFormFile
using Microsoft.Extensions.Configuration; // for IConfiguration
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace CloudinaryService
{
    public class ClodinaryService
    {

        public Cloudinary clodinary;
        public ClodinaryService(IConfiguration config)
        {
            var account = new Account
            {
                ApiKey = "314395916455212",
                ApiSecret = "pxedM4zfNr_JfKPeWfDU7e0ad2o",
                Cloud = "df4vap5ch"
            };
            clodinary = new Cloudinary(account);
        }
        public async Task<(string? url, string? publicId)> singleUpload(IFormFile file)
        {
            if (file == null || file.Length <= 0)
            {
                return (null, null);
            }

            using var stream = file.OpenReadStream();

            var data = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "Upload/profileUrl"
            };

            try
            {
                var result = await clodinary.UploadAsync(data);
                Console.WriteLine(result?.Error?.Message+"error message");
                
                if (result?.SecureUrl == null)
                    return (null, null);

                return (result.SecureUrl.ToString(), result.PublicId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return (null, null);
        }
        public async Task<List<(string? url, string? publicId)>> multipleUploads(List<IFormFile> files)
        {
            var images = new List<(string? url, string? publicId)>();
            if (files == null || files.Count == 0)
            {
                return images;
            }
            foreach (var item in files)
            {
                var result = await singleUpload(item);
                images.Add((result.url, result.publicId));
            }
            return images;
        }


        public async Task deleteFile(string publicId)
        {
            try
            {
                var deleteParams = new DeletionParams(publicId);
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