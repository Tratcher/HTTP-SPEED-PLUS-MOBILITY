using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol.Credentials
{
    public class CredentialSlot
    {
        private CredentialFrame _frame;

        public CredentialSlot(CredentialFrame frame)
        {
            // TODO: Copy out the frame data into cert objects?
            _frame = frame;
        }
    }
}
