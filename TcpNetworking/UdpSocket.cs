﻿using System;
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

    public delegate void DataEventHandler( UdpSocket socket, Packet packet );
    public delegate void SocketErrorEventHandler( UdpSocket socket, Exception ex );

    #endregion

    [Serializable]
    public class UdpSocket {

        #region Events

        /// <summary>Fires when the connection to the remote host is lost</summary>
        public event SocketErrorEventHandler OnConnectionLost;
        /// <summary>Fires when a <see cref="Packet"/> has been received from the remote host</summary>
        public static event DataEventHandler OnDataReceived;
        /// <summary>Fires when a <see cref="Packet"/> has been sent to the remote host</summary>
        public static event DataEventHandler OnDataSent;

        #endregion

        #region Local Variables

        #region Private Variables

        [NonSerialized] private Socket _sender;
        [NonSerialized] private Socket _receiver;
        [NonSerialized] private Stopwatch _pingWatch = new Stopwatch();
        [NonSerialized] private Stopwatch _stayAliveWatch = new Stopwatch();

        private IPEndPoint _receiveFromEP;
        private IPEndPoint _serverEP;

        #endregion

        #region PingPong

        /// <summary>This <see cref="Ping"/> variable is used to check if the remote host sends the correct <see cref="Pong"/> back.</summary>
        public Ping Ping;
        /// <summary>This <see cref="Pong"/> variable is used to check if the remote host sent the correct <see cref="Pong"/> back, in comparison to the <see cref="Ping"/> variable.</summary>
        public Pong Pong;

        #endregion

        #region Properties

        /// <summary>Gets the amount of milliseconds passed since the last <see cref="Ping"/> that this client sent. For server-sided intentions.</summary>
        public int MsSinceLastPing => ( int )_pingWatch.ElapsedMilliseconds;

        /// <summary>The local <see cref="IPEndPoint"/> of the remote host</summary>
        public IPEndPoint LocalEndPoint { get => _receiveFromEP; private set => _receiveFromEP = value; }
        /// <summary>The <see cref="IPEndPoint"/> reserved for receiving <see cref="Packet"/>s from the remote host</summary>
        public IPEndPoint RemoteEndPoint { get => _serverEP; private set => _serverEP = value; }
        /// <summary>Whether the <see cref="UdpSocket"/> blocked the sending of <see cref="Packet"/>s to the remote host</summary>
        public bool IsBlockingSend { get { try { return _sender.Blocking; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> blocked the receiving of <see cref="Packet"/>s from the remote host</summary>
        public bool IsBlockingReceive { get { try { return _receiver.Blocking; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> is listening for incoming <see cref="Packet"/>s</summary>
        public bool IsListening { get { try { return _receiver.IsBound; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> is connected to the remote host</summary>
        public bool Connected { get { try { return _sender.Connected; } catch ( Exception ) { return false; } } }

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        public UdpSocket() {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class and automatically connects it to the remote host.
        /// </summary>
        /// <param name="hostname">The hostname of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public UdpSocket( string hostname, int port ) {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            Connect( hostname, port );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class and automatically connects it to the remote host.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public UdpSocket( IPAddress hostIP, int port ) {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            Connect( new IPEndPoint( hostIP, port ) );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class and automatically connects it to the remote host.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> of the remote host to connect to</param>
        public UdpSocket( IPEndPoint endPoint ) {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );

            Connect( endPoint );
        }

        #endregion

        #region Methods

        #region Connection-Methods

        /// <summary>
        /// Base-Connects the underlying sending-<see cref="Socket"/> by using only the following: <seealso cref="Socket.Connect(EndPoint)"/>. Primarily only used server-sided.
        /// </summary>
        /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> of the remote host to connect to</param>
        public void ConnectSocket( IPEndPoint remoteEndPoint ) => _sender.Connect( remoteEndPoint );
        /// <summary>
        /// Base-Binds the underlying sending-<see cref="Socket"/> by using only the following: <seealso cref="Socket.Bind(EndPoint)"/>. Primarily only used server-sided.
        /// </summary>
        /// <param name="remoteEndPoint">The <see cref="IPEndPoint"/> of the remote host to connect to</param>
        public void BindSocket( IPEndPoint remoteEndPoint ) => _sender.Bind( remoteEndPoint );

        /// <summary>
        /// Establishes a connection to a remote host.
        /// </summary>
        /// <param name="hostname">The hostname of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public void Connect( string hostname, int port ) {
            if ( string.IsNullOrEmpty( hostname ) )
                throw new Exception( $"Could not connect, the hostname parameter is empty" );

            if ( port < 1 )
                throw new Exception( $"Could not connect, port is number is invalid: \"{port}\"" );

            if ( !IPAddress.TryParse( hostname, out IPAddress ip ) )
                ip = Dns.GetHostAddresses( hostname )[ 0 ];
            if ( ip == null )
                throw new Exception( "Could not resolve hostname \"" + hostname + "\"" );

            Connect( new IPEndPoint( ip, port ) );
        }
        /// <summary>
        /// Establishes a connection to a remote host.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public void Connect( IPAddress hostIP, int port ) {
            if ( port < 1 )
                throw new Exception( $"Could not connect, port is number is invalid: \"{port}\"" );

            if ( hostIP == null )
                throw new Exception( "Could not connect, IP is of type \"null\"" );

            Connect( new IPEndPoint( hostIP, port ) );
        }
        /// <summary>
        /// Establishes a connection to a remote host.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> of the remote host to connect to</param>
        public void Connect( IPEndPoint endPoint ) {
            RemoteEndPoint = endPoint;

            _receiver.SendTo( new Packet( GET.NewClientID ).Serialized, RemoteEndPoint );
            Console.WriteLine( $"SENT NEW CLIENT ID REQUEST TO {RemoteEndPoint}" );

            if ( !TryReceive( out Packet p ) ) {
                Console.WriteLine( $"RECV FOR NEW CLIENT ID FAILED" );
                return;
            }

            Console.WriteLine( $"RECV NEW CLIENT ID ({p.Content}) FROM {RemoteEndPoint}" );
            Send( p );
            Console.WriteLine( $"SENT NEW CLIENT ID BACK FOR CONFIRMATION TO {RemoteEndPoint}" );
        }

        /// <summary>
        /// Closes the <see cref="UdpSocket"/> and releases all associated resources.
        /// </summary>
        public void Close() => Close( true );

        /// <summary>
        /// Closes the <see cref="UdpSocket"/> and releases all associated resources.
        /// </summary>
        /// <param name="InvokeConnectionLost">Whether to invoke the <seealso cref="OnConnectionLost"/> event</param>
        public void Close( bool InvokeConnectionLost ) {
            _sender?.Close();
            _receiver?.Close();

            if ( InvokeConnectionLost )
                OnConnectionLost?.Invoke( this, null );
        }

        #endregion

        #region Trafficking-Methods

        /// <summary>
        /// Send any kind of <see cref="object"/> to the remote host.
        /// </summary>
        /// <param name="value">The <see cref="object"/> to send</param>
        public void Send( object value ) => Send( new Packet( value ) );
        /// <summary>
        /// Send a <see cref="Packet"/> to the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> to send</param>
        public void Send( Packet packet ) {
            if ( _sender == null )
                throw new Exception( $"Cannot send a packet when the UdpSocket is not connected." );

            _sender.SendTo( packet.Serialized, RemoteEndPoint );
            OnDataSent?.Invoke( this, packet );
        }

        /// <summary>Sends a <see cref="Ping"/> to the remote host, expecting a <see cref="Pong"/> with the same ID back.</summary>
        public void SendPing() {
            if ( Ping == null || Pong == null || Ping.ID == Pong.ID )
                Ping = new Ping();

            Send( Ping );
        }

        /// <summary>
        /// Sends a <see cref="Ping"/> to the remote host every 500 milliseconds to make sure that the connection stays alive.
        /// </summary>
        /// <returns>Whether the <see cref="Ping"/> was returned as a <see cref="Pong"/></returns>
        public bool StayAlive() => StayAlive( 10, 100, true );
        /// <summary>
        /// Sends a <see cref="Ping"/> to the remote host to make sure that the connection stays alive.
        /// </summary>
        /// <param name="tries">The amount of <see cref="Ping"/>s to send in total</param>
        /// <param name="delay">The amount of milliseconds to wait after every ping</param>
        /// <param name="blockSpam">Whether to prevent <see cref="Ping"/>-spamming (100ms minimal delay between unique <see cref="Ping"/>s)</param>
        /// <returns>Whether the <see cref="Ping"/> was returned as a <see cref="Pong"/></returns>
        public bool StayAlive( int tries, int delay, bool blockSpam ) {
            if ( blockSpam && _stayAliveWatch.ElapsedMilliseconds < 100 )
                return false;
            _stayAliveWatch.Restart();

            SendPing();

            Thread.Sleep( delay );

            if ( tries <= 0 )
                return false;
            if ( Pong != null && Ping.ID == Pong.ID )
                return true;

            return StayAlive( --tries, delay, false );
        }

        /// <summary>Resets the <seealso cref="MsSinceLastPing"/>'s time back to 0.</summary>
        public void ResetPingWatch() => _pingWatch.Restart();

        /// <summary>
        /// Start listening for incoming packets from the remote host, if found then call the <seealso cref="OnDataReceived"/> event.
        /// </summary>
        public void StartReceiving() => StartReceiving( 4096 );
        /// <summary>
        /// Start listening for incoming packets from the remote host, if found then call the <seealso cref="OnDataReceived"/> event.
        /// <param name="bufferSize">The maximum size of the buffer in <see cref="byte"/>s</param>
        /// </summary>
        public void StartReceiving( int bufferSize ) {
            Task.Run( () => {
                while ( _receiver != null ) {
                    if ( !TryReceive( out Packet p, bufferSize ) || p == null )
                        continue;

                    OnDataReceived?.Invoke( this, p );
                }
            } );
        }
        /// <summary>
        /// Stops listening for incoming packets from the remote host.
        /// </summary>
        public void StopReceiving() => _receiver = null;

        /// <summary>
        /// Try to receive a packet from the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to fill if a <see cref="Packet"/> has arrived</param>
        /// <returns>True if a packet has arrived, false if the packet was corrupted or was null</returns>
        public bool TryReceive( out Packet packet ) => TryReceive( out packet, 4096 );
        /// <summary>
        /// Try to receive a packet from the remote host.
        /// </summary>
        /// <param name="packet">The <see cref="Packet"/> variable to fill if a <see cref="Packet"/> has arrived</param>
        /// <param name="bufferSize">The maximum size of the buffer in <see cref="byte"/>s</param>
        /// <returns>True if a packet has arrived, false if the packet was corrupted or was null</returns>
        public bool TryReceive( out Packet packet, int bufferSize ) {
            packet = null;
            try {
                packet = Receive( bufferSize );
                return packet != null;
            } catch ( Exception ) { return false; }
        }

        /// <summary>
        /// Receive a packet from the remote host.
        /// </summary>
        /// <returns>The received <see cref="Packet"/></returns>
        public Packet Receive() => Receive( 4096 );
        /// <summary>
        /// Receive a packet from the remote host.
        /// </summary>
        /// <param name="bufferSize">The maximum size of the buffer in <see cref="byte"/>s</param>
        /// <returns>The received <see cref="Packet"/></returns>
        public Packet Receive( int bufferSize ) {
            byte[] buffer = new byte[ bufferSize ];
            int length = _receiver.Receive( buffer );
            Packet p = new Packet( buffer.ToList().GetRange( 0, length ) );

            if ( p.Type.Name.ToLower() == "pong" )
                Pong = p.DeserializePacket<Pong>();
            
            return p;
        }

        #endregion

        #endregion

    }
}
