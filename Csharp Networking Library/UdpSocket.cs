using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Networking {

    public class UdpSocket {

        #region Events

        public event SocketEventHandler ConnectionSuccessful;
        public event SocketErrorEventHandler ConnectionFailed;
        public event DataEventHandler DataReceived;

        #endregion

        #region Local Variables

        private Ping _ping;

        private Socket _listener;
        private Socket _client;

        public EndPoint LocalEndPoint { get { try { return _client.LocalEndPoint; } catch ( Exception ) { return null; } } }
        public EndPoint RemoteEndPoint { get { try { return _client.RemoteEndPoint; } catch ( Exception ) { return null; } } }

        public bool Connected;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        public UdpSocket() {
            // Initialize both client and listener sockets
            _client = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
            _listener = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class and automatically connects it to the given IP and port.
        /// </summary>
        public UdpSocket( string hostname, int port ) {
            // Initialize both client and listener sockets
            _client = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
            _listener = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };

            Connect( hostname, port );
        }

        /// <summary>
        /// Converts an <see cref="EndPoint"/> to a <see cref="UdpSocket"/>.
        /// </summary>
        public UdpSocket( EndPoint newEP ) {
            // Initialize both client and listener sockets
            _client = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
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
            _client.Connect( hostEndPoint );
            // Start listening to the newly created connection the the server
            _listener.Bind( _client.LocalEndPoint );

            SendPing();
            Connected = true;
        }

        public void Close() { Close( true ); }
        public void Close( bool invokeConnectionLostEvent ) {
            if ( invokeConnectionLostEvent )
                ConnectionFailed?.Invoke( this, null );

            // TODO: BREAK CONNECTION TO SERVER
            _client.Close();
        }

        #endregion

        #region Packet Traffic Methods

        public void Send( object data ) => Send( new Packet( data ) );
        public void Send( Packet data ) => Send( data, false );
        public void Send( object data, bool debug ) => Send( new Packet( data ), debug );
        public void Send( Packet data, bool debug ) {
            if ( data.Type.Name.ToLower() != "ping" && !Connected ) {
                Console.WriteLine( "Could not connect to the server" );
                return;
            }

            try {
                _client.SendTo( data.Serialized, RemoteEndPoint );

                // Strictly for debugging the sent messages
                if ( debug )
                    Console.WriteLine( $"SENT \"{data.Content}\" TO {RemoteEndPoint}" );
            } catch ( Exception ex ) {
                ConnectionFailed?.Invoke( this, ex );
            }
        }

        public void SendPing() => SendPing( 25 );

        private void SendPing( int tries ) {
            if ( tries <= 0 ) {
                if ( _ping.Received ) {
                    Console.WriteLine( "Pong received!" );
                    Connected = true;
                    ConnectionSuccessful?.Invoke( this );
                } else {
                    Console.WriteLine( "Ping did not come back on time." );
                    Connected = false;
                    ConnectionFailed?.Invoke( this, null );
                    return;
                }
            }

            Task.Run( () => {
                _ping = new Ping();
                string pingID = _ping.ID;

                Send( _ping );
                Thread.Sleep( 200 );

                if ( _ping.ID == pingID && !_ping.Received )
                    SendPing( --tries );
            } );
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
                            ConnectionFailed?.Invoke( this, null );
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
                Packet p = ReceiveOnce( bufferSize );
                return p != null;
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
            if ( p.Type.Name.ToLower() == "ping" && p.TryDeserializePacket( out _ping ) )
                Console.WriteLine( _ping.Received ? "Received pong!" : "Something went wrong with a pong message." );

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
