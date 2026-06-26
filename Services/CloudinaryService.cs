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

    public async Task<string?> UploadImageAsync(IFormFile file, string folder = "images")
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
            Folder = folder,
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

        // Theo yêu cầu của User: Lưu tên và đuôi ảnh vào db, kèm theo folder
        return $"{folder}/{finalName}{Path.GetExtension(file.FileName)}";
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
            Folder = "images/complaints",
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

        return $"images/complaints/{finalName}{Path.GetExtension(file.FileName)}";
    }

    public async Task<string?> UploadBannerImageAsync(IFormFile file)
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
            Folder = "images/banners",
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

        return $"images/banners/{finalName}{Path.GetExtension(file.FileName)}";
    }
    public async Task<bool> DeleteBannerImageAsync(string fileNameWithExtension)
    {
        if (string.IsNullOrEmpty(fileNameWithExtension)) return false;
        
        string publicId;
        if (fileNameWithExtension.Contains('/'))
        {
            publicId = fileNameWithExtension.Substring(0, fileNameWithExtension.LastIndexOf('.'));
        }
        else
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            publicId = $"images/banners/{fileNameWithoutExtension}";
        }
        
        var deletionParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deletionParams);
        
        return result.Result == "ok";
    }

    public async Task<bool> DeleteImageAsync(string fileNameWithExtension, string folder = "images")
    {
        if (string.IsNullOrEmpty(fileNameWithExtension)) return false;
        
        string publicId;
        if (fileNameWithExtension.Contains('/'))
        {
            // Nếu đã có sẵn đường dẫn (ví dụ: images/products/abc.jpg)
            publicId = fileNameWithExtension.Substring(0, fileNameWithExtension.LastIndexOf('.'));
        }
        else
        {
            // Dành cho ảnh cũ (chỉ có abc.jpg)
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            publicId = $"{folder}/{fileNameWithoutExtension}";
        }
        
        var deletionParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deletionParams);
        
        return result.Result == "ok";
    }

    public async Task<bool> DeleteComplaintImageAsync(string fileNameWithExtension)
    {
        if (string.IsNullOrEmpty(fileNameWithExtension)) return false;
        
        string publicId;
        if (fileNameWithExtension.Contains('/'))
        {
            publicId = fileNameWithExtension.Substring(0, fileNameWithExtension.LastIndexOf('.'));
        }
        else
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            publicId = $"images/complaints/{fileNameWithoutExtension}";
        }
        
        var deletionParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deletionParams);
        
        return result.Result == "ok";
    }
}
