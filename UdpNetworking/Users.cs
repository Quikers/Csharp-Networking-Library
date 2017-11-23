using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace UdpNetworking {

    [Serializable]
    public class User {

        #region Events

        public event UserEventHandler OnUsernameChanged;

        #endregion

        #region Local Variables

        #region Static Variables

        public static UdpServer Listener;

        #endregion
        #region Public Variables

        public string ID;
        public int SenderPort = -1;

        #endregion
        #region Private Variables

        private string _username;

        #endregion
        #region Properties

        /// <summary>Gets the username of the user, or sets it and fires the OnUsernameChanged event.</summary>
        public string Username {
            get => _username;
            set {
                if ( _username == value || string.IsNullOrWhiteSpace( value ) )
                    return;

                _username = value;
                OnUsernameChanged?.Invoke( this );
            }
        }
        /// <summary>Gets the underlying <see cref="UdpSocket"/>.</summary>
        public UdpSocket Socket { get; private set; }

        /// <summary>Whether the <see cref="User"/> is connected to the remote host.</summary>
        public bool Connected => Socket.Connected;
        /// <summary>How many milliseconds it has been since this <see cref="User"/>'s last received <see cref="Packet"/>.</summary>
        public int MsSinceLastPacket => Socket.MsSinceLastPacket;

        /// <summary>The <see cref="IPEndPoint"/> that the remote host is listening for <see cref="Packet"/>s on.</summary>
        public IPEndPoint ReceiverEndPoint { get { try { return Socket.RemoteEndPoint.ToIPEndPoint(); } catch ( Exception ) { return null; } } }
        /// <summary>The <see cref="IPEndPoint"/> that the remote host is using to send <see cref="Packet"/>s toward the server.</summary>
        public IPEndPoint SenderEndPoint { get { try { return SenderPort > 0 ? new IPEndPoint( ReceiverEndPoint.Address, SenderPort ) : null; } catch ( Exception ) { return null; } } }

        #endregion

        #endregion

        #region Constructors

        public User( IPEndPoint remoteHost ) {
            Task.Run( () => {
                Username = "UNKNOWN";
                SetSocket( remoteHost );

                Init();
            } ).Wait();
        }
        public User( string username, IPEndPoint remoteHost ) {
            Task.Run( () => {
                Username = username;
                SetSocket( remoteHost );

                Init();
            } ).Wait();
        }

        #endregion

        #region Methods

        #region Initialization

        private void Init() {
            // Create a temporary receiving socket on a different port than the main port
            Socket socket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
            IPEndPoint ep = new IPEndPoint( IPAddress.Any, UdpServer.AvailablePort );
            socket.Bind( ep );

            Packet p = null;
            EndPoint tmp = ep;
            byte[] buffer = new byte[ 4096 ];
            // For as long as the right ID has not been received, keep receiving data.
            while ( p == null || p.Type.Name.ToLower() != "newid" || p.DeserializePacket<NewID>().ID != ID ) {
                ID = Guid.NewGuid().ToString( "n" );
                Send( new NewID { ID = ID, ServerPort = ep.Port } );
                // Use a basic receive function without any packet-handling to receive the client's ID back on the sender-socket.
                int length = socket.ReceiveFrom( buffer, ref tmp );
                p = new Packet( buffer.ToList().GetRange( 0, length ) );
            }
            socket.Close();

            Listener.InvokeOnDataReceived( Socket, p, tmp.ToIPEndPoint() );

            // If successful, set the User's sender-port for future reference.
            SenderPort = tmp.ToIPEndPoint().Port;
        }

        public void SetSocket( IPEndPoint endPoint ) {
            Socket = new UdpSocket();
            Socket.Connect( endPoint, true );
        }

        #endregion
        #region Trafficking-Methods

        public void Send( object value ) => Socket.Send( value );
        public void Send( Packet packet ) => Socket.Send( packet );

        public IPEndPoint Receive( out Packet packet ) => Socket.ReceiveFrom( out packet );
        public IPEndPoint Receive( out Packet packet, int bufferSize ) => Socket.ReceiveFrom( out packet, bufferSize );

        #endregion

        public void RenewID() => Init();
        /// <summary>Resets the <seealso cref="UdpSocket.MsSinceLastPacket"/>'s time back to 0.</summary>
        public void ResetPingWatch() => Socket.ResetPingWatch();
        /// <summary>Converts the <see cref="User"/> instance into a string containing it's <seealso cref="Username"/>, <seealso cref="ID"/> and it's <seealso cref="ReceiverEndPoint"/> + <seealso cref="SenderPort"/>.</summary>
        /// <returns>The string containing the <see cref="User"/>'s information</returns>
        public override string ToString() => $"{Username}#{ID} ({ReceiverEndPoint} & {SenderPort})";

        #endregion

    }

    #region Event Handlers

    public delegate void UserEventHandler( User user );

    #endregion

    [Serializable]
    public class UserList : IEnumerable<User> {

        #region Events

        public event UserEventHandler OnUserListChanged;
        private event UserEventHandler _onUsernameChangedEvents;
        public event UserEventHandler OnUsernameChanged {
            add {
                _onUsernameChangedEvents += value;
                _userList.ToList().ForEach( u => u.OnUsernameChanged += value );
            }
            remove {
                _onUsernameChangedEvents -= value;
                _userList.ToList().ForEach( u => u.OnUsernameChanged -= value );
            }
        }
        public event UserEventHandler OnUserAdded;
        public event UserEventHandler OnUserRemoved;

        #endregion

        #region Local Variables

        private List<User> _userList = new List<User>();
        public int Count => _userList.Count;

        #endregion

        #region Indexer Properties

        public User this[ User user ] => _userList.FirstOrDefault( u => u == user );
        public User this[ string id ] => _userList.FirstOrDefault( u => u.ID == id );
        public User this[ EndPoint endPoint ] => _userList.FirstOrDefault( u => u.ReceiverEndPoint.ToString() == endPoint.ToString() || u.SenderEndPoint.ToString() == endPoint.ToString() );
        public User this[ UdpSocket socket ] => _userList.FirstOrDefault( u => u.Socket == socket );

        #endregion

        #region User Collection Manipulation Methods

        public void Add( IPEndPoint endPoint ) => Add( new User( endPoint ) );
        public void Add( string username, IPEndPoint endPoint ) => Add( new User( username, endPoint ) );
        public void Add( User user ) {
            if ( user.Username != "UNKNOWN" && Contains( user.Username ) || user.Socket != null && Contains( user.Socket ) )
                return;
            if ( !string.IsNullOrWhiteSpace( user.ID ) && Contains( user.ID ) )
                return;

            _userList.Add( user );

            OnUserAdded?.Invoke( this[ user ] );
            OnUserListChanged?.Invoke( this[ user ] );
            SetUserEvents( this[ user ] );
        }

        public void Remove( User user ) {
            if ( !Contains( user ) )
                return;
            
            UdpServer.AvailablePort = user.SenderPort;
            user.Socket.Close();

            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Remove( user );
        }
        public void Remove( string id ) {
            if ( !Contains( id ) )
                return;

            User user = this[ id ];
            UdpServer.AvailablePort = user.SenderPort;
            user.Socket.Close();

            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Remove( user );
        }
        public void Remove( UdpSocket socket ) {
            if ( !Contains( socket ) )
                return;

            User user = this[ socket ];
            UdpServer.AvailablePort = user.SenderPort;
            user.Socket.Close();

            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Remove( user );
        }

        public void RemoveAt( int index ) {
            if ( _userList[ index ] == null )
                return;

            User user = _userList[ index ];
            UdpServer.AvailablePort = user.SenderPort;
            user.Socket.Close();

            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.RemoveAt( index );
        }

        #region Overrides

        public void Clear() { _userList.Clear(); }

        #endregion

        #endregion

        #region Contains Methods

        public bool Contains( User user ) {
            return this[ user ] != null;
        }
        public bool Contains( string id ) => this[ id ] != null;
        public bool Contains( EndPoint endPoint ) => this[ endPoint ] != null;
        public bool Contains( UdpSocket socket ) => this[ socket ] != null;

        #endregion

        #region User Managing Methods

        /// <summary>
        /// Renews the ID of every <see cref="User"/> in the <see cref="UserList"/> (only use on server-side).
        /// </summary>
        public void RenewIDs() {
            foreach ( User user in _userList.ToList() )
                user.RenewID();
        }

        /// <summary>
        /// Removes all the <see cref="User"/>s currently not connected (only usable on server-side).
        /// </summary>
        public void ClearDisconnectedUsers() {
            foreach ( User user in _userList.ToList().Where( u => u.Socket == null || !u.Socket.Connected ) )
                Remove( user );
        }

        public void FromIEnumerable( IEnumerable<User> userList ) {
            _userList = userList.ToList();
            OnUserListChanged?.Invoke( null );
        }

        public IEnumerator<User> GetEnumerator() {
            return ( ( IEnumerable<User> )_userList ).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ( ( IEnumerable<User> )_userList ).GetEnumerator();
        }

        #endregion

        #region Private Methods

        private void SetUserEvents( User user ) {
            if ( _onUsernameChangedEvents == null || _onUsernameChangedEvents.GetInvocationList().Length <= 0 )
                return;

            foreach ( Delegate evnt in _onUsernameChangedEvents.GetInvocationList() ) {
                user.OnUsernameChanged += ( UserEventHandler )evnt;
            }
        }

        #endregion

    }

}