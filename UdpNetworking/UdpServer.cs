using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdpNetworking {

    #region Event Handlers

    public delegate void SocketEventHandler( UdpSocket socket );

    #endregion

    public class UdpServer {

        #region Events

        /// <summary>Fires when a new client has connected.</summary>
        public SocketEventHandler OnNewClientRequest;
        /// <summary>Fires when the <seealso cref="Clients"/> list has updated.</summary>
        public SocketEventHandler OnClientListUpdated;
        /// <summary>Fires when a new packet has been received.</summary>
        public DataEventHandler OnDataReceived;
        /// <summary>Fires when a packet has been sent by any of the clients in the <seealso cref="Clients"/> list.</summary>
        public DataEventHandler OnDataSent;

        #endregion

        #region Local Variables

        private Socket _socket;
        
        /// <summary>The list of <see cref="UdpSocket"/>s managed by the <see cref="UdpServer"/>.</summary>
        public List<UdpSocket> Clients = new List<UdpSocket>();
        /// <summary>Whether the <see cref="UdpServer"/> is bound and started receiving.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The <see cref="EndPoint"/> that the server is bound to.</summary>
        public EndPoint LocalEndPoint { get { try { return _socket.LocalEndPoint; } catch ( Exception ) { return null; } } }
        /// <summary>Whether the <see cref="UdpServer"/> is in blocking mode</summary>
        public bool IsBlocking { get { try { return _socket.Blocking; } catch ( Exception ) { return false; } } }

        #endregion

        #region Constructors

        /// <summary>Creates a new instance of the <see cref="UdpServer"/> class.</summary>
        public UdpServer() => _socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to all available <see cref="IPAddress"/>es and the given and port.
        /// </summary>
        /// <param name="port">The port to bind the server on</param>
        public UdpServer( int port ) {
            _socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

            Bind( new IPEndPoint( IPAddress.Any, port ) );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to the given hostname and port.
        /// </summary>
        /// <param name="hostname">The hostname to bind the server to</param>
        /// <param name="port">The port to bind the server on</param>
        public UdpServer( string hostname, int port ) {
            _socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

            Bind( hostname, port );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to the given <see cref="IPAddress"/> and port.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> to bind the server to</param>
        /// <param name="port">The port to bind the server on</param>
        public UdpServer( IPAddress hostIP, int port ) {
            _socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

            Bind( new IPEndPoint( hostIP, port ) );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to the given <see cref="EndPoint"/>.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> to bind the server to</param>
        public UdpServer( EndPoint endPoint ) {
            _socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

            Bind( endPoint );
        }

        #endregion

        #region Methods

        #region Connection-Methods

        /// <summary>
        /// Binds the <see cref="UdpServer"/> to the given hostname and port.
        /// </summary>
        /// <param name="hostname">The hostname to bind to</param>
        /// <param name="port">The port to bind to</param>
        public void Bind( string hostname, int port ) {
            if ( string.IsNullOrEmpty( hostname ) )
                throw new Exception( $"Could not bind, the hostname parameter is empty" );

            if ( port < 1 )
                throw new Exception( $"Could not bind, port is number is invalid: \"{port}\"" );

            if ( !IPAddress.TryParse( hostname, out IPAddress ip ) )
                ip = Dns.GetHostAddresses( hostname )[ 0 ];
            if ( ip == null )
                throw new Exception( "Could not resolve hostname \"" + hostname + "\"" );

            Bind( new IPEndPoint( ip, port ) );
        }

        /// <summary>
        /// Binds the <see cref="UdpServer"/> to the given <see cref="IPAddress"/> and port.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> to bind to</param>
        /// <param name="port">The port to bind to</param>
        public void Bind( IPAddress hostIP, int port ) {
            if ( port < 1 )
                throw new Exception( $"Could not bind, port is number is invalid: \"{port}\"" );

            if ( hostIP == null )
                throw new Exception( "Could not bind, IP is of type \"null\"" );

            Bind( new IPEndPoint( hostIP, port ) );
        }

        /// <summary>
        /// Binds the <see cref="UdpServer"/> to the given <see cref="EndPoint"/>.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> to bind to</param>
        public void Bind( EndPoint endPoint ) {
            _socket.Bind( endPoint );
        }

        /// <summary>
        /// Closes the <see cref="UdpServer"/> and releases all associated resources.
        /// </summary>
        public void Close() => _socket.Close();

        #endregion

        #region Trafficking-Methods

        /// <summary>
        /// Start listening for incoming packets from the remote host, if found then call the <seealso cref="OnDataReceived"/> event.
        /// </summary>
        public void StartReceiving() => StartReceiving( 4096 );
        /// <summary>
        /// Start listening for incoming packets from the remote host, if found then call the <seealso cref="OnDataReceived"/> event.
        /// <param name="bufferSize">The maximum packet size to receive</param>
        /// </summary>
        public void StartReceiving( int bufferSize ) {
            IsActive = true;

            Task.Run( () => {
                while ( IsActive ) {
                    if ( !TryReceiveOnce( out Packet p, out EndPoint ep, bufferSize ) || p == null || ep == null )
                        continue;

                    try {
                        // Check if the UdpSocket EndPoint exists
                        int index = Clients.IndexOf( Clients.First( s => s.RemoteEndPoint.ToString() == ep.ToString() ) );

                        if ( p.Type.Name.ToLower() == "ping" )
                            Clients[index].Send( new Pong( p.DeserializePacket<Ping>().ID ) );

                        OnDataReceived?.Invoke( Clients[index], p );
                    } catch ( Exception ) { /* Ignored */ }
                }
            } );

            Task.Run( () => {
                while ( true ) {
                    Clients.ForEach( s => {
                        if ( !s.Connected )
                            Clients.Remove( s );

                        if ( s.MsSinceLastPing >= 10000 )
                            Clients.Remove( s );

                        Console.WriteLine( $"{s.RemoteEndPoint}'S MS SINCE LAST PING: {s.MsSinceLastPing}" );
                    } );

                    Thread.Sleep( 1000 );
                }
            } );
        }
        /// <summary>
        /// Stops listening for incoming packets from the remote host.
        /// </summary>
        public void StopReceiving() => IsActive = false;

        /// <summary>
        /// Try to receive a packet from the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to fill if a <see cref="Packet"/> has arrived</param>
        /// <param name="endPoint">The remote <see cref="EndPoint"/> that sent the received <see cref="Packet"/></param>
        /// <returns>True if a packet has arrived, false if the packet was corrupted or was null</returns>
        public bool TryReceiveOnce( out Packet packet, out EndPoint endPoint ) => TryReceiveOnce( out packet, out endPoint, 4096 );
        /// <summary>
        /// Try to receive a packet from the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to fill if a <see cref="Packet"/> has arrived</param>
        /// <param name="endPoint">The remote <see cref="EndPoint"/> that sent the received <see cref="Packet"/></param>
        /// <param name="bufferSize">The maximum packet size to receive</param>
        /// <returns>True if a packet has arrived, false if the packet was corrupted or was null</returns>
        public bool TryReceiveOnce( out Packet packet, out EndPoint endPoint, int bufferSize ) {
            packet = null;
            try {
                packet = ReceiveOnce( out endPoint, bufferSize );
                return packet != null;
            } catch ( Exception ) {
                endPoint = null;
                return false;
            }
        }

        /// <summary>
        /// Receive a packet from the remote host and handle any new connections.
        /// </summary>
        /// <param name="endPoint">The remote <see cref="EndPoint"/> that sent the received <see cref="Packet"/></param>
        /// <returns>The received <see cref="Packet"/></returns>
        public Packet ReceiveOnce( out EndPoint endPoint ) => ReceiveOnce( out endPoint, 4096 );
        /// <summary>
        /// Receive a packet from the remote host and handle any new connections.
        /// </summary>
        /// <param name="endPoint">The remote <see cref="EndPoint"/> that sent the received <see cref="Packet"/></param>
        /// <param name="bufferSize">The maximum packet size to receive</param>
        /// <returns>The received <see cref="Packet"/></returns>
        public Packet ReceiveOnce( out EndPoint endPoint, int bufferSize ) {
            if ( _socket == null )
                throw new Exception( $"Cannot receive a packet when the UdpSocket is not listening for packets." );

            EndPoint newClient = new IPEndPoint( IPAddress.Any, 0 );
            byte[] buffer = new byte[ bufferSize ];
            int length = _socket.ReceiveFrom( buffer, ref newClient );
            Packet p = new Packet( buffer.ToList().GetRange( 0, length ) );

            endPoint = newClient;

            try {
                UdpSocket tmp = Clients.First( s => s.RemoteEndPoint.ToString() == newClient.ToString() );
                tmp.ResetPingWatch();
                return p;
            } catch ( Exception ) { /* Ignored */ }

            UdpSocket socket = new UdpSocket( endPoint );
            Clients.Add( socket );
            OnNewClientRequest?.Invoke( socket );

            return p;
        }

        #endregion

        #endregion

    }

}
