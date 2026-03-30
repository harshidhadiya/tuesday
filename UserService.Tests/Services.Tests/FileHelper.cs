
using System.Text;
using Microsoft.AspNetCore.Http;

public static class FileHelper
{
    public static IFormFile CreateFakeFile(
        string fileName = "test.txt",
        string content = "Hello World",
        string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}