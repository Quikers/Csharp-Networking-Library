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

    #region Private Classes

    public class ClientInfo {

        public static UdpServer Listener;

        private int _senderPort = -1;

        public UdpSocket Socket;
        public IPEndPoint ReceiverEndPoint { get { try { return Socket.RemoteEndPoint.ToIPEndPoint(); } catch ( Exception ) { return null; } } }
        public IPEndPoint SenderEndPoint { get { try { return _senderPort > 0 ? new IPEndPoint( ReceiverEndPoint.Address, _senderPort ) : null; } catch ( Exception ) { return null; } } }

        public string ID;

        public ClientInfo( IPEndPoint remoteHost ) {
            SetSocket( remoteHost );

            Init();
        }

        public void SetSocket( IPEndPoint endPoint ) {
            Socket = new UdpSocket();
            Socket.BindSocket( new IPEndPoint( IPAddress.Any, 8080 ) );
            Socket.Connect( endPoint );
        }

        private void Init() {
            ID = Guid.NewGuid().ToString( "n" );
            Send( ID );

            Console.WriteLine( $"SENT NEW ID \"{ID}\" TO {Socket.RemoteEndPoint}" );

            if ( Listener == null ) {
                Console.WriteLine( "Global listener socket not set, set sender port manually required" );
                return;
            }

            //Task.Run( () => {
            IPEndPoint ep = Listener.ReceiveFrom( out Packet p );
            if ( p.Content.ToString() != ID )
                return;

            _senderPort = ep.Port;
            Console.WriteLine( $"RECV SENDER PORT ({_senderPort}) FOR CLIENT {Socket.RemoteEndPoint}#{ID}" );
            //} );
        }

        public void SetSenderPort( int port ) => _senderPort = port;

        public void RenewID() => Init();

        public void Send( object value ) => Socket.Send( value );
        public void Send( Packet packet ) => Socket.Send( packet );
    }

    #endregion

    public class UdpServer {

        #region Events

        /// <summary>Fires when a new <see cref="UdpSocket"/> has connected.</summary>
        public event SocketEventHandler OnNewClientRequest;
        /// <summary>Fires when a new <see cref="Packet"/> has been received.</summary>
        public event DataEventHandler OnDataReceived;
        /// <summary>Fires when a <see cref="Packet"/> has been sent by any of the <see cref="UdpSocket"/>s in the <seealso cref="Clients"/> list.</summary>
        public event DataEventHandler OnDataSent { add => UdpSocket.OnDataSent += value; remove => UdpSocket.OnDataSent -= value; }

        #endregion

        #region Local Variables

        private Socket _socket;

        /// <summary>The list of <see cref="User"/>s managed by the <see cref="UdpServer"/>.</summary>
        public UserList Clients = new UserList();
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
            if ( string.IsNullOrWhiteSpace( hostname ) )
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

        public void Send( object value ) => Send( new Packet( value ) );
        public void Send( Packet packet ) {
            _socket.Send( packet.Serialized );
        }

        public void SendTo( object value, IPEndPoint remoteHost ) => SendTo( new Packet( value ), remoteHost );
        public void SendTo( Packet packet, IPEndPoint remoteHost ) {
            _socket.SendTo( packet.Serialized, remoteHost );
        }

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
                    if ( !TryReceiveFrom( out Packet p, out EndPoint ep, bufferSize ) || p == null || ep == null )
                        continue;

                    try {
                        // Check if the UdpSocket EndPoint exists
                        if ( Clients.Exists( ep ) && p.Type.Name.ToLower() == "ping" )
                            Clients[ ep ].Socket.Send( p.DeserializePacket<Ping>().ToPong() );

                        OnDataReceived?.Invoke( Clients[ ep ].Socket, p );
                    } catch ( Exception ex ) { Console.WriteLine( ex ); }
                }
            } );

            Task.Run( () => {
                while ( true ) {
                    Clients.ToList().ForEach( s => {
                        if ( !s.Socket.Connected )
                            Clients.Remove( s );

                        if ( s.Socket.MsSinceLastPing >= 10000 )
                            Clients.Remove( s );
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
        public bool TryReceiveFrom( out Packet packet, out EndPoint endPoint ) => TryReceiveFrom( out packet, out endPoint, 4096 );
        /// <summary>
        /// Try to receive a packet from the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to fill if a <see cref="Packet"/> has arrived</param>
        /// <param name="endPoint">The remote <see cref="EndPoint"/> that sent the received <see cref="Packet"/></param>
        /// <param name="bufferSize">The maximum packet size to receive</param>
        /// <returns>True if a packet has arrived, false if the packet was corrupted or was null</returns>
        public bool TryReceiveFrom( out Packet packet, out EndPoint endPoint, int bufferSize ) {
            try {
                endPoint = ReceiveFrom( out packet, bufferSize );
                return packet != null;
            } catch ( Exception ) {
                packet = null;
                endPoint = null;
                return false;
            }
        }

        /// <summary>
        /// Receives one <see cref="Packet"/> and stores the remote host's <see cref="IPEndPoint"/>
        /// </summary>
        /// <param name="packet">This variable will be filled with a <see cref="Packet"/> with the contents of the data received</param>
        /// <returns>The <see cref="IPAddress"/> and port information of the remote <see cref="IPEndPoint"/></returns>
        public IPEndPoint ReceiveFrom( out Packet packet ) => ReceiveFrom( out packet, 4096 );

        /// <summary>
        /// Receives one <see cref="Packet"/> and stores the remote host's <see cref="IPEndPoint"/>
        /// </summary>
        /// <param name="packet">This variable will be filled with a <see cref="Packet"/> with the contents of the data received</param>
        /// <param name="bufferSize">The maximum packet size to receive</param>
        /// <returns>The <see cref="IPAddress"/> and port information of the remote <see cref="IPEndPoint"/></returns>
        public IPEndPoint ReceiveFrom( out Packet packet, int bufferSize ) {
            byte[] buffer = new byte[ bufferSize ];
            EndPoint newClient = new IPEndPoint( IPAddress.Any, 8080 );

            int length = _socket.ReceiveFrom( buffer, ref newClient );
            packet = new Packet( buffer.ToList().GetRange( 0, length ) );

            if ( packet.TryDeserializePacket( out GET getRequest ) && getRequest == GET.NewClientID ) {
                Console.WriteLine( "New connectionID requested from " + newClient );
                if ( Clients.Exists( newClient ) ) {
                    Clients[ newClient ].RenewID();
                    Console.WriteLine( "ConnectionID refreshed!" );
                } else {
                    Clients.Add( newClient.ToIPEndPoint() );
                    OnNewClientRequest?.Invoke( Clients[ newClient ].Socket );
                    Console.WriteLine( $"New client ({Clients[newClient].Socket.RemoteEndPoint}) created!" );
                }
            }

            if ( Clients.Exists( newClient ) )
                Clients[ newClient ].Socket.ResetPingWatch();
            
            return newClient.ToIPEndPoint();
        }

        #endregion

        #endregion

    }

}
