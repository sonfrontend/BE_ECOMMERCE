using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System;

namespace BE_ECOMMERCE.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var account = new Account(
            configuration["Cloudinary:CloudName"],
            configuration["Cloudinary:ApiKey"],
            configuration["Cloudinary:ApiSecret"]
        );

        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<string?> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return null;

        string originalName = Path.GetFileNameWithoutExtension(file.FileName);
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        string finalName = $"{originalName}_{timestamp}";

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "images/messages",
            PublicId = finalName,
            UseFilename = true,
            UniqueFilename = false,
            Overwrite = true
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
        {
            throw new Exception(uploadResult.Error.Message);
        }

        // Theo yêu cầu của User: Chỉ lưu Tên_ảnh_cuối.
        // Trên Cloudinary, PublicId sẽ có định dạng: "images/messages/Tên_ảnh_cuối"
        // Ta chỉ lấy Tên_ảnh_cuối trả về
        return finalName;
    }

    public async Task<string?> UploadComplaintImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return null;

        string originalName = Path.GetFileNameWithoutExtension(file.FileName);
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        string finalName = $"{originalName}_{timestamp}";

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "images/complants",
            PublicId = finalName,
            UseFilename = true,
            UniqueFilename = false,
            Overwrite = true
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
        {
            throw new Exception(uploadResult.Error.Message);
        }

        // Return only the filename with its extension, e.g., "my_image_12345.png"
        return Path.GetFileName(new Uri(uploadResult.SecureUrl.ToString()).LocalPath);
    }
}
