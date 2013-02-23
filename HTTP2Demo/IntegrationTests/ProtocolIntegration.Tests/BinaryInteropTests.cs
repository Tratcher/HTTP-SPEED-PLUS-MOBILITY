using ClientProtocol;
using ServerProtocol;
using SharedProtocol;
using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ProtocolIntegration.Tests
{
    using System.IO;
    using AppFunc = Func<IDictionary<string, object>, Task>;

    // Connect and share only binary frames
    public class BinaryInteropTests
    {
        [Fact]
        public async Task StartAndStop()
        {
            await RunSessionAsync(StatusCodeOnlyResponse, (cs, ss) => Task.FromResult<object>(null));
        }

        [Fact]
        public async Task SimpleStatusCodeResponse()
        {
            await RunSessionAsync(StatusCodeOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("GET");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, false, CancellationToken.None);
                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                int read = clientProtocolStream.ResponseStream.Read(new byte[10], 0, 10);
                Assert.Equal(0, read);
            });
        }

        [Fact]
        public async Task HelloWorldResponse()
        {
            await RunSessionAsync(HelloWorldOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, false, CancellationToken.None);
                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                using (StreamReader reader = new StreamReader(clientProtocolStream.ResponseStream))
                {
                    string read = reader.ReadToEnd();
                    Assert.Equal("Hello World", read);
                }
            });
        }

        [Fact]
        public async Task HelloWorldAsyncResponse()
        {
            await RunSessionAsync(HelloWorldAsyncOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, false, CancellationToken.None);
                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                using (StreamReader reader = new StreamReader(clientProtocolStream.ResponseStream))
                {
                    string read = await reader.ReadToEndAsync();
                    Assert.Equal("Hello World", read);
                }
            });
        }

        [Fact]
        public async Task EchoHelloWorldResponse()
        {
            await RunSessionAsync(EchoOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, true, CancellationToken.None);
                using (StreamWriter writer = new StreamWriter(clientProtocolStream.RequestStream))
                {
                    writer.Write("Hello World");
                    writer.Flush();
                }
                clientProtocolStream.EndRequest();

                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                using (StreamReader reader = new StreamReader(clientProtocolStream.ResponseStream))
                {
                    string read = reader.ReadToEnd();
                    Assert.Equal("Hello World", read);
                }
            });
        }

        [Fact]
        public async Task EchoHelloWorldAsyncResponse()
        {
            await RunSessionAsync(EchoAsyncOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, true, CancellationToken.None);
                using (StreamWriter writer = new StreamWriter(clientProtocolStream.RequestStream))
                {
                    await writer.WriteAsync("Hello World");
                    await writer.FlushAsync();
                }
                clientProtocolStream.EndRequest();

                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                using (StreamReader reader = new StreamReader(clientProtocolStream.ResponseStream))
                {
                    string read = await reader.ReadToEndAsync();
                    Assert.Equal("Hello World", read);
                }
            });
        }

        private static IList<KeyValuePair<string, string>> GenerateHeaders(string method)
        {
            IList<KeyValuePair<string, string>> requestPairs = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>(":method", method),
                    new KeyValuePair<string, string>(":path", "/"),
                    new KeyValuePair<string, string>(":version", "HTTP/1.1"),
                    new KeyValuePair<string, string>(":host", "localhost:8080"),
                    new KeyValuePair<string, string>(":scheme", "http"),
                };
            return requestPairs;
        }

        public static async Task RunSessionAsync(AppFunc app,
            Func<Http2ClientSession, Http2ServerSession, Task> sessionOperations)
        {
            DuplexStream clientStream = new DuplexStream();
            DuplexStream serverStream = clientStream.GetOpositeStream();

            Task clientTask, serverTask;
            using (Http2ClientSession clientSession = new Http2ClientSession(false, CancellationToken.None))
            {
                using (Http2ServerSession serverSession = new Http2ServerSession(app, CreateTransportInfo()))
                {
                    clientTask = clientSession.Start(clientStream, CancellationToken.None);
                    serverTask = serverSession.Start(serverStream, CancellationToken.None);

                    await sessionOperations(clientSession, serverSession);
                }
            }

            await clientTask;
            await serverTask;
        }

        public static Task StatusCodeOnlyResponse(IDictionary<string, object> environment)
        {
            environment["owin.ResponseStatusCode"] = 201;
            return Task.FromResult<object>(null);
        }

        public static Task HelloWorldOnlyResponse(IDictionary<string, object> environment)
        {
            environment["owin.ResponseStatusCode"] = 201;
            Stream responseBody = (Stream)environment["owin.ResponseBody"];
            using (StreamWriter writer = new StreamWriter(responseBody))
            {
                writer.Write("Hello World");
                writer.Flush();
            }
            return Task.FromResult<object>(null);
        }

        public static async Task HelloWorldAsyncOnlyResponse(IDictionary<string, object> environment)
        {
            environment["owin.ResponseStatusCode"] = 201;
            Stream responseBody = (Stream)environment["owin.ResponseBody"];
            using (StreamWriter writer = new StreamWriter(responseBody))
            {
                await writer.WriteAsync("Hello World");
                await writer.FlushAsync();
            }
        }

        public static Task EchoOnlyResponse(IDictionary<string, object> environment)
        {
            environment["owin.ResponseStatusCode"] = 201;
            Stream requestBody = (Stream)environment["owin.RequestBody"];
            Stream responseBody = (Stream)environment["owin.ResponseBody"];
            requestBody.CopyTo(responseBody);
            return Task.FromResult<object>(null);
        }

        public static Task EchoAsyncOnlyResponse(IDictionary<string, object> environment)
        {
            environment["owin.ResponseStatusCode"] = 201;
            Stream requestBody = (Stream)environment["owin.RequestBody"];
            Stream responseBody = (Stream)environment["owin.ResponseBody"];
            return requestBody.CopyToAsync(responseBody);
        }

        private static TransportInformation CreateTransportInfo()
        {
            return new TransportInformation();
        }
    }
}
