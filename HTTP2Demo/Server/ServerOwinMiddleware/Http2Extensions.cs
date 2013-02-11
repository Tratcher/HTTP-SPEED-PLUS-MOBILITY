using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Owin;
using ServerOwinMiddleware;

namespace Owin
{
    public static class Http2Extensions
    {
        public static IAppBuilder UseHttp2(this IAppBuilder builder)
        {
            return builder.Use(typeof(Http2Middleware));
        }
    }
}
