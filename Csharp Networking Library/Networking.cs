using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Networking {

    #region Event Handlers

    public delegate void DataEventHandler( UdpSocket socket, Packet packet );
    public delegate void TcpDataEventHandler( TcpSocket socket, Packet packet );

    #endregion

    #region DataClasses

    [Serializable]
    public class Ping {

        #region Variables

        public string ID;
        public bool Sent;
        public bool Received;

        #endregion

        #region Methods

        private string GenerateID() {
            return Guid.NewGuid().ToString( "N" );
        }

        #endregion

        #region Constructors

        public Ping() {
            ID = GenerateID();
            Sent = true;
        }

        #endregion

    }

    #endregion

}
