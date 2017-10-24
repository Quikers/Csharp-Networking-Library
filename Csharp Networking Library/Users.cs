using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Net;

namespace Networking {

    [Serializable]
    public class User {

        #region Local Variables

        public int ID;
        public string Username;
        [NonSerialized] public TcpSocket TcpConnectionInfo;
        [NonSerialized] public UdpSocket UdpConnectionInfo;

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
        public User( TcpSocket tcpConnectionInfo ) {
            ID = -1;
            Username = "UNKNOWN";
            TcpConnectionInfo = tcpConnectionInfo;
        }
        public User( UdpSocket udpConnectionInfo ) {
            ID = -1;
            Username = "UNKNOWN";
            UdpConnectionInfo = udpConnectionInfo;
        }
        public User( int id, string username ) {
            ID = id;
            Username = username;
        }
        public User( string username, TcpSocket tcpConnectionInfo ) {
            ID = -1;
            Username = username;
            TcpConnectionInfo = tcpConnectionInfo;
        }
        public User( int id, string username, TcpSocket tcpConnectionInfo ) {
            ID = id;
            Username = username;
            TcpConnectionInfo = tcpConnectionInfo;
        }
        public User( string username, UdpSocket udpConnectionInfo ) {
            ID = -1;
            Username = username;
            UdpConnectionInfo = udpConnectionInfo;
        }
        public User( int id, string username, UdpSocket udpConnectionInfo ) {
            ID = id;
            Username = username;
            UdpConnectionInfo = udpConnectionInfo;
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

        #endregion

        #region Indexer Properties

        public User this[ User user ] { get { try { return _userList.First( u => u == user ); } catch ( Exception ) { return null; } } }
        public User this[ int id ] { get { try { return _userList.First( u => u.ID == id ); } catch ( Exception ) { return null; } } }
        public User this[ string username ] { get { try { return _userList.First( u => u.Username.ToLower() == username.ToLower() ); } catch ( Exception ) { return null; } } }
        public User this[ TcpSocket connectionInfo ] { get { try { return _userList.First( u => u.TcpConnectionInfo == connectionInfo ); } catch ( Exception ) { return null; } } }
        public User this[ UdpSocket connectionInfo ] { get { try { return _userList.First( u => u.UdpConnectionInfo == connectionInfo ); } catch ( Exception ) { return null; } } }

        #endregion

        #region User-collection Manipulation Methods

        public void CreateUser( string username ) => CreateUser( username, null );
        public void CreateUser( string username, TcpSocket connectionInfo ) => AddUser( new User( username, connectionInfo ) );
        public void CreateTempUser( TcpSocket connectionInfo ) => AddUser( new User( connectionInfo ) );
        public void CreateTempUser( UdpSocket connectionInfo ) => AddUser( new User( connectionInfo ) );

        public void AddUser( User user ) {
            if ( Exists( user.Username ) || user.TcpConnectionInfo != null && Exists( user.TcpConnectionInfo ) )
                return;
            if ( user.ID >= 0 && Exists( user.ID ) ) {
                return;
            }

            Add( user );
        }

        public void RemoveUser( User user ) => Task.Run( () => {
            if ( !Exists( user ) )
                return;

            Remove( user );
        } ).Wait();

        public void RemoveUser( int id ) => Task.Run( () => {
            if ( !Exists( id ) )
                return;

            Remove( this[ id ] );
        } );
        public void RemoveUser( string username ) => Task.Run( () => {
            if ( !Exists( username ) )
                return;

            Remove( this[ username ] );
        } ).Wait();
        public void RemoveUser( TcpSocket connectionInfo ) => Task.Run( () => {
            if ( !Exists( connectionInfo ) )
                return;

            Remove( this[ connectionInfo ] );
        } ).Wait();
        public void RemoveUser( UdpSocket connectionInfo ) => Task.Run( () => {
            if ( !Exists( connectionInfo ) )
                return;

            Remove( this[ connectionInfo ] );
        } ).Wait();

        public void RemoveUserAt( int index ) => Task.Run( () => {
            if ( _userList[ index ] == null )
                return;

            RemoveAt( index );
        } ).Wait();

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
        public bool Exists( UdpSocket connectionInfo ) => this[ connectionInfo ] != null;

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
        public void ClearDisconnectedUsers() => ClearDisconnectedUsers( true, true );
        public void ClearDisconnectedUsers( bool checkTcpProtocol, bool checkUdpProtocol ) {
            if ( checkTcpProtocol )
                foreach ( User user in _userList.ToList().Where( u => u.TcpConnectionInfo == null || !u.TcpConnectionInfo.Connected ) )
                    RemoveUser( user );
            if ( checkUdpProtocol )
                foreach ( User user in _userList.ToList().Where( u => u.UdpConnectionInfo == null || !u.UdpConnectionInfo.Connected ) )
                    RemoveUser( user );
        }

        #region Don't touch or you will die

        public IEnumerator<User> GetEnumerator() {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion

        #endregion

    }

}