using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MilestoneTG.Splunk
{
    /// <summary>
    /// Compresses the <see cref="HttpRequestMessage"/> using the GZip compression method.
    /// </summary>
    /// <remarks>
    /// This handler only compresses the request. You should enable AutoDecompression to 
    /// decompress the response.
    /// </remarks>
    /// <example>
    /// <code>
    /// HttpClientHandler httpClientHandler = new HttpClientHandler
    /// {
    ///     //Decompress the response
    ///     AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    /// };
    /// //Compress requests
    /// CompressionDelegatingHandler commpressionHandler = new CompressionDelegatingHandler(httpClientHandler);
    /// 
    /// httpClient = new HttpClient(commpressionHandler);
    /// </code>
    /// 
    /// If you use <see cref="CompressionDelegatingHandler"/> in conjunction with
    /// <see cref="Microsoft.Rest.RetryDelegatingHandler"/>, the retry handler should come 
    /// after the compression handler to avoid the overhead of recompressing the message each time.
    /// <code>
    /// HttpClientHandler httpClientHandler = new HttpClientHandler
    /// {
    ///     //Decompress the response
    ///     AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    /// };
    /// //Transient Fault Handling
    /// RetryDelegatingHandler retryHandler = new RetryDelegatingHandler
    /// {
    ///     InnerHandler = httpClientHandler,
    /// };
    /// //Compress requests
    /// CompressionDelegatingHandler commpressionHandler = new CompressionDelegatingHandler(retryHandler);
    /// 
    /// httpClient = new HttpClient(commpressionHandler);
    /// </code>
    /// </example>
    public class CompressionDelegatingHandler : DelegatingHandler
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CompressionDelegatingHandler"/>.
        /// </summary>
        /// <param name="innerHandler">The next <see cref="DelegatingHandler"/> in the pipeline
        /// or <see cref="HttpClientHandler"/> if it's the last delegating handler in the pipeline.</param>
        public CompressionDelegatingHandler(HttpMessageHandler innerHandler):base(innerHandler)
        {
           
        }

        /// <summary>
        /// Compresses the request body and sends it to the next handler in the pipeline.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] rawBytes = await request.Content.ReadAsByteArrayAsync();
                using (GZipStream gZip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    await gZip.WriteAsync(rawBytes, 0, rawBytes.Length);
                }

                ms.Position = 0;

                StreamContent content = new StreamContent(ms);
                content.Headers.ContentEncoding.Add("gzip");

                request.Content = content;

                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}
