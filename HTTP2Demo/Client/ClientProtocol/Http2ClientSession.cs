using SharedProtocol;
using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProtocol
{
    public class Http2ClientSession : Http2BaseSession
    {
        private Stream _sessionStream;

        public Http2ClientSession(Stream sessionStream)
        {
            _sessionStream = sessionStream;
        }

        public Http2ClientStream GetStream(int id)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _sessionStream.Dispose();
        }

        protected override void DispatchIncomingFrame(Frame frame)
        {
            throw new NotImplementedException();
        }
    }
}
