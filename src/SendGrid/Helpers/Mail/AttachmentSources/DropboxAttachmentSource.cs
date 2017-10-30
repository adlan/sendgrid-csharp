// <copyright file="DropboxAttachmentSource.cs" company="SendGrid">
// Copyright (c) SendGrid. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace SendGrid.Helpers.Mail
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Attach file from Dropbox folder
    /// </summary>
    /// <remarks>
    /// Uses Dropbox API version 2
    /// </remarks>
    public class DropboxAttachmentSource : IAttachmentSource
    {
        private readonly Uri endpoint = new Uri("https://content.dropboxapi.com/2/files/download");
        private readonly string accessToken;
        private readonly string filePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="DropboxAttachmentSource"/> class.
        /// </summary>
        /// <param name="accessToken">Access token</param>
        /// <param name="filePath">File path</param>
        public DropboxAttachmentSource(string accessToken, string filePath)
        {
            this.accessToken = accessToken;
            this.filePath = filePath;
        }

        /// <summary>
        /// Gets Dropbox API endpoint
        /// </summary>
        public Uri Endpoint => this.endpoint;

        /// <summary>
        /// Gets Dropbox API version
        /// </summary>
        public int ApiVersion
        {
            get { return 2; }
        }

        /// <summary>
        /// Create <see cref="AttachmentSource" /> object from source
        /// </summary>
        /// <returns>An <see cref="AttachmentSource" /> object</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when file does not exists in Dropbox at given path
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when request is unsuccessful because of various reasons
        /// </exception>
        public async Task<AttachmentSource> GetAttachmentAsync()
        {
            var apiArgument = new FileDownloadArgument(this.filePath);

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Post;
                    request.RequestUri = this.endpoint;
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.accessToken);
                    request.Headers.Add("Dropbox-API-Arg", JsonConvert.SerializeObject(apiArgument));

                    var response = await client.SendAsync(request).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var rawApiResponse = response.Headers.GetValues("Dropbox-API-Result");
                        var buffer = await response.Content.ReadAsByteArrayAsync();

                        long fileSize = buffer.Length;
                        var fileName = Path.GetFileName(this.filePath);

                        if (rawApiResponse.Any())
                        {
                            var apiResponse = JsonConvert.DeserializeObject<SuccessfulFileDownloadResponse>(rawApiResponse.First());
                            fileName = apiResponse.Name;
                            fileSize = apiResponse.Size;
                        }

                        return new AttachmentSource(fileName, string.Empty, buffer, fileSize);
                    }
                    else
                    {
                        var content = await response.Content.ReadAsStringAsync();

                        switch (response.StatusCode)
                        {
                            case HttpStatusCode.BadRequest:
                                throw new Exception(content);
                            case HttpStatusCode.Unauthorized:
                            case HttpStatusCode.Conflict:
                                var apiResponse = JsonConvert.DeserializeObject<UnsuccessfulFileDownloadResponse>(content);

                                if (response.StatusCode == HttpStatusCode.Conflict)
                                {
                                    throw new FileNotFoundException(apiResponse.ErrorSummary);
                                }
                                else
                                {
                                    throw new Exception(apiResponse.ErrorSummary);
                                }

                            case (HttpStatusCode)429:
                                throw new Exception("Too many request made");
                            default:
                                throw new Exception("Dropbox service error");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Dropbox's file download API argument
        /// </summary>
        private class FileDownloadArgument
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FileDownloadArgument"/> class.
            /// </summary>
            /// <param name="path">File path in Dropbox</param>
            public FileDownloadArgument(string path)
            {
                Path = path;
            }

            /// <summary>
            /// Gets file path in Dropbox
            /// </summary>
            [JsonProperty(PropertyName = "path")]
            public string Path { get; }
        }

        /// <summary>
        /// Represents succesful download response
        /// </summary>
        private class SuccessfulFileDownloadResponse
        {
            /// <summary>
            /// Gets or sets file name
            /// </summary>
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets file id
            /// </summary>
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            /// <summary>
            /// Gets or sets file size
            /// </summary>
            [JsonProperty(PropertyName = "size")]
            public long Size { get; set; }
        }

        /// <summary>
        /// Represents unsuccesful download response
        /// </summary>
        private class UnsuccessfulFileDownloadResponse
        {
            /// <summary>
            /// Gets or sets summary of the error
            /// </summary>
            [JsonProperty(PropertyName = "error_summary")]
            public string ErrorSummary { get; set; }

            /// <summary>
            /// Gets or sets end user message
            /// </summary>
            [JsonProperty(PropertyName = "user_message")]
            public string UserMessage { get; set; }
        }
    }
}
