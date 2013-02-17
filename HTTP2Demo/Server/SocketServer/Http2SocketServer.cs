using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Owin.Types;
using System.Net;
using System.Globalization;
using System.Net.Sockets;

namespace SocketServer
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using System.Net.Security;
    using System.IO;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using ServerProtocol;
    using System.Threading;

    public class Http2SocketServer : IDisposable
    {
        private AppFunc _next;
        private bool _enableSsl;
        private int _port;
        private Socket _socket;
        private bool _disposed;
        private X509Certificate _serverCert;
        private SslProtocols _sslProtocols = SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        public Http2SocketServer(Func<IDictionary<string, object>, Task> next, IDictionary<string, object> properties)
        {
            _next = next;
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp); // Dual mode

            IList<IDictionary<string, object>> addresses = 
                (IList<IDictionary<string, object>>)properties[OwinConstants.CommonKeys.Addresses];

            IDictionary<string, object> address = addresses.First();
            _enableSsl = !string.Equals((address.Get<string>("scheme") ?? "http"), "http", StringComparison.OrdinalIgnoreCase);

            _port = Int32.Parse(address.Get<string>("port") ?? (_enableSsl ? "443" : "80"), CultureInfo.InvariantCulture);

            string host = address.Get<string>("host");
            if (string.IsNullOrWhiteSpace(host) || !host.Equals("*") || !host.Equals("+"))
            {
                _socket.Bind(new IPEndPoint(IPAddress.IPv6Any, _port));
            }
            else
            {
                _socket.Bind(new IPEndPoint(Dns.GetHostAddresses(host)[0], _port));
            }

            Listen();
        }

        private async void Listen()
        {
            _socket.Listen(backlog: 2);
            while (!_disposed)
            {
                try
                {
                    Socket clientSocket = await Task.Factory.FromAsync(_socket.BeginAccept, (Func<IAsyncResult, Socket>)_socket.EndAccept, null);
                    Stream stream = new NetworkStream(clientSocket, ownsSocket: true);

                    X509Certificate clientCert = null;

                    if (_enableSsl)
                    {
                        SslStream sslStream = new SslStream(stream);
                        await sslStream.AuthenticateAsServerAsync(_serverCert, clientCertificateRequired: false, enabledSslProtocols: _sslProtocols, checkCertificateRevocation: false);
                        clientCert = sslStream.RemoteCertificate;
                        stream = sslStream;
                    }

                    // TODO: At this point we could read the first bit of the first byte received on this connection to determine if it is a HTTP/1.1 or 2.0 request.

                    IPEndPoint localEndPoint = (IPEndPoint)clientSocket.LocalEndPoint;
                    IPEndPoint remoteEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint;

                    TransportInformation transportInfo = new TransportInformation()
                    {
                        ClientCertificate = clientCert,
                        LocalPort = localEndPoint.Port.ToString(CultureInfo.InvariantCulture),
                        RemotePort = remoteEndPoint.Port.ToString(CultureInfo.InvariantCulture),
                    };
                    
                    // Side effect of using dual mode sockets, the IPv4 addresses look like 0::ffff:127.0.0.1.
                    if (localEndPoint.Address.IsIPv4MappedToIPv6)
                    {
                        transportInfo.LocalIpAddress = localEndPoint.Address.MapToIPv4().ToString();
                    }
                    else
                    {
                        transportInfo.LocalIpAddress = localEndPoint.Address.ToString();
                    }

                    if (remoteEndPoint.Address.IsIPv4MappedToIPv6)
                    {
                        transportInfo.RemoteIpAddress = remoteEndPoint.Address.MapToIPv4().ToString();
                    }
                    else
                    {
                        transportInfo.RemoteIpAddress = remoteEndPoint.Address.ToString();
                    }

                    Http2ServerSession session = new Http2ServerSession(_next, transportInfo);
                    // TODO: awaiting here will only let us accept the next session after the current one finishes.
                    await session.Start(stream, CancellationToken.None);
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                    Dispose();
                }
                catch (Exception)
                {
                    Dispose();
                    throw;
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _socket.Dispose();
        }
    }
}
