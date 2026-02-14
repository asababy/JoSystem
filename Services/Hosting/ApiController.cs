using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace JoSystem.Services.Hosting
{
    public static class ApiController
    {
        public static IEndpointRouteBuilder MapFileServerApis(this IEndpointRouteBuilder endpoints, string rootPath, string tempZipPath)
        {
            return endpoints;
        }
    }
}
