using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol
{
    public class Http2BaseStream
    {
        // Additional data has arrived for the request stream.  Add it to our request stream buffer, 
        // update any necessary state (e.g. FINs), and trigger any waiting readers.
        public void ReceiveData(DataFrame dataFrame)
        {
            throw new NotImplementedException();
        }
    }
}
