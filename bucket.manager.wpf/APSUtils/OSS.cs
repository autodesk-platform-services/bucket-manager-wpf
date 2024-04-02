using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Oss;
using Autodesk.Oss.Model;

namespace bucket.manager.wpf.APSUtils
{
    internal class OSS
    {
        /// <summary>
        /// Get bucket objects
        /// </summary>
        /// <param name="key">Bucket key</param>
        /// <param name="accessToken">Access token</param>
        /// <param name="limit">Limit to the response size</param>
        /// <param name="beginsWith">String to filter the result set by. </param>
        /// <param name="startAt">Key to use as an offset to continue pagination This is typically the last bucket key found in a preceding GET buckets response</param>
        /// <returns></returns>
        public static async Task<BucketObjects> GetBucketObjectsAsync(string key, string accessToken, int? limit=null, string? beginsWith=null, string? startAt=null)
        {
            var client = new OssClient(SdkManagerHelper.Instance);
            var result = await client.GetObjectsAsync(key, limit, beginsWith, startAt, accessToken);
            return result?? new BucketObjects();
        }

        /// <summary>
        /// Get buckets
        /// </summary>
        /// <param name="region">Desired region</param>
        /// <param name="accessToken">Access token</param>
        /// <param name="limit">Limit to the response size</param>
        /// <param name="startAt">The position to start listing the result set. This parameter is used to request the next set of items, when the response is paginated.</param>
        /// <returns></returns>
        public static async Task<Buckets> GetBucketsAsync(string region, string accessToken, int? limit, string? startAt)
        {
            var client = new OssClient(SdkManagerHelper.Instance);
            var result = await client.GetBucketsAsync(region, limit, startAt, accessToken);
            return result ?? new Buckets();
        }

        /// <summary>
        /// Create a bucket
        /// </summary>
        /// <param name="region">Desired region</param>
        /// <param name="key">Bucket key</param>
        /// <param name="policy">Bucket storage policy</param>
        /// <param name="accessToken">Access Token</param>
        /// <returns></returns>
        public static async Task<Bucket?> CreateBucketAsync(string region, string key, string policy, string accessToken)
        {
            var client = new OssClient(SdkManagerHelper.Instance);
            var payload = new CreateBucketsPayload { PolicyKey = policy, BucketKey = key};

            var result = await client.CreateBucketAsync(region, payload, accessToken);
            return result;
        }

        /// <summary>
        /// Upload file with OSS file uploader
        /// </summary>
        /// <param name="bucketKey">Bucket key</param>
        /// <param name="objectKey">Object key</param>
        /// <param name="sourceToUpload">Local file path</param>
        /// <param name="accessToken">Access token</param>
        /// <param name="progressUpdater">A progress updater for UI. Implement IProgress&lt;int&gt; interface </param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> UploadFileWithProgress(string bucketKey, string objectKey, string sourceToUpload, string accessToken, IProgress<int> progressUpdater)
        {
            // Create a new OSS file transfer client, using the configurations and authentication client
            var client = new OSSFileTransfer(FileTransferConfigurations.Instance, new Authentication());

            // Upload the file
            return await client.Upload(bucketKey, objectKey, sourceToUpload, accessToken, CancellationToken.None, progress:progressUpdater);
        }

        /// <summary>
        /// Delete a bucket item
        /// </summary>
        /// <param name="bucketKey">Bucket key</param>
        /// <param name="objectKey">Object key</param>
        /// <param name="accessToken">Access token</param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> DeleteObjectAsync(string bucketKey, string objectKey, string accessToken)
        {
            var client = new OssClient(SdkManagerHelper.Instance);
            return await client.DeleteObjectAsync(bucketKey, objectKey, accessToken: accessToken);
        }

        /// <summary>
        /// File transfer configurations for OSSFiletransfer, using singleton pattern
        /// </summary>
        internal class FileTransferConfigurations : IFileTransferConfigurations
        {
            // Retry count
            public int GetRetryCount()
            {
                return 5;
            }

            // Chunk count
            public int GetMaxChunkCountAllowed()
            {
                return Int32.MaxValue;
            }

            // Retries when token expired
            public int GetMaxRetryOnTokenExpiry()
            {
                return 3;
            }

            // Retries when URL expired
            public int GetMaxRetryOnUrlExpiry()
            {
                return 3;
            }

            // singleton instance
            private static FileTransferConfigurations? _instance;

            private FileTransferConfigurations()
            {

            }

            public static FileTransferConfigurations Instance
            {
                get
                {
                    return _instance ??= new FileTransferConfigurations();
                }
            }

        }
        
    }
}
