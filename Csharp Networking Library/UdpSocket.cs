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

        public event SocketEventHandler ConnectionSuccessful;
        public event SocketErrorEventHandler ConnectionFailed;
        public event SocketErrorEventHandler ConnectionLost;
        public event DataEventHandler DataReceived;
        public event DataEventHandler DataSent;

        #endregion

        #region Local Variables

        private Socket _listener;
        public Socket Client;

        public EndPoint LocalEndPoint { get { try { return Client.LocalEndPoint; } catch ( Exception ) { return null; } } }
        public EndPoint RemoteEndPoint { get { try { return Client.RemoteEndPoint; } catch ( Exception ) { return null; } } }

        public bool Connected;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        public UdpSocket() {
            // Initialize both client and listener sockets
            Client = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
            _listener = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class and automatically connects it to the given IP and port.
        /// </summary>
        public UdpSocket( string hostname, int port ) {
            // Initialize both client and listener sockets
            Client = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
            _listener = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };

            Connect( hostname, port );
        }

        /// <summary>
        /// Converts an <see cref="EndPoint"/> to a <see cref="UdpSocket"/>.
        /// </summary>
        public UdpSocket( EndPoint newEP ) {
            // Initialize both client and listener sockets
            Client = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
            _listener = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };

            Connect( newEP );
        }

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

            Connect( new IPEndPoint( ip, port ) );
        }
        public void Connect( EndPoint hostEndPoint ) {
            // Prepare for connections to the server
            Client.Connect( hostEndPoint );
            // Start listening to the newly created connection the the server
            _listener.Bind( Client.LocalEndPoint );
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
                        Client.SendTo( data.SerializePacket(), LocalEndPoint );

                        // Strictly for debugging the sent messages
                        Console.WriteLine( $"SENT \"{data.Content}\" TO {LocalEndPoint}" );

                        DataSent?.Invoke( this, data );
                    } catch ( Exception ex ) {
                        ConnectionLost?.Invoke( this, ex );
                    }
                }
            );
        }

        public void Receive() => Receive( 4096 );
        public void Receive( int bufferSize ) {
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

        public bool TryReceiveOnce( out Packet packet ) => TryReceiveOnce( out packet, 4096 );
        public bool TryReceiveOnce( out Packet packet, int bufferSize ) {
            packet = default( Packet );
            try {
                packet = ReceiveOnce( bufferSize );
                return true;
            } catch ( Exception ) { return false; }
        }

        public Packet ReceiveOnce() => ReceiveOnce( 4096 );
        public Packet ReceiveOnce( int bufferSize ) {
            if ( !Connected )
                return null;

            // Create a temp EndPoint to store the sender's connection information in
            EndPoint tmp = new IPEndPoint( IPAddress.Any, 0 );
            // Create a temp buffer to store the message in
            byte[] buffer = new byte[ bufferSize ];
            // Receive the message and take the actual length of the message (not the whole 4096 bytes)
            int length = _listener.ReceiveFrom( buffer, ref tmp );

            // Convert and store the message in a variable
            Packet p = new Packet( buffer.ToList().GetRange( 0, length ).ToArray() );

            // Strictly for debugging the received messages
            if ( p.Type.Name.ToLower() != "string" )
                return null;

            Console.WriteLine( $"RECV \"{p.Content}\" FROM {tmp}" );

            // If the message was a PING, send a PONG back
            if ( p.Content.ToString() == "PING" )
                Send( "PONG" );

            return p;
        }

        #endregion

        #region Event Callbacks

        private void PacketParseFailed( Packet packet ) { Console.WriteLine( "Failed to convert packet with type \"" + packet.Type + "\" to type \"string\"" ); }

        #endregion

        #endregion

    }

}
