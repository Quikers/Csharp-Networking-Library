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
        [NonSerialized] public TcpSocket TcpSocket;

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
        public User( TcpSocket tcpSocket ) {
            ID = -1;
            Username = "UNKNOWN";
            TcpSocket = tcpSocket;
        }
        public User( int id, string username ) {
            ID = id;
            Username = username;
        }
        public User( string username, TcpSocket tcpSocket ) {
            ID = -1;
            Username = username;
            TcpSocket = tcpSocket;
        }
        public User( int id, string username, TcpSocket tcpSocket ) {
            ID = id;
            Username = username;
            TcpSocket = tcpSocket;
        }

        #endregion

        #region Methods



        #endregion

    }

    [Serializable]
    public class UserList : IEnumerable<User> {

        #region Events

        public delegate void UserEvent( User user );
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
        public User this[ TcpSocket connectionInfo ] { get { try { return _userList.First( u => u.TcpSocket == connectionInfo ); } catch ( Exception ) { return null; } } }

        #endregion

        #region User-collection Manipulation Methods

        public void CreateUser( string username ) => CreateUser( username, null );
        public void CreateUser( string username, TcpSocket connectionInfo ) => AddUser( new User( username, connectionInfo ) );
        public void CreateTempUser( TcpSocket connectionInfo ) => AddUser( new User( connectionInfo ) );

        public void AddUser( User user ) {
            if ( user.Username != "UNKNOWN" && Exists( user.Username ) || user.TcpSocket != null && Exists( user.TcpSocket ) )
                return;
            if ( user.ID >= 0 && Exists( user.ID ) ) {
                return;
            }

            Add( user );
        }

        public void RemoveUser( User user ) {
            if ( !Exists( user ) )
                return;

            Remove( user );
        }

        public void RemoveUser( int id ) {
            if ( !Exists( id ) )
                return;

            Remove( this[ id ] );
        }
        public void RemoveUser( string username ) {
            if ( !Exists( username ) )
                return;

            Remove( this[ username ] );
        }
        public void RemoveUser( TcpSocket connectionInfo ) {
            if ( !Exists( connectionInfo ) )
                return;

            Remove( this[ connectionInfo ] );
        }

        public void RemoveUserAt( int index ) {
            if ( _userList[ index ] == null )
                return;

            RemoveAt( index );
        }

        #region Overrides

        private void Add( User user ) {
            OnUserAdded?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Add( user );
        }

        private void Remove( User user ) {
            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.Remove( user );
        }

        private void RemoveAt( int index ) {
            User user = _userList[ index ];
            OnUserRemoved?.Invoke( user );
            OnUserListChanged?.Invoke( user );
            _userList.RemoveAt( index );
        }

        public void Clear() { _userList.Clear(); }

        #endregion

        #endregion

        #region Exists-methods

        public bool Exists( User user ) => this[ user ] != null;
        public bool Exists( int id ) => this[ id ] != null;
        public bool Exists( string username ) => this[ username ] != null;
        public bool Exists( TcpSocket connectionInfo ) => this[ connectionInfo ] != null;

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
                foreach ( User user in _userList.ToList().Where( u => u.TcpSocket == null || !u.TcpSocket.Connected ) )
                    RemoveUser( user );
        }

        #region Don't touch or you will die

        public IEnumerator<User> GetEnumerator() => _userList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #endregion

    }

}