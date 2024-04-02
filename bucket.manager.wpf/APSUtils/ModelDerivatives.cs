using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.ModelDerivative;
using Autodesk.ModelDerivative.Model;
using Autodesk.ModelDerivative.Http;

namespace bucket.manager.wpf.APSUtils
{
    internal static class ModelDerivatives
    {
        /// <summary>
        /// Parse the region string to Region enum
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        private static Region GetRegionEnum(string region)
        {
            return region switch
            {
                "EMEA" => Region.EMEA,
                _ => Region.US
            };
        }
        /// <summary>
        /// Translate the URN to SVF or SVF2
        /// </summary>
        /// <param name="urn">Resource urn</param>
        /// <param name="accessToken">Access token</param>
        /// <param name="region">Desired region</param>
        /// <param name="workType">Desired format</param>
        /// <returns></returns>
        public static async Task<Job> TranslateAsync(string urn, string accessToken, string region, string workType)
        {
            var client = new ModelDerivativeClient(SdkManagerHelper.Instance);
            var regionEnum = GetRegionEnum(region);


            // Set the output format based on the work type
            JobPayloadFormat outputFormat = workType switch
            {
                "svf" => new JobSvfOutputFormat { Views = [View._2d, View._3d] },
                _ => new JobSvf2OutputFormat { Views = [View._2d, View._3d] }
            };

            // Prepare the job payload
            var jobPayload = new JobPayload
            {
                Input = new JobPayloadInput { Urn = urn },
                Output = new JobPayloadOutput
                {
                    Formats = [outputFormat],
                    Destination = new JobPayloadOutputDestination { Region = regionEnum }
                }
            };

            // Start the job
            return await client.StartJobAsync(false, XAdsDerivativeFormat.Latest, jobPayload, accessToken);
        }

        /// <summary>
        /// Get the manifest for a given URN
        /// </summary>
        /// <param name="urn">Resource urn</param>
        /// <param name="accessToken">Access token</param>
        /// <param name="region">Desired region</param>
        /// <returns></returns>
        public static async Task<Manifest?> GetManifestAsync(string urn, string accessToken, string region)
        {
            var client = new ModelDerivativeClient(SdkManagerHelper.Instance);
            var regionEnum = GetRegionEnum(region);

            return await client.GetManifestAsync(urn, regionEnum, null, accessToken, true);
        }
        /// <summary>
        /// Prepare a list of URL and cookies for a given URN
        /// </summary>
        /// <param name="urn">URN of the resource on Autodesk Platform Services</param>
        /// <param name="accessToken">Valid access token for downloading the resources</param>
        /// /// <param name="region">Desired region</param>
        /// <returns>List of resources for the given URN</returns>
        public static async Task<List<Resource>> PrepareUrlForDownload(string urn, string accessToken, string region)
        {

            // Get manifest for the URN
            var manifests = await GetManifestAsync(urn, accessToken, region);

            if (manifests?.Derivatives is null)
            {
                return [];
            }

            var resources = new List<Resource>();

            // Replace urn from manifest, it is decoded from base64
            urn = manifests.Urn;

            // Parse the manifest to get the resources
            foreach (var derivative in manifests.Derivatives)
            {
                // Resources we want to download are in the children
                if (derivative.Children is null)
                {
                    continue;
                }
                foreach (var derivativeChild in derivative.Children)
                {
                    // Parse the manifest to get the resources
                    var manifestItems = await ParseManifest(derivativeChild);
                    manifestItems.ForEach((item) =>
                     {
                         resources.Add(new Resource
                         {
                             FileName = item.Path.Filename,
                             RemotePath = item.Path.URL,
                             LocalPath = Path.Combine(item.Path.LocalPath, item.Path.Filename),
                             ContentType = item.ContentType,
                             CookieList = item.CookieList
                         });
                     });

                }
            }

            return resources;

            //  Parse a manifest child to get the resources
            async Task<List<ManifestItem>> ParseManifest(ManifestChildren child)
            {
                var result = new List<ManifestItem>();
                // No URN, no files for downloading
                if (!string.IsNullOrEmpty(child.Urn))
                {
                    var item = new ManifestItem()
                    {
                        URN = child.Urn,
                        Path = DecomposeURN(child.Urn)
                    };

                    // Get the download URL for the resource
                    var download = await FetchDerivativeDownloadUrl(child.Urn, urn, region, accessToken);
                    var url = download.DownloadInfo!.Url!;
                    item.Path.URL = url;
                    item.Path.Filename = Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..]);
                    item.ContentType = download.DownloadInfo!.ContentType!;
                    item.CookieList = download.CookieList!;
                    result.Add(item);
                }

