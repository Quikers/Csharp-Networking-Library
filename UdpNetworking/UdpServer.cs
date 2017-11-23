using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdpNetworking {

    public class UdpServer {

        #region Events

        /// <summary>Fires when a new <see cref="UdpSocket"/> has connected.</summary>
        public event UserEventHandler OnNewClientRequest;
        private event DataEventHandler _onDataReceived;
        /// <summary>Fires when a new <see cref="Packet"/> has been received.</summary>
        public event DataEventHandler OnDataReceived {
            add {
                _onDataReceived += value;
                UdpSocket.OnDataReceived += value; }
            remove {
                _onDataReceived -= value;
                UdpSocket.OnDataReceived -= value; }
        }
        /// <summary>Fires when a <see cref="Packet"/> has been sent by any of the <see cref="UdpSocket"/>s in the <seealso cref="UserList"/> list.</summary>
        public event DataEventHandler OnDataSent { add => UdpSocket.OnDataSent += value; remove => UdpSocket.OnDataSent -= value; }

        #endregion

        #region Local Variables

        #region Public Variables

        /// <summary>The list of <see cref="User"/>s managed by the <see cref="UdpServer"/>.</summary>
        public UserList UserList = new UserList();
        /// <summary>The port that was given by the user to listen on.</summary>
        public static int Port = -1;

        private static List<int> _usedPorts = new List<int>();

        /// <summary>The range of ports that should be available for this <see cref="UdpServer"/>.</summary>
        public static int PortRange = 50;
        /// <summary>Getting this will return an available port if there are any unused ports within the <seealso cref="PortRange"/>. Setting it will remove a used port from the list.</summary>
        public static int AvailablePort {
            get {
                int port = -1;

                int tries = 0;
                while ( tries++ < PortRange ) {
                    int tmp = Port + tries;
                    if ( _usedPorts.Any( p => p == tmp ) )
                        continue;

                    port = tmp;
                    _usedPorts.Add( port );
                    break;
                }

                if ( port <= 0 )
                    throw new Exception( $"There are no more available ports in this portrange {Port}-{Port + PortRange}({PortRange})" );

                return port;
            }
            set => _usedPorts.Remove( value );
        }

        #endregion
        #region Private Variables

        public Socket Socket;

        #endregion
        #region Properties

        /// <summary>Whether the <see cref="UdpServer"/> is currently listening for incoming <see cref="Packet"/>s.</summary>
        public bool IsActive { get; private set; }
        /// <summary>The <see cref="EndPoint"/> that the server is bound to.</summary>
        public IPEndPoint LocalEndPoint { get { try { return Socket.LocalEndPoint.ToIPEndPoint(); } catch ( Exception ) { return null; } } }
        /// <summary>Whether the <see cref="UdpServer"/> is in blocking mode.</summary>
        public bool IsBlocking { get { try { return Socket.Blocking; } catch ( Exception ) { return false; } } }

        #endregion

        #endregion

        #region Constructors

        /// <summary>Creates a new instance of the <see cref="UdpServer"/> class.</summary>
        public UdpServer() => Socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to all available <see cref="IPAddress"/>es and the given and port.
        /// </summary>
        /// <param name="port">The port to bind the server on</param>
        public UdpServer( int port ) {
            Socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            Bind( new IPEndPoint( IPAddress.Any, port ) );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to the given hostname and port.
        /// </summary>
        /// <param name="hostname">The hostname to bind the server to</param>
        /// <param name="port">The port to bind the server on</param>
        public UdpServer( string hostname, int port ) {
            Socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            Bind( hostname, port );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to the given <see cref="IPAddress"/> and port.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> to bind the server to</param>
        /// <param name="port">The port to bind the server on</param>
        public UdpServer( IPAddress hostIP, int port ) {
            Socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            Bind( new IPEndPoint( hostIP, port ) );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpServer"/> class and binds it to the given <see cref="EndPoint"/>.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> to bind the server to</param>
        public UdpServer( EndPoint endPoint ) {
            Socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            Bind( endPoint );
        }

        #endregion

        #region Methods

        public void InvokeOnDataReceived( UdpSocket socket, Packet packet, IPEndPoint endPoint ) => _onDataReceived?.Invoke( socket, packet, endPoint );

        #region Connection-Methods

        /// <summary>
        /// Binds the <see cref="UdpServer"/> to the given hostname and port.
        /// </summary>
        /// <param name="hostname">The hostname to bind to</param>
        /// <param name="port">The port to bind to</param>
        public void Bind( string hostname, int port ) {
            if ( string.IsNullOrWhiteSpace( hostname ) )
                throw new Exception( $"Cannot bind: hostname parameter is empty or null." );

            if ( port < 1 )
                throw new Exception( $"Cannot bind: port is number is invalid: \"{port}\"" );

            if ( !IPAddress.TryParse( hostname, out IPAddress ip ) )
                ip = Dns.GetHostAddresses( hostname )[ 0 ];
            if ( ip == null )
                throw new Exception( "Cannot bind: could not resolve hostname \"" + hostname + "\"" );

            Bind( new IPEndPoint( ip, port ) );
        }

        /// <summary>
        /// Binds the <see cref="UdpServer"/> to the given <see cref="IPAddress"/> and port.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> to bind to</param>
        /// <param name="port">The port to bind to</param>
        public void Bind( IPAddress hostIP, int port ) {
            if ( port < 1 )
                throw new Exception( $"Cannot bind: port is number is invalid: \"{port}\"" );

            if ( hostIP == null )
                throw new Exception( "Cannot bind: remote host IP parameter is null" );

            Bind( new IPEndPoint( hostIP, port ) );
        }

        /// <summary>
        /// Binds the <see cref="UdpServer"/> to the given <see cref="EndPoint"/>.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> to bind to</param>
        public void Bind( EndPoint endPoint ) {
            Port = endPoint.GetPort();
            PortRange = 50;
            Socket.Bind( endPoint );
            User.Listener = this;
        }

        /// <summary>
        /// Closes the <see cref="UdpServer"/> and releases all associated resources.
        /// </summary>
        public void Close() => Socket.Close();

        #endregion

        #region Trafficking-Methods

        public void SendTo( object value, IPEndPoint remoteHost ) => SendTo( new Packet( value ), remoteHost );
        public void SendTo( Packet packet, IPEndPoint remoteHost ) {
            if ( !UserList.Contains( remoteHost ) ) {
                Socket s = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
                //s.Bind( new IPEndPoint( IPAddress.Any, Port ) );
                //s.Connect( new IPEndPoint( remoteHost.Address, remoteHost.Port - 2 ) );
                for ( int addedPort = 0; addedPort < 3; addedPort++ )
                    s.SendTo( packet.Serialized, new IPEndPoint( remoteHost.Address, remoteHost.Port - addedPort ) );
                return;
            }

            UserList[ remoteHost ].Send( packet );
        }

        #region ReceiveFrom

        #region ReceiveFrom One Packet

        /// <summary>
        /// ReceiveFrom one <see cref="Packet"/> and store the remote host's connection info in the given <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable that will be filled with the received <see cref="Packet"/></param>
        /// <returns>The <see cref="IPEndPoint"/> of the remote host from which the received <see cref="Packet"/> came from</returns>
        public IPEndPoint ReceiveFrom( out Packet packet ) => ReceiveFrom( out packet, 4096 );
        /// <summary>
        /// ReceiveFrom one <see cref="Packet"/> and store the remote host's connection info in the given <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable that will be filled with the received <see cref="Packet"/></param>
        /// <param name="bufferSize">The maximum size of the <see cref="Packet"/> to receive in <see cref="byte"/>s</param>
        /// <returns>The <see cref="IPEndPoint"/> of the remote host from which the received <see cref="Packet"/> came from</returns>
        public IPEndPoint ReceiveFrom( out Packet packet, int bufferSize ) {
            byte[] buffer = new byte[ bufferSize ];
            EndPoint ep = new IPEndPoint( IPAddress.Any, 0 );
            // Wait for an incoming packet.
            int length = Socket.ReceiveFrom( buffer, ref ep );
            // Format the data and remove empty buffer, then convert it to a packet.
            packet = new Packet( buffer.ToList().GetRange( 0, length ) );
            IPEndPoint newClient = ep.ToIPEndPoint();

            // Fire the OnDataReceived event with the collected data.
            _onDataReceived?.Invoke( UserList[ newClient ]?.Socket, packet, newClient );

            // If the received packet is of type ping.
            if ( packet.Type.Name.ToLower() == "ping" )
                // Send a pong back.
                SendTo( packet.DeserializePacket<Ping>().ToPong(), newClient );

            bool userAdded = false;
            // If the packet is of type Login and the remote host's IPEndPoint has not been registered yet, create a new user.
            if ( packet.Type.Name.ToLower() == "login" ) {
                Login login = packet.DeserializePacket<Login>();
                if ( !UserList.Contains( newClient ) ) {
                    UserList.Add( login.Username, newClient );
                    userAdded = true;
                } else
                    // Change the user's username, which in turn fires the User.OnUsernameChanged event.
                    UserList[ newClient ].Username = login.Username;
            }

            if ( !UserList.Contains( newClient ) ) {
                if ( packet.Type.Name.ToLower() != "login" )
                    SendTo( REQUEST.Login, newClient );
                return newClient;
            }

            // If the user exists, (re)start it's MsSinceLastPing Stopwatch.
            UserList[ newClient ].ResetPingWatch();

            if ( userAdded )
                OnNewClientRequest?.Invoke( UserList[ newClient ] );

            return newClient;
        }

        /// <summary>
        /// ReceiveFrom one <see cref="Packet"/> and store the remote host's connection info in the given <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable that will be filled with the received <see cref="Packet"/></param>
        /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> of the remote host from which the received <see cref="Packet"/> came from</param>
        public void ReceiveFrom( out Packet packet, out IPEndPoint remoteEndPoint ) => ReceiveFrom( out packet, out remoteEndPoint, 4096 );
        /// <summary>
        /// ReceiveFrom one <see cref="Packet"/> and store the remote host's connection info in the given <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable that will be filled with the received <see cref="Packet"/></param>
        /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> of the remote host from which the received <see cref="Packet"/> came from</param>
        /// <param name="bufferSize">The maximum size of the <see cref="Packet"/> to receive in <see cref="byte"/>s</param>
        public void ReceiveFrom( out Packet packet, out IPEndPoint remoteEndPoint, int bufferSize ) => remoteEndPoint = ReceiveFrom( out packet, 4096 );

        #endregion

        #region Try Receiving One Packet

        /// <summary>
        /// Try to receive a packet from the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to fill if a <see cref="Packet"/> has arrived</param>
        /// <param name="endPoint">The remote <see cref="EndPoint"/> that sent the received <see cref="Packet"/></param>
        /// <returns>True if a packet has arrived, false if the packet was corrupted or was null</returns>
        public bool TryReceiveFrom( out Packet packet, out IPEndPoint endPoint ) => TryReceiveFrom( out packet, out endPoint, 4096 );
        /// <summary>
        /// Try to receive a packet from the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to fill if a <see cref="Packet"/> has arrived</param>
        /// <param name="endPoint">The remote <see cref="EndPoint"/> that sent the received <see cref="Packet"/></param>
        /// <param name="bufferSize">The maximum packet size to receive</param>
        /// <returns>True if a packet has arrived, false if the packet was corrupted or was null</returns>
        public bool TryReceiveFrom( out Packet packet, out IPEndPoint endPoint, int bufferSize ) {
            try {
                endPoint = ReceiveFrom( out packet, bufferSize );
                return packet != null;
            } catch ( Exception ) {
                packet = null;
                endPoint = null;
                return false;
            }
        }

        #endregion

        #region Start Receiving From With Events

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
                while ( IsActive )
                    // Wait to receive a packet from any client on the bound IP and Port.
                    TryReceiveFrom( out Packet p, out IPEndPoint ep, bufferSize );
            } );

            Task.Run( () => {
                while ( IsActive ) {
                    UserList.ToList().ForEach( u => {
                        if ( u.Connected && u.MsSinceLastPacket < 10000 )
                            return;
                        Console.WriteLine( $"{u} disconnected: timed out after {u.MsSinceLastPacket / 1000.0} seconds" );
                        UserList.Remove( u );
                    } );

                    Thread.Sleep( 100 );
                }
            } );
        }
        /// <summary>
        /// Stops listening for incoming packets from the remote host.
        /// </summary>
        public void StopReceiving() => IsActive = false;

        #endregion

        #endregion

        #endregion

        #endregion

    }

}
