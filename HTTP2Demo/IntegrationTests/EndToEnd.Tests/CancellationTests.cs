using ClientHandler;
using Microsoft.Owin.Hosting;
using Owin;
using Owin.Types;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace EndToEnd.Tests
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class CancellationTests
    {
        private const string Url = "http://localhost:12345/";

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys", true)]
        [InlineData("SocketServer", false)]
        [InlineData("Firefly", true)]
        public async Task Http2SessionTracker_AppExceptionBeforeResponseHeaders_500Status(string server, bool doHandshake)
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
                    builder.Run((AppFunc)NextNotImplemented);
                }))
            {
                using (HttpClient client = new HttpClient(new Http2SessionTracker(doHandshake, new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);
                    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                    Assert.Equal(string.Empty, response.Content.ReadAsStringAsync().Result);
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys", true)]
        [InlineData("SocketServer", false)]
        [InlineData("Firefly", true)]
        public async Task Http2SessionTracker_AppExceptionAfterResponseHeaders_IOException(string server, bool doHandshake)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.UseHttp2(http2Branch => http2Branch.Run((AppFunc)ExceptionAfterFirstWrite));
                    builder.Run((AppFunc)ExceptionAfterFirstWrite);
                }))
            {
                using (HttpClient client = new HttpClient(new Http2SessionTracker(doHandshake, new NotImplementedHandler())))
                {
                    HttpResponseMessage response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.Throws<AggregateException>(() => response.Content.ReadAsStringAsync().Result);
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys", true)]
        [InlineData("SocketServer", false)]
        [InlineData("Firefly", true)]
        public async Task Http2SessionTracker_ClientCancellationBeforeResponse_TaskCancelled(string server, bool doHandshake)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.UseHttp2(http2Branch => http2Branch.Run((AppFunc)CompleteWhenCancelled));
                    builder.Run((AppFunc)CompleteWhenCancelled);
                }))
            {
                using (HttpClient client = new HttpClient(new Http2SessionTracker(doHandshake, new NotImplementedHandler())))
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    Task requestTask = client.GetAsync(Url, cts.Token);
                    await Task.Delay(500); // Make sure the request hits the wire.
                    cts.Cancel();
                    Assert.Throws<AggregateException>(() => requestTask.Wait());
                }
            }
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.HttpSys")]
        [InlineData("Firefly")]
        public async Task Http2SessionTracker_ClientCancellationBeforeUpgrade_TaskCancelled(string server)
        {
            using (WebApplication.Start(
                options =>
                {
                    options.Url = Url;
                    options.Server = server;
                },
                builder =>
                {
                    builder.Run((AppFunc)CompleteWhenCancelled);
                }))
            {
                using (HttpClient client = new HttpClient(new Http2SessionTracker(true, new NotImplementedHandler())))
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    Task requestTask = client.GetAsync(Url, cts.Token);
                    await Task.Delay(500); // Make sure the request hits the wire.
                    cts.Cancel();
                    Assert.Throws<AggregateException>(() => requestTask.Wait());
                }
            }
        }

        public async Task ExceptionAfterFirstWrite(IDictionary<string, object> environment)
        {
            OwinResponse response = new OwinResponse(environment);
            await response.WriteAsync("Hello World");
            await Task.Delay(500); // Make sure the headers get received.
            throw new NotImplementedException();
        }

        public Task CompleteWhenCancelled(IDictionary<string, object> environment)
        {
            OwinResponse response = new OwinResponse(environment);
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            response.CallCancelled.Register(() =>
                {
                    tcs.TrySetCanceled();
                });
            return tcs.Task;
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
