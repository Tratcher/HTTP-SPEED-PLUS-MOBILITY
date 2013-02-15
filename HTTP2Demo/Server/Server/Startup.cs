using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using Owin.Types;

namespace Server
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class Startup
    {
        public void Configuration(IAppBuilder builder)
        {
            /* // Debugging a TypeLoadException
            builder.Use(new Func<AppFunc, AppFunc>(next =>
                {
                    return env =>
                    {
                        return next(env);
                    };
                }));
            */

            builder.UseHttp2();
            builder.Use(new Func<AppFunc, AppFunc>(ignoredNextApp => (AppFunc)Invoke));
        }
        
        // Invoked once per request.
        public Task Invoke(IDictionary<string, object> environment)
        {
            OwinResponse owinResponse = new OwinResponse(environment);
            /*
	        string responseText = "Hello World";
	        byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
	  */
	        // owinResponse.SetHeader("Content-Length", responseBytes.Length.ToString(CultureInfo.InvariantCulture));
            // owinResponse.SetHeader("Content-Type", "text/plain");

            // return owinResponse.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
            return Task.FromResult<object>(null);
        }
    }
}
