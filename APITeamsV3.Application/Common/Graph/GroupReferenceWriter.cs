using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace APITeamsV3.Application.Common.Graph
{
    public static class GroupReferenceWriter
    {
        private static readonly Dictionary<string, ParsableFactory<IParsable>> ErrorMapping =
            new Dictionary<string, ParsableFactory<IParsable>>(StringComparer.OrdinalIgnoreCase)
            {
                ["4XX"] = SafeODataErrorFactory,
                ["5XX"] = SafeODataErrorFactory
            };

        public static Task AddOwnerAsync(GraphServiceClient client, string groupId, ReferenceCreate reference, CancellationToken cancellationToken = default)
        {
            var requestInfo = client.Groups[groupId].Owners.Ref.ToPostRequestInformation(reference);
            SetReferenceJsonContent(requestInfo, reference);
            return client.RequestAdapter.SendNoContentAsync(requestInfo, errorMapping: ErrorMapping, cancellationToken: cancellationToken);
        }

        public static Task AddMemberAsync(GraphServiceClient client, string groupId, ReferenceCreate reference, CancellationToken cancellationToken = default)
        {
            var requestInfo = client.Groups[groupId].Members.Ref.ToPostRequestInformation(reference);
            SetReferenceJsonContent(requestInfo, reference);
            return client.RequestAdapter.SendNoContentAsync(requestInfo, errorMapping: ErrorMapping, cancellationToken: cancellationToken);
        }

        private static void SetReferenceJsonContent(Microsoft.Kiota.Abstractions.RequestInformation requestInfo, ReferenceCreate reference)
        {
            if (string.IsNullOrWhiteSpace(reference?.OdataId))
            {
                throw new ArgumentException("Reference OdataId is required.", nameof(reference));
            }

            var normalizedOdataId = NormalizeGroupReference(reference.OdataId);

            var payload = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["@odata.id"] = normalizedOdataId
            });

            requestInfo.SetStreamContent(new MemoryStream(Encoding.UTF8.GetBytes(payload)), "application/json");
        }

        private static string NormalizeGroupReference(string odataId)
        {
            if (odataId.Contains("/directoryObjects/", StringComparison.OrdinalIgnoreCase))
            {
                return odataId;
            }

            return odataId
                .Replace("/v1.0/users/", "/v1.0/directoryObjects/", StringComparison.OrdinalIgnoreCase)
                .Replace("/beta/users/", "/beta/directoryObjects/", StringComparison.OrdinalIgnoreCase);
        }

        private static IParsable SafeODataErrorFactory(IParseNode parseNode)
        {
            try
            {
                return ODataError.CreateFromDiscriminatorValue(parseNode);
            }
            catch
            {
                return new ODataError
                {
                    Error = new MainError
                    {
                        Code = "UnknownGraphError",
                        Message = "Graph returned an error response that could not be parsed."
                    }
                };
            }
        }
    }
}
