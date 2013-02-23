using ClientProtocol;
using ServerProtocol;
using SharedProtocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ProtocolIntegration.Tests
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class PingTests
    {
        [Fact]
        public Task ClientPings()
        {
            return RunSessionAsync(NotImplementedApp, async (clientSession, serverSession) =>
            {
                TimeSpan roundTrip = await clientSession.PingAsync();
                Assert.InRange(roundTrip.TotalMilliseconds, 0, 100);
            });
        }

        [Fact]
        public Task ClientPingsx5()
        {
            return RunSessionAsync(NotImplementedApp, async (clientSession, serverSession) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    TimeSpan roundTrip = await clientSession.PingAsync();
                    Assert.InRange(roundTrip.TotalMilliseconds, 0, 100);
                }
            });
        }

        [Fact]
        public Task ServerPings()
        {
            return RunSessionAsync(NotImplementedApp, async (clientSession, serverSession) =>
            {
                TimeSpan roundTrip = await serverSession.PingAsync();
                Assert.InRange(roundTrip.TotalMilliseconds, 0, 100);
            });
        }

        [Fact]
        public Task ServerPingsx5()
        {
            return RunSessionAsync(NotImplementedApp, async (clientSession, serverSession) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    TimeSpan roundTrip = await serverSession.PingAsync();
                    Assert.InRange(roundTrip.TotalMilliseconds, 0, 100);
                }
            });
        }

        [Fact]
        public Task ClientAndServerPings()
        {
            return RunSessionAsync(NotImplementedApp, async (clientSession, serverSession) =>
            {
                Task<TimeSpan> clientPing = clientSession.PingAsync();
                Task<TimeSpan> serverPing = serverSession.PingAsync();
                TimeSpan clientRoundTrip = await clientPing;
                TimeSpan serverRoundTrip = await serverPing;
                Assert.InRange(clientRoundTrip.TotalMilliseconds, 0, 100);
                Assert.InRange(serverRoundTrip.TotalMilliseconds, 0, 100);
            });
        }

        public static async Task RunSessionAsync(AppFunc app,
            Func<Http2ClientSession, Http2ServerSession, Task> sessionOperations)
        {
            DuplexStream clientStream = new DuplexStream();
            DuplexStream serverStream = clientStream.GetOpositeStream();

            Task clientTask, serverTask;
            using (Http2ClientSession clientSession = new Http2ClientSession(false, CancellationToken.None))
            {
                using (Http2ServerSession serverSession = new Http2ServerSession(app, new TransportInformation()))
                {
                    clientTask = clientSession.Start(clientStream, CancellationToken.None);
                    serverTask = serverSession.Start(serverStream, CancellationToken.None);

                    await sessionOperations(clientSession, serverSession);
                }
            }

            await clientTask;
            await serverTask;
        }

        public static Task NotImplementedApp(IDictionary<string, object> environment)
        {
            throw new NotImplementedException();
        }
    }
}
