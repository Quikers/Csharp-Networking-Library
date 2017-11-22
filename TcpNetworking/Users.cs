using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Net;

namespace UdpNetworking {

    [Serializable]
    public class User {

        #region Events

        public event UserEventHandler OnUsernameChanged;

        #endregion

        #region Local Variables

        public string ID => _ci.ID;
        private string _username;
        public string Username {
            get => _username;
            set {
                if ( _username == value || string.IsNullOrWhiteSpace( value ) )
                    return;

                _username = value;
                OnUsernameChanged?.Invoke( this );
            }
        }

        private ClientInfo _ci;
        public UdpSocket Socket {
            get { try { return _ci.Socket; } catch (Exception) { return null; } }
        }

        #endregion

        #region Constructors

        public User() => Username = "UNKNOWN";
        public User( string username ) => Username = username;
        public User( IPEndPoint endPoint ) {
            Username = "UNKNOWN";
            _ci.SetSocket( endPoint );
        }
        public User( string username, IPEndPoint endPoint ) {
            Username = username;
            _ci.SetSocket( endPoint );
        }

        private void Init( string username = "UNKNOWN", IPEndPoint endPoint = null ) {
            Username = username;

            if ( endPoint == null )
                return;

            _ci = new ClientInfo( endPoint );
        }

        #endregion

        #region Methods

        public void RenewID() => _ci.RenewID();

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

        public User this[ User user ] { get { try { return _userList.First( u => u == user ); } catch ( Exception ) { return null; } } }
        public User this[ string id ] { get { try { return _userList.First( u => u.ID == id ); } catch ( Exception ) { return null; } } }
        public User this[ EndPoint endPoint ] { get { try { return _userList.First( u => u.Socket.RemoteEndPoint.ToString() == endPoint.ToString() ); } catch ( Exception ) { return null; } } }
        public User this[ UdpSocket socket ] { get { try { return _userList.First( u => u.Socket == socket ); } catch ( Exception ) { return null; } } }

        #endregion

        #region User Collection Manipulation Methods

        public void Create( string username ) => Create( username, null );
        public void Create( string username, UdpSocket socket ) => Add( new User( username, socket.RemoteEndPoint ) );
        public void Create( IPEndPoint endPoint ) => Add( new User( endPoint ) );
        public void Create( UdpSocket socket ) => Add( new User( socket.RemoteEndPoint ) );

        public void Add( IPEndPoint endPoint ) => Add( new User( endPoint ) );
        public void Add( UdpSocket socket ) => Add( new User( socket.RemoteEndPoint ) );
        public void Add( User user ) {
            if ( user.Username != "UNKNOWN" && Exists( user.Username ) || user.Socket != null && Exists( user.Socket ) )
                return;
            if ( !string.IsNullOrWhiteSpace( user.ID ) && Exists( user.ID ) )
                return;

            _userList.Add( user );

            OnUserAdded?.Invoke( this[ user ] );
            OnUserListChanged?.Invoke( this[ user ] );
            SetUserEvents( this[ user ] );
        }

        public void Remove( User user ) {
            if ( !Exists( user ) )
                return;

            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Remove( user );
        }
        public void Remove( string id ) {
            if ( !Exists( id ) )
                return;

            OnUserRemoved?.Invoke( this[ id ] );
            OnUserListChanged?.Invoke( this[ id ] );
            _userList.Remove( this[ id ] );
        }
        public void Remove( UdpSocket socket ) {
            if ( !Exists( socket ) )
                return;

            OnUserRemoved?.Invoke( this[ socket ] );
            OnUserListChanged?.Invoke( this[ socket ] );
            _userList.Remove( this[ socket ] );
        }

        public void RemoveAt( int index ) {
            if ( _userList[ index ] == null )
                return;

            User user = _userList[ index ];
            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.RemoveAt( index );
        }

        #region Overrides

        public void Clear() { _userList.Clear(); }

        #endregion

        #endregion

        #region Exists Methods

        public bool Exists( User user ) => this[ user ] != null;
        public bool Exists( string id ) => this[ id ] != null;
        public bool Exists( EndPoint endPoint ) => this[ endPoint ] != null;
        public bool Exists( UdpSocket socket ) => this[ socket ] != null;

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
                user.OnUsernameChanged += (UserEventHandler)evnt;
            }
        }

        #endregion

    }

}