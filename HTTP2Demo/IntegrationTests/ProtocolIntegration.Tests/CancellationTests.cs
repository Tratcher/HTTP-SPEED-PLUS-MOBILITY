using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ProtocolIntegration.Tests
{
    using ClientProtocol;
    using Owin.Types;
    using ServerProtocol;
    using SharedProtocol.IO;
    using System.IO;
    using System.Threading;
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class CancellationTests
    {
        [Fact]
        public Task ApplicationExceptionBeforeHeaders_500StatusCode()
        {
            return RunSessionAsync((AppFunc)(env => { throw new NotImplementedException(); }), 
                async (clientSession, serverSession) =>
            {
                IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("GET");
                Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, false, CancellationToken.None);
                IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                Assert.Equal(2, responsePairs.Count);
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "500 Internal Server Error")));
                Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                int read = clientProtocolStream.ResponseStream.Read(new byte[10], 0, 10);
                Assert.Equal(0, read);
            });
        }

        [Fact]
        public Task ApplicationExceptionAfterHeaders_StreamResetIOException()
        {
            ManualResetEvent waitForRead = new ManualResetEvent(false);
            return RunSessionAsync(
                (AppFunc)(env => 
                {
                    new OwinResponse(env).Write("Hello World");
                    throw new NotImplementedException(); 
                }),
                async (clientSession, serverSession) =>
                {
                    IList<KeyValuePair<string, string>> requestPairs = GenerateHeaders("GET");
                    Http2ClientStream clientProtocolStream = clientSession.SendRequest(requestPairs, null, 3, false, CancellationToken.None);
                    IList<KeyValuePair<string, string>> responsePairs = await clientProtocolStream.GetResponseAsync();

                    Assert.Equal(2, responsePairs.Count);
                    Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":status", "200 OK")));
                    Assert.True(responsePairs.Contains(new KeyValuePair<string, string>(":version", "HTTP/1.1")));
                    int read = clientProtocolStream.ResponseStream.Read(new byte[20], 0, 20);
                    Assert.Equal("Hello World".Length, read);

                    Assert.Throws<IOException>(() => clientProtocolStream.ResponseStream.Read(new byte[20], 0, 20));
                    Assert.Throws<AggregateException>(() => clientProtocolStream.ResponseStream.ReadAsync(new byte[20], 0, 20).Result);
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

        private static TransportInformation CreateTransportInfo()
        {
            return new TransportInformation();
        }
    }
}
