using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Networking {

    public class UdpSocket {

        #region Events

        public event UdpSocketEventHandler ConnectionSuccessful;
        public event UdpSocketErrorEventHandler ConnectionFailed;
        public event UdpSocketErrorEventHandler ConnectionLost;
        public event UdpDataEventHandler DataReceived;
        public event UdpDataEventHandler DataSent;

        #endregion

        #region Local Variables

        private UdpClient _socket = new UdpClient();
        private IPEndPoint _serverEP;
        public bool Connected;

        public int LocalPort { get { try { return int.Parse( _socket.Client.LocalEndPoint.ToString().Split( ':' )[ 1 ] ); } catch ( Exception ) { return 0; } } }
        public IPAddress LocalIP { get { try { return IPAddress.Parse( _socket.Client.LocalEndPoint.ToString().Split( ':' )[ 0 ] ); } catch ( Exception ) { return null; } } }
        public EndPoint LocalEndPoint { get { try { return _socket.Client.LocalEndPoint; } catch ( Exception ) { return null; } } }
        public EndPoint RemoteEndPoint { get { try { return _socket.Client.RemoteEndPoint; } catch ( Exception ) { return null; } } }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        public UdpSocket() { }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class and automatically connects it to the given IP and port.
        /// </summary>
        public UdpSocket( string hostname, int port ) => Connect( hostname, port );
        /// <summary>
        /// Converts a <see cref="UdpClient"/> to a <see cref="UdpSocket"/>.
        /// </summary>
        public UdpSocket( UdpClient socket ) { _socket = socket; }

        #endregion

        #region Methods

        #region Connection Methods

        public void Connect( string hostname, int port ) {
            if ( string.IsNullOrEmpty( hostname ) || port < 1 )
                return;

            if ( !IPAddress.TryParse( hostname, out IPAddress ip ) )
                ip = Dns.GetHostAddresses( hostname )[ 0 ];
            if ( ip == null )
                throw new Exception( "Could not resolve hostname \"" + hostname + "\"" );

            _serverEP = new IPEndPoint( ip, port );
            Send( new Login {
                Username = "UNKNOWN",
                LocalEndPoint = new IPEndPoint( IPAddress.Parse( LocalEndPoint.ToString().Split( ':' )[ 0 ] ), int.Parse( LocalEndPoint.ToString().Split( ':' )[ 1 ] ) ),
                RemoteEndPoint = new IPEndPoint( IPAddress.Parse( RemoteEndPoint.ToString().Split( ':' )[ 0 ] ), int.Parse( RemoteEndPoint.ToString().Split( ':' )[ 1 ] ) )
            } );

            Packet p = ReceiveOnce();
            if ( p.Type.Name.ToLower() != "command" ) {
                Connect( hostname, port );
                return;
            }

            if ( !p.TryDeserializePacket( out Command cmd ) )
                throw new InvalidCastException( "Could not convert packet of type \"{p.Type.Name}\", was the packet corrupted?" );

            if ( cmd.Type == CommandType.UsernameTaken ) {
                Console.WriteLine( "Could not connect to the server: Username was already taken." );
                return;
            }

            Connected = true;
        }

        public void Close() { Close( true ); }
        public void Close( bool invokeConnectionLostEvent ) {
            if ( invokeConnectionLostEvent )
                ConnectionLost?.Invoke( this, null );

            // TODO: BREAK CONNECTION TO SERVER
        }

        #endregion

        #region Packet Traffic Methods

        public void Send( object data ) { Send( new Packet( data ) ); }

        public void Send( Packet data ) {
            if ( !Connected )
                return;

            Task.Run( // Initiate the sender thread
                () => {
                    try {
                        byte[] buffer = data.SerializePacket();
                        _socket.Send( buffer, buffer.Length, _serverEP );

                        DataSent?.Invoke( this, data );
                    } catch ( Exception ex ) {
                        ConnectionLost?.Invoke( this, ex );
                    }
                }
            );
        }

        public void Receive( int bufferSize = 128 ) {
            if ( !Connected )
                return;

            Task.Run( // Initiate the receiver thread
                () => {
                    bool error = false;
                    do {
                        if ( !TryReceiveOnce( out Packet packet ) ) {
                            error = true;
                            ConnectionLost?.Invoke( this, null );
                        }

                        if ( packet != null )
                            packet.ParseFailed += PacketParseFailed;

                        DataReceived?.Invoke( this, packet );
                    } while ( Connected && !error );
                }
            );
        }

        public bool TryReceiveOnce( out Packet packet, int bufferSize = 4096 ) {
            packet = default( Packet );
            try {
                packet = ReceiveOnce( bufferSize );
                return true;
            } catch ( Exception ) { return false; }
        }

        public Packet ReceiveOnce( int bufferSize = 4096 ) {
            if ( !Connected )
                return null;

            byte[] bytes = _socket.Receive( ref _serverEP );
            return new Packet( bytes );
        }

        #endregion

        #region Event Callbacks

        private void PacketParseFailed( Packet packet ) { Console.WriteLine( "Failed to convert packet with type \"" + packet.Type + "\" to type \"string\"" ); }

        #endregion

        #endregion

    }
}
