using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UdpNetworking {

    #region Event Handlers

    public delegate void DataEventHandler( UdpSocket socket, Packet packet );
    public delegate void SocketErrorEventHandler( UdpSocket socket, Exception ex );

    #endregion

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

        private Socket _sender;
        private Socket _receiver;
        private Stopwatch _pingWatch = new Stopwatch();

        /// <summary>This <see cref="Ping"/> variable is used to check if the remote host sends the correct <see cref="Pong"/> back.</summary>
        public Ping Ping;
        /// <summary>This <see cref="Pong"/> variable is used to check if the remote host sent the correct <see cref="Pong"/> back, in comparison to the <see cref="Ping"/> variable.</summary>
        public Pong Pong;

        /// <summary>Gets the amount of milliseconds passed since the last <see cref="Ping"/> that this client sent. For server-sided intentions.</summary>
        public int MsSinceLastPing => (int)_pingWatch.ElapsedMilliseconds;

        /// <summary>The <see cref="EndPoint"/> of the remote host</summary>
        public EndPoint LocalEndPoint { get { try { return _sender.LocalEndPoint; } catch ( Exception ) { return null; } } }
        /// <summary>The <see cref="EndPoint"/> reserved for receiving <see cref="Packet"/>s from the remote host</summary>
        public EndPoint RemoteEndPoint { get { try { return _sender.RemoteEndPoint; } catch ( Exception ) { return null; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> blocked the sending of <see cref="Packet"/>s to the remote host</summary>
        public bool IsBlockingSend { get { try { return _sender.Blocking; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> blocked the receiving of <see cref="Packet"/>s from the remote host</summary>
        public bool IsBlockingReceive { get { try { return _receiver.Blocking; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> is listening for incoming <see cref="Packet"/>s</summary>
        public bool IsListening { get { try { return _receiver.IsBound; } catch ( Exception ) { return false; } } }
        /// <summary>Whether the <see cref="UdpSocket"/> is connected to the remote host</summary>
        public bool Connected { get { try { return _sender.Connected; } catch ( Exception ) { return false; } } }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        public UdpSocket() {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        /// <param name="hostname">The hostname of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public UdpSocket( string hostname, int port ) {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

            Connect( hostname, port );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        /// <param name="hostIP">The <see cref="IPAddress"/> of the remote host to connect to</param>
        /// <param name="port">The port on the remote host to connect to</param>
        public UdpSocket( IPAddress hostIP, int port ) {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

            Connect( new IPEndPoint( hostIP, port ) );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="UdpSocket"/> class.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> of the remote host to connect to</param>
        public UdpSocket( EndPoint endPoint ) {
            _sender = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };
            _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { EnableBroadcast = true, ExclusiveAddressUse = false };

            Connect( endPoint );
        }

        #endregion

        #region Methods

        #region Connection-Methods

        /// <summary>
        /// Establishes a connection to a remote host and starts listening for incoming messages.
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
        /// Establishes a connection to a remote host and starts listening for incoming messages.
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
        /// Establishes a connection to a remote host and starts listening for incoming messages.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> of the remote host to connect to</param>
        public void Connect( EndPoint endPoint ) {
            _sender.Connect( endPoint );
            _receiver.Bind( _sender.LocalEndPoint );

            _pingWatch.Restart();
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
            _sender.Close();
            _receiver.Close();

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

            _sender.Send( packet.Serialized );
            OnDataSent?.Invoke( this, packet );
        }

        /// <summary>Sends a <see cref="Ping"/> to the remote host, expecting a <see cref="Pong"/> with the same ID back.</summary>
        public void SendPing() {
            if ( Ping != null && Pong != null && Ping.ID == Pong.ID )
                Ping = new Ping();

            Send( Ping );
        }

        /// <summary>Resets the <seealso cref="MsSinceLastPing"/>'s time back to 0.</summary>
        public void ResetPingWatch() => _pingWatch.Restart();

        /// <summary>Start listening for incoming packets from the remote host, if found then call the <seealso cref="OnDataReceived"/> event.</summary>
        public void StartReceiving() {
            Task.Run( () => {
                while ( _receiver != null ) {
                    if ( !TryReceiveOnce( out Packet p ) || p == null )
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
        public bool TryReceiveOnce( out Packet packet ) {
            packet = null;
            try {
                packet = ReceiveOnce();
                return packet != null;
            } catch ( Exception ) { return false; }
        }
        /// <summary>
        /// Receive a packet from the remote host.
        /// </summary>
        /// <returns>The received <see cref="Packet"/></returns>
        public Packet ReceiveOnce() {
            if ( _receiver == null )
                throw new Exception( $"Cannot receive a packet when the UdpSocket is not listening for packets." );

            byte[] buffer = new byte[ 4096 ];
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
