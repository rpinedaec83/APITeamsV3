using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace APITeamsV3.API.Filters
{
    public class TenantHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                operation.Parameters = new List<OpenApiParameter>();

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Company-Key",
                In = ParameterLocation.Header,
                Description = "Company Key for multi-tenant requests (Dev/Test)",
                Required = false, // Optional because middleware might handle default or error if missing
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Default = new Microsoft.OpenApi.Any.OpenApiString("idat") // IDAT default
                }
            });
        }
    }
}
