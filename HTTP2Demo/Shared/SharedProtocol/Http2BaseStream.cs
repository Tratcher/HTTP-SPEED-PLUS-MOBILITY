using SharedProtocol.Compression;
using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedProtocol
{
    public abstract class Http2BaseStream
    {
        protected int _id;
        protected WriteQueue _writeQueue;
        protected CompressionProcessor _compressor;
        protected CancellationToken _cancel;
        protected int _version;
        protected int _priority;

        protected Http2BaseStream(int id, WriteQueue writeQueue, CancellationToken cancel)
        {
            _id = id;
            _writeQueue = writeQueue;
            _cancel = cancel;
            _compressor = new CompressionProcessor();
        }

        public int Id
        {
            get { return _id; }
        }

        public CompressionProcessor Compressor
        {
            get { return _compressor; }
        }

        // Additional data has arrived for the request stream.  Add it to our request stream buffer, 
        // update any necessary state (e.g. FINs), and trigger any waiting readers.
        public void ReceiveData(DataFrame dataFrame)
        {
            throw new NotImplementedException();
        }
    }
}
