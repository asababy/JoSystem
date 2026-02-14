using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using JoSystem.Data;
using JoSystem.Models.Entities;

namespace JoSystem.Services
{
    public static class DbService
    {
        public static void Init()
        {
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();

                try
                {
                    db.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS Roles (
                            Id INTEGER NOT NULL CONSTRAINT PK_Roles PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            Description TEXT NULL
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS IX_Roles_Name ON Roles (Name);
                        
                        CREATE TABLE IF NOT EXISTS UserRoles (
                            RoleId INTEGER NOT NULL,
                            UserId INTEGER NOT NULL,
                            CONSTRAINT PK_UserRoles PRIMARY KEY (RoleId, UserId),
                            CONSTRAINT FK_UserRoles_Roles_RoleId FOREIGN KEY (RoleId) REFERENCES Roles (Id) ON DELETE CASCADE,
                            CONSTRAINT FK_UserRoles_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
                        );
                        CREATE INDEX IF NOT EXISTS IX_UserRoles_UserId ON UserRoles (UserId);
                    ");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Schema update warning: {ex.Message}");
                }

                if (!db.Users.Any())
                {
                    db.Users.Add(new User
                    {
                        Username = "admin",
                        PasswordHash = HashPassword("123456"),
                        IsAdmin = true
                    });
                    db.SaveChanges();
                }

                var currentConfig = ConfigService.Current;
                if (!db.ServerConfigs.Any())
                {
                    db.ServerConfigs.AddRange(
                        new ServerConfig { Key = "RootDirectory", Value = currentConfig.RootDirectory, Description = "文件根目录" },
                        new ServerConfig { Key = "LogDirectory", Value = currentConfig.LogDirectory, Description = "日志存放目录" },
                        new ServerConfig { Key = "MaxUploadSizeGB", Value = currentConfig.MaxUploadSizeGB.ToString(), Description = "最大上传限制(GB)" },
                        new ServerConfig { Key = "MaxDownloadSizeGB", Value = currentConfig.MaxDownloadSizeGB.ToString(), Description = "最大下载限制(GB)" },
                        new ServerConfig { Key = "LogPageSize", Value = currentConfig.LogPageSize.ToString(), Description = "日志每页条数" },
                        new ServerConfig { Key = "EnableSwagger", Value = currentConfig.EnableSwagger.ToString(), Description = "启用Swagger文档（建议仅开发/内网环境打开）" },
                        new ServerConfig { Key = "SwaggerIpWhitelist", Value = currentConfig.SwaggerIpWhitelist, Description = "Swagger访问IP白名单(支持;分隔, 支持CIDR如192.168.0.0/24)" }
                    );
                    db.SaveChanges();
                }
            }
        }

        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        
        public static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}
