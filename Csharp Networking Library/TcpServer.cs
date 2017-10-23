using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Networking {

    public class TcpServer : TcpListener {

        #region Events

        public event TcpSocketEventHandler ClientConnectionRequested;

        #endregion

        #region Local Variables

        public EndPoint LocalEndPoint => LocalEndpoint;

        #endregion

        #region Constructors

        public TcpServer( int port, TcpSocketEventHandler callback ) : base( IPAddress.Any, port ) {
            ClientConnectionRequested += callback;
            BeginAccepting();
        }
        public TcpServer( IPAddress ip, int port, TcpSocketEventHandler callback ) : base( ip, port ) {
            ClientConnectionRequested += callback;
            BeginAccepting();
        }

        #endregion

        #region Methods

        private void BeginAccepting() {
            Start();
            BeginAcceptTcpClient( ar => {
                try {
                    TcpSocket socket = new TcpSocket( ( ( TcpListener )ar.AsyncState ).EndAcceptTcpClient( ar ) );
                    ClientConnectionRequested?.Invoke( socket );
                } catch ( ObjectDisposedException ) { /* Is caused when exitting the server while still listening for new clients */ }

                BeginAccepting();
            }, this );
        }

        #endregion

    }

}
