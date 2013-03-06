using ClientHandler;
using Microsoft.Owin.Hosting;
using Owin;
using Owin.Types;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace EndToEnd.Tests
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class UploadTests
    {
        private const string Url = "http://localhost:12345/";

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpListener")]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        [InlineData("Firefly")]
        public async Task HttpClientHandler_Http2Middleware_UploadOn11(string server)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.UseHttp2(http2Branch => http2Branch.Run((AppFunc)NextNotImplemented));
                    builder.Run((AppFunc)Echo);
                }))
            {
                using (HttpClient client = new HttpClient(new HttpClientHandler()))
                {
                    HttpResponseMessage response = await client.PostAsync(Url, new StringContent("Hello World"));
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    Assert.Equal("Hello World", body);
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        [InlineData("Firefly")]
        public async Task Http2SessionTracker_Http2Middleware_UploadOn20(string server)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.UseHttp2(http2Branch => http2Branch.Run((AppFunc)Echo));
                    builder.Run((AppFunc)NextNotImplemented);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: true, fallbackHandler: new NotImplementedHandler())))
                {
                    // Do a dummy request to open the session
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();

                    response = await client.PostAsync(Url, new StringContent("Hello World"));
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    Assert.Equal("Hello World", body);
                }
            }
        }

        [Fact]
        public async Task Http2SessionTrackerWithoutHandshake_Http2SocketServer_DirectHttp2Upload()
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = "SocketServer";
                },
                builder =>
                {
                    builder.UseHttp2(http2Branch => http2Branch.Run((AppFunc)NextNotImplemented));
                    builder.Run((AppFunc)Echo);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: false, fallbackHandler: new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.PostAsync(Url, new StringContent("Hello World"));
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    Assert.Equal("Hello World", body);
                }
            }
        }

        public async Task Echo(IDictionary<string, object> environment)
        {
            OwinRequest request = new OwinRequest(environment);
            OwinResponse response = new OwinResponse(environment);
            await request.Body.CopyToAsync(response.Body);
        }

        private Task NextNotImplemented(IDictionary<string, object> environment)
        {
            throw new NotImplementedException();
        }

        private class NotImplementedHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
