using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ServerOwinMiddleware.Tests
{
    using OpaqueUpgrade = Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>;
    using OpaqueFunc = Func<IDictionary<string, object>, Task>;

    public class Http2MiddlewareTests
    {
        [Fact]
        public async Task NonUpgradeRequest_PassesThrough()
        {
            Http2Middleware middleware = new Http2Middleware(NextSuccess, NextNotImplemented);
            IDictionary<string, object> environment = CreateBasicRequest();
            await middleware.Invoke(environment);

            Assert.Equal(201, environment["owin.ResponseStatusCode"]);
        }

        [Fact]
        public async Task UpgradeRequest_Upgrades()
        {
            bool upgradeCalled = false;
            Http2Middleware middleware = new Http2Middleware(NextNotImplemented, NextSuccess);
            IDictionary<string, object> environment = CreateUpgradeRequest();
            environment["opaque.Upgrade"] = new OpaqueUpgrade((options, callback) => upgradeCalled = true);
            await middleware.Invoke(environment);

            Assert.True(upgradeCalled);
            Assert.Equal(101, environment["owin.ResponseStatusCode"]);
            IDictionary<string, string[]> responseHeaders =
                (IDictionary<string, string[]>)environment["owin.ResponseHeaders"];
            Assert.Equal(2, responseHeaders.Count); // Connection, Upgrade
            Assert.Equal("Upgrade", responseHeaders["Connection"][0]);
            Assert.Equal("HTTP/2.0", responseHeaders["Upgrade"][0]);
        }

        private IDictionary<string, object> CreateUpgradeRequest()
        {
            return new Dictionary<string, object>()
            {
                { "owin.RequestMethod", "GET" },
                { "owin.RequestHeaders", new Dictionary<string, string[]>()
                    {
                        { "Connection", new[] { "Upgrade" } },
                        { "Upgrade", new[] { "HTTP/2.0" } },
                    }
                },
                { "owin.ResponseHeaders", new Dictionary<string, string[]>() },
            };
        }

        private IDictionary<string, object> CreateBasicRequest()
        {
            return new Dictionary<string, object>()
            {
                { "owin.RequestHeaders", new Dictionary<string, string[]>() },
                { "owin.ResponseHeaders", new Dictionary<string, string[]>() },
            };
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
    }
}
