using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Networking {

    #region Event Handlers

    public delegate void TcpSocketEventHandler( TcpSocket socket );
    public delegate void UdpSocketEventHandler( UdpSocket socket );
    public delegate void TcpSocketErrorEventHandler( TcpSocket socket, Exception ex );
    public delegate void UdpSocketErrorEventHandler( UdpSocket socket, Exception ex );

    #endregion

    public class TcpSocket {

        #region Events

        public event TcpSocketEventHandler ConnectionSuccessful;
        public event TcpSocketErrorEventHandler ConnectionFailed;
        public event TcpSocketErrorEventHandler ConnectionLost;
        public event TcpDataEventHandler DataReceived;
        public event TcpDataEventHandler DataSent;

        #endregion

        #region Local Variables

        private TcpClient _socket = new TcpClient();

        public NetworkStream Stream { get { try { return _socket.GetStream(); } catch ( Exception ) { return null; } } }
        public EndPoint LocalEndPoint { get { try { return _socket.Client.LocalEndPoint; } catch ( Exception ) { return null; } } }
        public EndPoint RemoteEndPoint { get { try { return _socket.Client.RemoteEndPoint; } catch ( Exception ) { return null; } } }
        public bool Connected { get { try { return _socket.Connected; } catch( Exception ) { return false; } } }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the <see cref="TcpSocket"/> class.
        /// </summary>
        public TcpSocket() { }
        /// <summary>
        /// Creates a new instance of the <see cref="TcpSocket"/> class and automatically connects it to the given IP and port.
        /// </summary>
        public TcpSocket( string hostname, int port ) { _socket.Connect( hostname, port ); }
        /// <summary>
        /// Converts a TcpClient to a TcpSocket.
        /// </summary>
        public TcpSocket( TcpClient socket ) { _socket = socket; }

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

            _socket.BeginConnect( ip.ToString(), port, ar => {
                try {
                    _socket.EndConnect( ar );

                    if ( ( ( TcpSocket )ar.AsyncState )._socket.Connected ) {
                        ConnectionSuccessful?.Invoke( this );
                    } else
                        ConnectionFailed?.Invoke( this, null );
                } catch ( Exception ex ) {
                    ConnectionFailed?.Invoke( this, ex );
                }
            }, this );
        }

        public void Close() { Close( true ); }
        public void Close( bool invokeConnectionLostEvent ) {
            if ( _socket.Connected && invokeConnectionLostEvent )
                ConnectionLost?.Invoke( this, null );

            _socket.Close();
            Stream?.Close();
        }

        #endregion

        #region Packet Traffic Methods

        public void Send( object data ) { Send( new Packet( data ) ); }

        public void Send( Packet data ) {
            if ( Stream == null || !_socket.Connected )
                return;

            Task.Run( // Initiate the sender thread
                () => {
                    try {
                        byte[] buffer = data.SerializePacket();
                        Stream?.Write( buffer, 0, buffer.Length );

                        DataSent?.Invoke( this, data );
                    } catch ( Exception ex ) {
                        ConnectionLost?.Invoke( this, ex );
                    }
                }
            );
        }

        public void Receive( int bufferSize = 128 ) {
            if ( Stream == null || !_socket.Connected )
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
                    } while ( _socket.Connected && !error );
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
            if ( Stream == null || !_socket.Connected )
                return null;

            byte[] bytes = new byte[ bufferSize ];
            int length = ( Stream?.Read( bytes, 0, bytes.Length ) ).Value;

            return new Packet( new List<byte>( bytes ).GetRange( 0, length ) );
        }

        #endregion

        #region Event Callbacks

        private void PacketParseFailed( Packet packet ) { Console.WriteLine( "Failed to convert packet with type \"" + packet.Type + "\" to type \"string\"" ); }

        #endregion

        #endregion

    }

}