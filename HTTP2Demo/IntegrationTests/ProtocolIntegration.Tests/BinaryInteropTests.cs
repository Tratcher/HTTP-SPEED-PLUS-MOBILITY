using ClientProtocol;
using ServerProtocol;
using SharedProtocol.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ProtocolIntegration.Tests
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    // Connect and share only binary frames
    public class BinaryInteropTests
    {
        [Fact]
        public Task StartAndStop()
        {
            return RunSessionAsync(StatusCodeOnlyResponse, (cs, ss) => Task.FromResult<object>(null));
        }

        [Fact]
        public Task SimpleStatusCodeResponse()
        {
            return RunSessionAsync(StatusCodeOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("GET");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, false, CancellationToken.None);
                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                int read = clientProtocolStream.ResponseStream.Read(new byte[10], 0, 10);
                Assert.Equal(0, read);
            });
        }

        [Fact]
        public Task SimpleStatusCodeResponseX2()
        {
            return RunSessionAsync(StatusCodeOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("GET");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, false, CancellationToken.None);
                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                int read = clientProtocolStream.ResponseStream.Read(new byte[10], 0, 10);
                Assert.Equal(0, read);
                
                requestPairs = GenerateHeaders("GET");
                clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, false, CancellationToken.None);
                responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                read = clientProtocolStream.ResponseStream.Read(new byte[10], 0, 10);
                Assert.Equal(0, read);
            });
        }

        [Fact]
        public Task HelloWorldResponse()
        {
            return RunSessionAsync(HelloWorldOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, false, CancellationToken.None);
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
        public Task HelloWorldAsyncResponse()
        {
            return RunSessionAsync(HelloWorldAsyncOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, false, CancellationToken.None);
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
        public Task EchoHelloWorldResponse()
        {
            return RunSessionAsync(EchoOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, true, CancellationToken.None);
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
        public Task EchoHelloWorldAsyncResponse()
        {
            return RunSessionAsync(EchoAsyncOnlyResponse, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, true, CancellationToken.None);
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

        [Fact]
        public Task EchoPriorityTest()
        {
            return RunSessionAsync(EchoPriority, async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("POST");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 6, false, CancellationToken.None);

                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "201")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                using (StreamReader reader = new StreamReader(clientProtocolStream.ResponseStream))
                {
                    string read = await reader.ReadToEndAsync();
                    Assert.Equal("6", read);
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
            using (Http2ClientSession clientSession = new Http2ClientSession(clientStream, false, CancellationToken.None, CancellationToken.None))
            {
                using (Http2ServerSession serverSession = new Http2ServerSession(app, CreateTransportInfo()))
                {
                    clientTask = clientSession.Start();
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

        public static Task EchoPriority(IDictionary<string, object> environment)
        {
            environment["owin.ResponseStatusCode"] = 201;
            Stream responseBody = (Stream)environment["owin.ResponseBody"];
            int priority = (int)environment["http2.Priority"];
            StreamWriter writer = new StreamWriter(responseBody);
            writer.AutoFlush = true;
            return writer.WriteAsync(priority.ToString());
        }

        private static TransportInformation CreateTransportInfo()
        {
            return new TransportInformation();
        }
    }
}
