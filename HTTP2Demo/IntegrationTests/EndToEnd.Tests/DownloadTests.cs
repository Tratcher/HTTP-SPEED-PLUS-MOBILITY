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

    public class DownloadTests
    {
        private const string Url = "http://localhost:12345/";

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpListener")]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        // [InlineData("Firefly")]
        public async Task HttpClientHandler_Http2Middleware_DownloadOn11(string server)
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
                    builder.Run((AppFunc)DownloadMultipleWrites);
                }))
            {
                using (HttpClient client = new HttpClient(new HttpClientHandler()))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    Assert.Equal("Hello WorldHello WorldHello World", body); 
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        // [InlineData("Firefly")]
        public async Task Http2SessionTracker_Http2Middleware_DownloadOn20(string server)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.UseHttp2(http2Branch => http2Branch.Run((AppFunc)DownloadMultipleWrites));
                    builder.Run((AppFunc)NextNotImplemented);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: true, fallbackHandler: new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    Assert.Equal("Hello WorldHello WorldHello World", body); 
                }
            }
        }

        [Fact]
        public async Task Http2SessionTrackerWithoutHandshake_Http2SocketServer_DirectHttp2Download()
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
                    builder.Run((AppFunc)DownloadMultipleWrites);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: false, fallbackHandler: new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    Assert.Equal("Hello WorldHello WorldHello World", body); 
                }
            }
        }

        public async Task DownloadMultipleWrites(IDictionary<string, object> environment)
        {
            OwinResponse response = new OwinResponse(environment);
            await response.WriteAsync("Hello World");
            await response.WriteAsync("Hello World");
            await response.WriteAsync("Hello World");
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
