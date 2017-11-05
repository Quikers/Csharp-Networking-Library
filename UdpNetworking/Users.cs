using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Net;

namespace UdpNetworking {

    [Serializable]
    public class User {

        #region Local Variables

        public int ID;
        public string Username;
        [NonSerialized] public UdpSocket Socket;

        #endregion

        #region Constructors

        public User() {
            ID = -1;
            Username = "UNKNOWN";
        }
        public User( int id ) {
            ID = id;
            Username = "UNKNOWN";
        }
        public User( string username ) {
            ID = -1;
            Username = username;
        }
        public User( EndPoint endPoint ) {
            ID = -1;
            Username = "UNKNOWN";
            Socket = new UdpSocket( endPoint );
        }
        public User( UdpSocket socket ) {
            ID = -1;
            Username = "UNKNOWN";
            Socket = socket;
        }
        public User( int id, string username ) {
            ID = id;
            Username = username;
        }
        public User( string username, EndPoint endPoint ) {
            ID = -1;
            Username = username;
            Socket = new UdpSocket( endPoint );
        }
        public User( string username, UdpSocket socket ) {
            ID = -1;
            Username = username;
            Socket = socket;
        }
        public User( int id, string username, EndPoint endPoint ) {
            ID = id;
            Username = username;
            Socket = new UdpSocket( endPoint );
        }
        public User( int id, string username, UdpSocket socket ) {
            ID = id;
            Username = username;
            Socket = socket;
        }

        #endregion

        #region Methods



        #endregion

    }

    [Serializable]
    public class UserList : IEnumerable<User> {

        #region Event Handlers

        public delegate void UserEvent( User user );

        #endregion

        #region Events

        public event UserEvent OnUserListChanged;
        public event UserEvent OnUserAdded;
        public event UserEvent OnUserRemoved;

        #endregion

        #region Local Variables

        private List<User> _userList = new List<User>();
        private int _idAutoIncrement;

        public int Count => _userList.Count;

        #endregion

        #region Indexer Properties

        public User this[ User user ] { get { try { return _userList.First( u => u == user ); } catch ( Exception ) { return null; } } }
        public User this[ int id ] { get { try { return _userList.First( u => u.ID == id ); } catch ( Exception ) { return null; } } }
        public User this[ string username ] { get { try { return _userList.First( u => u.Username.ToLower() == username.ToLower() ); } catch ( Exception ) { return null; } } }
        public User this[ EndPoint endPoint ] { get { try { return _userList.First( u => u.Socket.RemoteEndPoint.ToString() == endPoint.ToString() ); } catch ( Exception ) { return null; } } }
        public User this[ UdpSocket socket ] { get { try { return _userList.First( u => u.Socket == socket ); } catch ( Exception ) { return null; } } }

        #endregion

        #region User-collection Manipulation Methods

        public void Create( string username ) => Create( username, null );
        public void Create( string username, UdpSocket socket ) => Add( new User( username, socket ) );
        public void Create( EndPoint endPoint ) => Add( new User( endPoint ) );
        public void Create( UdpSocket socket ) => Add( new User( socket ) );

        public void Add( EndPoint endPoint ) => Add( new User( endPoint ) );
        public void Add( UdpSocket socket ) => Add( new User( socket ) );
        public void Add( User user ) {
            if ( user.Username != "UNKNOWN" && Exists( user.Username ) || user.Socket != null && Exists( user.Socket ) )
                return;
            if ( user.ID >= 0 && Exists( user.ID ) )
                return;

            OnUserAdded?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Add( user );
        }

        public void Remove( User user ) {
            if ( !Exists( user ) )
                return;

            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Remove( user );
        }

        public void Remove( int id ) {
            if ( !Exists( id ) )
                return;

            OnUserRemoved?.Invoke( this[ id ] );
            OnUserListChanged?.Invoke( this[ id ] );
            _userList.Remove( this[ id ] );
        }
        public void Remove( string username ) {
            if ( !Exists( username ) )
                return;

            OnUserRemoved?.Invoke( this[ username ] );
            OnUserListChanged?.Invoke( this[ username ] );
            _userList.Remove( this[ username ] );
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

        #region Exists-methods

        public bool Exists( User user ) => this[ user ] != null;
        public bool Exists( int id ) => this[ id ] != null;
        public bool Exists( string username ) => this[ username ] != null;
        public bool Exists( EndPoint endPoint ) => this[ endPoint ] != null;
        public bool Exists( UdpSocket socket ) => this[ socket ] != null;

        #endregion

        #region User Managing Methods

        /// <summary>
        /// Assigns an ID to every <see cref="User"/> with an ID of less than 0.
        /// </summary>
        public void AssignIDs() {
            foreach ( User user in _userList.ToList().Where( u => u.ID < 0 ) )
                user.ID = _idAutoIncrement++;
        }

        /// <summary>
        /// Removes all the <see cref="User"/>s currently not connected (only usable on server-side).
        /// </summary>
        public void ClearDisconnectedUsers() {
            foreach ( User user in _userList.ToList().Where( u => u.Socket == null || !u.Socket.Connected ) )
                Remove( user );
        }

        public IEnumerator<User> GetEnumerator() {
            return ( ( IEnumerable<User> )_userList ).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ( ( IEnumerable<User> )_userList ).GetEnumerator();
        }

        #endregion

    }

}