                // There could be children in a child, parse them too
                if (child.Children is not null)
                {
                    foreach (var c in child.Children)
                    {
                        result.AddRange(await ParseManifest(c));
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Download the resource from the given URL
        /// </summary>
        /// <param name="derivativeUrn">The urn for getting the manifest</param>
        /// <param name="urn">The urn of a children in the manifest</param>
        /// <param name="region">Desired region</param>
        /// <param name="accessToken">Access token for APS services</param>
        /// <returns></returns>
        private static async Task<DerivativeDownloadWithCookie> FetchDerivativeDownloadUrl(string derivativeUrn, string urn, string region, string accessToken)
        {
            // Get the download URL for the resource. The latest API uses signed cookies for downloading the resources.
            // We use APIs from Autodesk.ModelDerivative.Http to get the DerivativeDownload with HttpResponse
            var client = new DerivativesApi(SdkManagerHelper.Instance);
            var regionEnum = GetRegionEnum(region);
            var derivativeDownloadResponse =  await client.GetDerivativeUrlAsync(derivativeUrn, urn, regionEnum, accessToken: accessToken, throwOnError: true);
            return new DerivativeDownloadWithCookie
            {
                DownloadInfo = derivativeDownloadResponse.Content,
                CookieList = derivativeDownloadResponse.HttpResponse.Headers.GetValues("Set-Cookie") ?? []
            };
        }

        /// <summary>
        /// Resource to download
        /// </summary>
        public struct Resource
        {
            /// <summary>
            /// Filename name (no path)
            /// </summary>
            public string FileName { get; set; }
            /// <summary>
            /// Remove path to download (must add developer.api.autodesk.com prefix)
            /// </summary>
            public string RemotePath { get; set; }
            /// <summary>
            /// Path to save file locally
            /// </summary>
            public string LocalPath { get; set; }
            /// <summary>
            /// Content type of the file
            /// </summary>
            public string ContentType { get; set; }
            /// <summary>
            /// List of cookies to be used for downloading the file
            /// </summary>
            public IEnumerable<string> CookieList { get; set; }
        }

        /// <summary>
        /// A manifest item from the manifest children
        /// </summary>
        private record ManifestItem
        {
            /// <summary>
            /// The URN of the resource
            /// </summary>
            public string URN { get; set; }
            /// <summary>
            /// Path for a manifest item
            /// </summary>
            public PathInfo Path { get; set; }
            /// <summary>
            /// Content type of the file
            /// </summary>
            public string ContentType { get; set; }
            /// <summary>
            /// List of cookies to be used for downloading the file
            /// </summary>
            public IEnumerable<string> CookieList { get; set; }

        }

        /// <summary>
        /// Derivative download result with cookies
        /// </summary>
        private record DerivativeDownloadWithCookie
        {
            /// <summary>
            /// Download information from FetchDerivativeDownloadUrl
            /// </summary>
            public DerivativeDownload? DownloadInfo { get; init; }
            /// <summary>
            /// Cookies from the header
            /// </summary>
            public IEnumerable<string>? CookieList { get; init; }
        }

        /// <summary>
        /// Path information for a manifest child
        /// </summary>
        private record PathInfo
        { 
            /// <summary>
            /// Local path to save the file
            /// </summary>
            public string LocalPath { get; init; }
            /// <summary>
            /// URL to download the file
            /// </summary>
            public string URL { get; set; }
            /// <summary>
            /// Filename
            /// </summary>
            public string Filename { get; set; }
        }

        /// <summary>
        /// Decompose the URN to get the local path
        /// </summary>
        /// <param name="encodedUrn">Encode Urn from a manifest child</param>
        /// <returns></returns>
        private static PathInfo DecomposeURN(string encodedUrn)
        {
            string urn = Uri.UnescapeDataString(encodedUrn);
            string basePath = urn.Substring(0, urn.LastIndexOf('/') + 1);
            string localPath = basePath.Substring(basePath.IndexOf('/') + 1);
            localPath = Regex.Replace(localPath, "[/]?output/", string.Empty);

            return new PathInfo()
            {
                LocalPath = localPath,
                URL = string.Empty,
            };
        }
    }
}
