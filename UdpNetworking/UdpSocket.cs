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

    #region Event Handlers

    public delegate void DataEventHandler( UdpSocket socket, Packet packet, IPEndPoint endPoint );
    public delegate void SocketErrorEventHandler( User user, Exception ex );

    #endregion

    [Serializable]
    public class UdpSocket {

        #region Events

        /// <summary>
        /// Fires when a <see cref="Packet"/> has been received.
        /// </summary>
        public static event DataEventHandler OnDataReceived;
        /// <summary>
        /// Fires when a <see cref="Packet"/> has been sent.
        /// </summary>
        public static event DataEventHandler OnDataSent;

        #endregion

        #region Local Variables

        #region Private Variables

        [NonSerialized] private Socket _sender;
        [NonSerialized] private Socket _receiver;
        [NonSerialized] private Stopwatch _packetWatch = new Stopwatch();
        [NonSerialized] private Stopwatch _stayAliveWatch = new Stopwatch();

        private IPEndPoint _originalEP;
        private IPEndPoint _senderEP;
        private IPEndPoint _receiverEP;

        #endregion

        #region PingPong

        /// <summary>This <see cref="Ping"/> variable is used to check if the remote host sends the correct <see cref="Pong"/> back.</summary>
        public Ping Ping;
        /// <summary>This <see cref="Pong"/> variable is used to check if the remote host sent the correct <see cref="Pong"/> back, in comparison to the <see cref="Ping"/> variable.</summary>
        public Pong Pong;

        #endregion

        #region Properties

        /// <summary>The local <see cref="IPEndPoint"/> of the <see cref="UdpSocket"/>.</summary>
        public IPEndPoint LocalEndPoint { get => _receiverEP ?? _receiver?.LocalEndPoint?.ToIPEndPoint(); set => _receiverEP = value; }
        /// <summary>The current <see cref="IPEndPoint"/> that the <see cref="UdpSocket"/> is connected to.</summary>
        public IPEndPoint RemoteEndPoint { get => _senderEP ?? _sender?.RemoteEndPoint?.ToIPEndPoint(); set => _senderEP = value; }
        /// <summary>The <see cref="IPEndPoint"/> that the <see cref="UdpSocket"/> was originally connected to.</summary>
        public IPEndPoint ServerEndPoint { get => _originalEP ?? _sender?.RemoteEndPoint?.ToIPEndPoint(); set => _originalEP = value; }

        /// <summary>Whether the <see cref="UdpSocket"/> blocked the sending of <see cref="Packet"/>s to the remote host.</summary>
        public bool IsBlockingSend { get { try { return _sender.Blocking; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> blocked the receiving of <see cref="Packet"/>s from the remote host.</summary>
        public bool IsBlockingReceive { get { try { return _receiver.Blocking; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> is listening for incoming <see cref="Packet"/>s.</summary>
        public bool IsListening { get { try { return _receiver.IsBound; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> is connected to the remote host.</summary>
        public bool Connected { get { try { return _sender.Connected; } catch ( Exception ) { return false; } } }

        /// <summary>How many milliseconds it has been since this <see cref="UdpSocket"/>'s last received ping.</summary>
        public int MsSinceLastPacket => ( int )_packetWatch.ElapsedMilliseconds;

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new <see cref="UdpSocket"/>.
        /// </summary>
        public UdpSocket() {
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
        }
        /// <summary>
        /// Initializes a new <see cref="UdpSocket"/> and automatically searches for and connects to the given hostname and port.
        /// </summary>
        /// <param name="hostname">The name (ip or domainname) of the remote host to connect to</param>
        /// <param name="port">The port to connect to the remote host with</param>
        public UdpSocket( string hostname, int port ) => Connect( hostname, port );
        /// <summary>
        /// Initializes a new <see cref="UdpSocket"/> and automatically connects to the given <see cref="IPAddress"/> and port.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> of the remote host to connect to</param>
        /// <param name="port">The port to connect to the remote host with</param>
        public UdpSocket( IPAddress hostIP, int port ) => Connect( hostIP, port );
        /// <summary>
        /// Initializes a new <see cref="UdpSocket"/> and automatically connects to the given <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> of the remote host to connect to</param>
        public UdpSocket( IPEndPoint remoteEndPoint ) => Connect( remoteEndPoint );

        #endregion

        #region Methods

        #region Connection-Methods

        /// <summary>
        /// Establishes a connection to a remote host (is also able to connect to domainnames).
        /// </summary>
        /// <param name="hostname">The hostname of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public void Connect( string hostname, int port ) {
            if ( string.IsNullOrEmpty( hostname ) )
                throw new Exception( $"Cannot connect: hostname parameter is empty or null." );

            if ( port < 1 )
                throw new Exception( $"Cannot connect: port is number is invalid: \"{port}\"" );

            if ( !IPAddress.TryParse( hostname, out IPAddress ip ) )
                ip = Dns.GetHostAddresses( hostname )[ 0 ];
            if ( ip == null )
                throw new Exception( "Cannot connect: could not resolve hostname \"" + hostname + "\"" );

            Connect( new IPEndPoint( ip, port ) );
        }
        /// <summary>
        /// Establishes a connection to a remote host.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public void Connect( IPAddress hostIP, int port ) {
            if ( port < 1 )
                throw new Exception( $"Cannot connect: port is number is invalid: \"{port}\"" );

            if ( hostIP == null )
                throw new Exception( "Cannot connect: remote host IP parameter is null" );

            Connect( new IPEndPoint( hostIP, port ) );
        }

        /// <summary>
        /// Establishes a connection to a remote host.
        /// </summary>
        /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> of the remote host to connect to</param>
        public void Connect( IPEndPoint remoteEndPoint ) => Connect( remoteEndPoint, false );

        /// <summary>
        /// Establishes a connection to a remote host.
        /// </summary>
        /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> of the remote host to connect to</param>
        /// <param name="serverSided">Whether to make this <see cref="UdpSocket"/> a server-sided client</param>
        public void Connect( IPEndPoint remoteEndPoint, bool serverSided ) {
            if ( _receiver == null && _sender == null )
                Close();

            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            // Store the server's connection info for independent use.
            ServerEndPoint = remoteEndPoint;
            RemoteEndPoint = remoteEndPoint;
            _stayAliveWatch.Restart();

            // If only the sending-socket should be connected (very basic connect function).
            if ( serverSided ) {
                // Connect and return method.
                _sender.Bind( new IPEndPoint( IPAddress.Any, RemoteEndPoint.Address.IsLocal() ? 0 : UdpServer.Port ) );
                _sender.Connect( RemoteEndPoint );
                return;
            }

            Packet p = null;
            // For as long as a new ID has not been received, keep trying to get one from the server.
            while ( p == null || p.Type.Name.ToLower() != "newid" || !p.TryDeserializePacket( out NewID newID ) ) {
                // Send a new connection request to the server with the receiver socket.
                // This opens a port in your NAT which we will utilize to setup a server without port-forwarding.
                Packet sendLogin = new Packet( new Login( "Quikers" ) );
                _receiver.SendTo( sendLogin.Serialized, RemoteEndPoint );
                OnDataSent?.Invoke( this, sendLogin, RemoteEndPoint );

                // Wait to receive a message back from the server containing a newly assigned, unique ID.
                // This ID will be used to register our receiver and sender socket under the same ID on the server.
                ReceiveFrom( out p );
            }
            
            // Send the newly received ID back to the server so that the server knows that this sender-socket belongs together with our receiver-socket.
            //RemoteEndPoint = new IPEndPoint( RemoteEndPoint.Address, p.DeserializePacket<NewID>().ServerPort );
            SendTo( p, new IPEndPoint( RemoteEndPoint.Address, p.DeserializePacket<NewID>().ServerPort ) );
        }

        /// <summary>
        /// Binds a <see cref="UdpSocket"/>'s receiver to an <see cref="IPEndPoint"/>.
        /// </summary>
        public void Bind( IPEndPoint remoteHost ) {
            _receiver.Bind( remoteHost );
        }

        /// <summary>
        /// Closes the connection to the remote host and stops listening for incoming <see cref="Packet"/>s.
        /// </summary>
        public void Close() {
            _sender.Close();
            _receiver.Close();

            _sender = null;
            _receiver = null;
        }

        #endregion

        #region Trafficking-Methods

        #region PingPong

        /// <summary>
        /// Sends a <see cref="Ping"/> to the remote host, expecting a <see cref="Pong"/> with the same ID back.
        /// </summary>
        public bool SendPing() => SendPingTo( RemoteEndPoint );
        public bool SendPingTo( IPEndPoint remoteEndPoint ) {
            // If no ping has been sent or if the last sent ping and received pong have the same ID
            if ( Ping == null || Ping.ID == Pong.ID )
                // Add a new ping, else send the old ping again
                Ping = new Ping();

            // Send the handled ping
            Ping.MsCounter.Restart();
            SendTo( Ping, remoteEndPoint );

            Task<bool> task = new Task<bool>( () => {
                Thread.Sleep( 10 );
                if ( Pong != null && Ping.ID == Pong.ID ) {
                    return true;
                }
                Connect( RemoteEndPoint );
                return false;
            } );
            task.Start();
            task.Wait();

            return task.Result;
        }

        /// <summary>
        /// Sends a <see cref="Ping"/> to the remote host 10 times in 1 second to make sure that the connection stays alive.
        /// </summary>
        /// <returns>Whether the <see cref="Ping.ID"/> was returned as a <see cref="Pong.ID"/></returns>
        public bool StayAlive() => StayAlive( 10, 1000, true );
        /// <summary>
        /// Sends a <see cref="Ping"/> to the remote host a given amount of tries with each having a delay of a given value in milliseconds to make sure that the connection stays alive.
        /// </summary>
        /// <param name="tries">The amount of <see cref="Ping"/>s to send in total</param>
        /// <returns>Whether the <see cref="Ping.ID"/> was returned as a <see cref="Pong.ID"/></returns>
        public bool StayAlive( int tries ) => StayAlive( tries, 1000, true );
        /// <summary>
        /// Sends a <see cref="Ping"/> to the remote host a given amount of tries with each having a delay of a given value in milliseconds to make sure that the connection stays alive.
        /// </summary>
        /// <param name="tries">The amount of <see cref="Ping"/>s to send in total</param>
        /// <param name="delay">The amount of milliseconds to wait after every ping</param>
        /// <returns>Whether the <see cref="Ping.ID"/> was returned as a <see cref="Pong.ID"/></returns>
        public bool StayAlive( int tries, int delay ) => StayAlive( tries, delay, true );
        /// <summary>
        /// Sends a <see cref="Ping"/> to the remote host a given amount of tries with each having a delay of a given value in milliseconds to make sure that the connection stays alive.
        /// </summary>
        /// <param name="tries">The amount of <see cref="Ping"/>s to send in total</param>
        /// <param name="delay">The amount of milliseconds to wait after every ping</param>
        /// <param name="blockSpam">Whether to prevent <see cref="Ping"/>-spamming (100ms minimal delay between unique <see cref="Ping"/>s)</param>
        /// <returns>Whether the <see cref="Ping.ID"/> was returned as a <see cref="Pong.ID"/></returns>
        private bool StayAlive( int tries, int delay, bool blockSpam ) {
            while ( tries-- > 0 ) {
                if ( blockSpam && _stayAliveWatch.ElapsedMilliseconds < 100 || _stayAliveWatch.ElapsedMilliseconds < delay )
                    Thread.Sleep( delay - (int)_stayAliveWatch.ElapsedMilliseconds );
                _stayAliveWatch.Restart();

                if ( SendPing() )
                    return true;

                blockSpam = false;
            }
            return false;
        }

        /// <summary>
        /// Resets the <seealso cref="MsSinceLastPacket"/>'s time back to 0.
        /// </summary>
        public void ResetPingWatch() => _packetWatch.Restart();

        #endregion

        #region Send

        /// <summary>
        /// Sends an <see cref="object"/> to the remote host.
        /// </summary>
        /// <param name="value">The <see cref="object"/> to send</param>
        public void Send( object value ) => Send( new Packet( value ) );
        /// <summary>
        /// Sends a <see cref="Packet"/> to the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> to send</param>
        public void Send( Packet packet ) => SendTo( packet, RemoteEndPoint );

        /// <summary>
        /// Sends an <see cref="object"/> to the given <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="value">The <see cref="object"/> to send</param>
        /// <param name="remoteHost">The <see cref="IPEndPoint"/> to send the <see cref="object"/> to</param>
        public void SendTo( object value, IPEndPoint remoteHost ) => SendTo( new Packet( value ), remoteHost );
        /// <summary>
        /// Sends a <see cref="Packet"/> to the given <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> to send</param>
        /// <param name="remoteHost">The <see cref="IPEndPoint"/> to send the <see cref="Packet"/> to</param>
        public void SendTo( Packet packet, IPEndPoint remoteHost ) {
            _sender.SendTo( packet.Serialized, remoteHost );

            OnDataSent?.Invoke( this, packet, remoteHost );
        }

        #endregion
        #region ReceiveFrom

        #region ReceiveFrom One Packet

        /// <summary>
        /// Receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> of the remote host that sent the <see cref="Packet"/></param>
        public void ReceiveFrom( out Packet packet, out IPEndPoint endPoint ) => endPoint = ReceiveFrom( out packet, 4096 );
        /// <summary>
        /// Receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <param name="bufferSize">The maximum size of the <see cref="Packet"/> to receive in <see cref="byte"/>s</param>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> of the remote host that sent the <see cref="Packet"/></param>
        public void ReceiveFrom( out Packet packet, out IPEndPoint endPoint, int bufferSize ) => endPoint = ReceiveFrom( out packet, bufferSize );
        /// <summary>
        /// Receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <returns>The <see cref="IPEndPoint"/> of the remote host that sent the <see cref="Packet"/></returns>
        public IPEndPoint ReceiveFrom( out Packet packet ) => ReceiveFrom( out packet, 4096 );
        /// <summary>
        /// Receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <param name="bufferSize">The maximum size of the <see cref="Packet"/> to receive in <see cref="byte"/>s</param>
        /// <returns>The <see cref="IPEndPoint"/> of the remote host that sent the <see cref="Packet"/></returns>
        public IPEndPoint ReceiveFrom( out Packet packet, int bufferSize ) {
            IPEndPoint ep = ReceiveFromBasic( out packet, bufferSize );
            if ( packet == null )
                return ep;
            OnDataReceived?.Invoke( this, packet, RemoteEndPoint );
            
            if ( packet.TypeName == "request" && packet.DeserializePacket<REQUEST>() == REQUEST.Login )
                Connect( ServerEndPoint );
            switch ( packet.Type.Name.ToLower() ) {
                default: /* Ignored */ break;
                case "ping":
                    Send( packet.DeserializePacket<Ping>().ToPong() );
                    break;
                case "pong":
                    Ping.MarkTime();
                    Pong = packet.DeserializePacket<Pong>();
                    break;
            }
            
            ResetPingWatch();
            
            // Return the IPEndPoint.
            return ep.ToIPEndPoint();
        }

        private Timer t;

        public IPEndPoint ReceiveFromBasic( out Packet packet ) => ReceiveFromBasic( out packet, 4096 );
        public IPEndPoint ReceiveFromBasic( out Packet packet, int bufferSize ) {
            // Add a buffer to store the packet in.
            byte[] buffer = new byte[ bufferSize ];
            EndPoint ep = new IPEndPoint( IPAddress.Any, 0 );
            try {
                // ReceiveFrom a buffer of data and store the length for data formatting.
                int length = _receiver.ReceiveFrom( buffer, ref ep );
                // Add a new packet out of the newly received and formatted data.
                packet = new Packet( buffer.ToList().GetRange( 0, length ) );
            } catch ( SocketException ex ) {
                if ( !ex.ToString().Contains( "WSACancelBlockingCall" ) )
                    Console.WriteLine( ex );

                packet = null;
                return null;
            }

            return ep.ToIPEndPoint();
        }

        #endregion
        #region Try Receiving One Packet

        /// <summary>
        /// Try to receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <returns>Whether the receiving of a new <see cref="Packet"/> was successful</returns>
        public bool TryReceiveFrom( out Packet packet ) => TryReceiveFrom( out packet, out IPEndPoint endPoint, 4096 );
        /// <summary>
        /// Try to receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <param name="bufferSize">The maximum size of the <see cref="Packet"/> to receive in <see cref="byte"/>s</param>
        /// <returns>Whether the receiving of a new <see cref="Packet"/> was successful</returns>
        public bool TryReceiveFrom( out Packet packet, int bufferSize ) => TryReceiveFrom( out packet, out IPEndPoint endPoint, bufferSize );
        /// <summary>
        /// Try to receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> of the remote host that sent the <see cref="Packet"/></param>
        /// <returns>Whether the receiving of a new <see cref="Packet"/> was successful</returns>
        public bool TryReceiveFrom( out Packet packet, out IPEndPoint endPoint ) => TryReceiveFrom( out packet, out endPoint, 4096 );
        /// <summary>
        /// Try to receive one <see cref="Packet"/>. 
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to store the received <see cref="Packet"/> in</param>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> of the remote host that sent the <see cref="Packet"/></param>
        /// <param name="bufferSize">The maximum size of the <see cref="Packet"/> to receive in <see cref="byte"/>s</param>
        /// <returns>Whether the receiving of a new <see cref="Packet"/> was successful</returns>
        public bool TryReceiveFrom( out Packet packet, out IPEndPoint endPoint, int bufferSize ) {
            try {
                ReceiveFrom( out packet, out endPoint, bufferSize );
                return packet != null;
            } catch ( Exception ) {
                packet = null;
                endPoint = null;
                return false;
            }
        }
        #endregion
        #region Start Receiving With Events

        /// <summary>
        /// Start waiting to receive <see cref="Packet"/>s from the remote host and calls <seealso cref="OnDataReceived"/> if one is received.
        /// </summary>
        public void StartReceiving() => StartReceiving( 4096 );
        /// <summary>
        /// Start waiting to receive <see cref="Packet"/>s from the remote host and calls <seealso cref="OnDataReceived"/> if one is received.
        /// </summary>
        /// <param name="bufferSize">The maximum size of the <see cref="Packet"/> to receive in <see cref="byte"/>s</param>
        public void StartReceiving( int bufferSize ) => Task.Run( () => {
            // While the receiver is active.
            while ( _receiver != null )
                // Try to receive a packet.
                TryReceiveFrom( out Packet _, bufferSize );
        } );

        /// <summary>
        /// Stops the <see cref="UdpSocket"/> from receiving any further <see cref="Packet"/>s.
        /// </summary>
        public void StopReceiving() {
            _receiver.Close();
            _receiver = null;
        }

        #endregion

        #endregion

        #endregion

        #endregion

    }
}
