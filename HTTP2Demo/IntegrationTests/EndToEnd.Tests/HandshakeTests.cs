using ClientHandler;
using Microsoft.Owin.Hosting;
using Owin;
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

    public class HandshakeTests
    {
        private const string Url = "http://localhost:12345/";

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        [InlineData("Firefly")]
        public async Task HttpClientHandler_Http2Middleware_FallbackTo11(string server)
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
                    builder.Run((AppFunc)NextSuccess);
                }))
            {
                using (HttpClient client = new HttpClient(new HttpClientHandler()))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpListener")]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        [InlineData("Firefly")]
        public async Task Http2SessionTracker_NoHttp2Middleware_FallbackTo11(string server)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.Run((AppFunc)NextSuccess);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: true, fallbackHandler: new HttpClientHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        [InlineData("Firefly")]
        public async Task Http2SessionTracker_Http2Middleware_Upgrade(string server)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.UseHttp2(http2Branch => http2Branch.Run((AppFunc)NextSuccess));
                    builder.Run((AppFunc)NextNotImplemented);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: true, fallbackHandler: new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpListener")]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        // [InlineData("Firefly")] // DOS Hangs the server. v0.6.3
        public void Http2SessionTrackerWithoutHandshake_Http11Server_Failure(string server)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.Run((AppFunc)NextNotImplemented);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: false, fallbackHandler: new NotImplementedHandler())))
                {
                    // TODO: Xunit 2.0 can handle async lamdas (when it's released).
                    // Should be a TaskCanceledException.
                    Assert.Throws<AggregateException>(() => client.GetAsync(Url).Result);
                }
            }
        }

        [Fact]
        public void HttpClientHandler_Http2SocketServer_Failure()
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = "SocketServer";
                },
                builder =>
                {
                    builder.Run((AppFunc)NextSuccess);
                }))
            {
                using (HttpClient client = new HttpClient(new HttpClientHandler()))
                {
                    // TODO: Xunit 2.0 can handle async lamdas (when it's released).
                    // Should be a HttpRequestException.
                    Assert.Throws<AggregateException>(() => client.GetAsync(Url).Result);
                }
            }
        }

        [Fact]
        public async Task Http2SessionTrackerWithoutHandshake_Http2SocketServer_DirectHttp2()
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = "SocketServer";
                },
                builder =>
                {
                    builder.Run((AppFunc)NextSuccess);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: false, fallbackHandler: new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        [Fact]
        public async Task Http2SessionTrackerWithHandshake_Http2SocketServer_FallBackToHttp2()
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = "SocketServer";
                },
                builder =>
                {
                    builder.Run((AppFunc)NextSuccess);
                }))
            {
                using (HttpClient client = new HttpClient(
                    new Http2SessionTracker(do11Handshake: true, fallbackHandler: new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        private Task NextNotImplemented(IDictionary<string, object> environment)
        {
            throw new NotImplementedException();
        }

        private Task NextSuccess(IDictionary<string, object> environment)
        {
            environment["owin.ResponseStatusCode"] = 201;
            return Task.FromResult<object>(null);
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
