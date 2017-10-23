using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Networking {
    class UdpServer {

        #region Events

        public event UdpSocketEventHandler ClientConnectionRequested;
        public event UdpDataEventHandler PacketReceived;

        #endregion

        #region Local Variables

        private UdpClient _socket;
        public bool ServerActive;

        public int Port { get { try { return int.Parse( _socket.Client.LocalEndPoint.ToString().Split( ':' )[ 1 ] ); } catch ( Exception ) { return 0; } } }
        public IPAddress IP { get { try { return IPAddress.Parse( _socket.Client.LocalEndPoint.ToString().Split( ':' )[ 0 ] ); } catch ( Exception ) { return null; } } }
        public EndPoint LocalEndPoint => _socket.Client.LocalEndPoint;
        public EndPoint RemoteEndPoint => _socket.Client.RemoteEndPoint;

        #endregion

        #region Constructors
        
        /// <summary>
        /// Initializes the UDP server and automatically starts waiting for connections on all available local IP addresses.
        /// </summary>
        /// <param name="port">The local port to listen to</param>
        /// <param name="callback">The method to call if a client has been found</param>
        public UdpServer( int port, UdpSocketEventHandler callback ) {
            _socket = new UdpClient( port );

            ClientConnectionRequested += callback;
            BeginAccepting();
        }

        /// <summary>
        /// Initializes the UDP server and automatically starts waiting for connections.
        /// </summary>
        /// <param name="ip">The local IP to listen to</param>
        /// <param name="port">The local port to listen to</param>
        /// <param name="callback">The method to call if a client has been found</param>
        public UdpServer( string ip, int port, UdpSocketEventHandler callback ) {
            if ( !IPAddress.TryParse( ip, out IPAddress tmp ) )
                throw new InvalidCastException( $"Could not parse \"{ip}\" to a valid IP address" );

            _socket = new UdpClient( new IPEndPoint( tmp, port ) );

            ClientConnectionRequested += callback;
            BeginAccepting();
        }

        #endregion

        #region Methods

        private void BeginAccepting() {
            ServerActive = true;

            while ( ServerActive ) {
                IPEndPoint newClient = new IPEndPoint( IPAddress.Any, 0 );
                byte[] bytes = new byte[ 4096 ];
                bytes = _socket.Receive( ref newClient );

                Packet p = new Packet( bytes );
                if ( p.Type.Name.ToLower() != "login" ) {
                    Console.WriteLine( $"Invalid packet type received: \"{p.Type.Name}\". Expected type: \"Login\"." );
                    return;
                }
                
                //ClientConnectionRequested?.Invoke(  );
            }
        }

        #endregion

    }
}
