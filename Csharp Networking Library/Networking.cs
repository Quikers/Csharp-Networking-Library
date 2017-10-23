using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Networking {

    #region Event Handlers

    public delegate void UdpDataEventHandler( UdpSocket socket, Packet packet );
    public delegate void TcpDataEventHandler( TcpSocket socket, Packet packet );

    #endregion

    public class Server {

        #region Event Handlers

        public delegate void TcpServerUserEventHandler( User user );

        #endregion

        #region Events

        public event TcpServerUserEventHandler OnClientConnected;
        public event TcpServerUserEventHandler OnClientDisconnected;

        public event TcpDataEventHandler OnPacketReceived;

        private event TcpSocketEventHandler ClientDisconnected;
        private event TcpSocketErrorEventHandler ClientErrorDiscovered;

        #endregion

        #region Local Variables

        public TcpServer _listener { get; private set; }

        public IPAddress IP { get; private set; }
        public int Port { get; private set; }
        public bool ServerActive { get; private set; }
        public bool ReceivingFromAllClients { get; private set; }

        public UserList Clients { get => Data.UserList; private set => Data.UserList = value; }

        #endregion

        #region Constructors

        public Server() { }

        public Server( int port ) {
            IP = IPAddress.Any;
            Port = port;
        }

        public Server( IPAddress ip, int port ) {
            IP = ip;
            Port = port;
        }

        #endregion

        #region Event Callbacks

        private void ClientFoundCallback( TcpSocket client ) {
            client.ConnectionLost += ClientErrorDiscovered;

            if ( !Clients.Exists( client ) )
                Clients.CreateTempUser( client );

            OnClientConnected?.Invoke( Clients[ client ] );
        }

        private void ClientErrorDiscoveredCallback( TcpSocket client, Exception ex ) {
            // TODO: Fix error messaging (check when exactly client disconnects/crashes on the client side)
            ClientDisconnected?.Invoke( client );
        }

        private void ClientDisconnectedCallback( TcpSocket client ) {
            User u = Data.UserList[ client ] ?? new User( client );
            Clients.RemoveUser( client );

            OnClientDisconnected?.Invoke( u );
        }

        #endregion

        #region Methods

        #region Server Status Methods

        public void StartListening() {
            if ( !_listener.Server.IsBound )
                _listener.Start();
        }
        public void StopListening() {
            if ( _listener.Server.IsBound )
                _listener.Stop();
        }

        public void RestartServer() => RestartServer( IP, Port );
        public void RestartServer( IPAddress ip, int port ) {
            IP = ip ?? IPAddress.Any;
            Port = port > 0 ? port : 7777;
            Clients.Clear();
            ServerActive = true;

            _listener = new TcpServer( IP, Port, ClientFoundCallback );
            ClientErrorDiscovered += ClientErrorDiscoveredCallback;
            ClientDisconnected += ClientDisconnectedCallback;
        }

        // DIFFERENCE IS THAT "StartServer()" CHECKS IF THE SERVER IS ALREADY ON, AND IF SO DOES ABSOLUTELY NOTHING.

        public void StartServer() => StartServer( IP, Port );
        public void StartServer( IPAddress ip, int port ) {
            if ( _listener != null && _listener.Server.IsBound )
                return;

            RestartServer( ip, port );
        }

        public void StopServer() {
            _listener.Stop();

            while ( Clients.Where( c => c != null && c.ConnectionInfo.Connected ).ToList().Count > 0 )
                foreach ( User user in Clients )
                    user.ConnectionInfo.Close();

            Clients.Clear();
            ServerActive = false;
        }

        #endregion

        #region Packet Traffic Methods

        public void Send( object obj, TcpSocket client ) => Send( new Packet( obj ), client );
        public void Send( Packet packet, TcpSocket client ) {
            if ( client != null && client.Connected )
                client.Send( packet );
            else
                ClientDisconnected?.Invoke( client );
        }

        public void Broadcast( object obj ) => Broadcast( new Packet( obj ) );
        public void Broadcast( Packet packet ) {
            Task.Run( () => {
                foreach ( User user in Clients.Where( c => c?.ConnectionInfo != null && c.ConnectionInfo.Connected ) )
                    Send( packet, user.ConnectionInfo );
            } );
        }

        #region Old unused shit

        public void StopReceivingFromAllClients() => ReceivingFromAllClients = false;

        public void ReceiveFromAllClients() {
            Task.Run( () => {
                ReceivingFromAllClients = true;
                while ( ServerActive && ReceivingFromAllClients ) {
                    foreach ( User user in Clients.Where( c => c != null && c.ConnectionInfo.Connected ) ) {
                        if ( !ServerActive || !ReceivingFromAllClients )
                            return;

                        try {
                            OnPacketReceived?.Invoke( user.ConnectionInfo, user.ConnectionInfo.ReceiveOnce() );
                        } catch ( Exception ex ) {
                            ClientErrorDiscovered?.Invoke( user.ConnectionInfo, ex );
                        }
                    }
                }
            } );
        }

        #endregion

        #endregion

        #endregion

    }

}
