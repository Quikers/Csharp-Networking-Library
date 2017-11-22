using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace UdpNetworking {

    #region Extension Methods

    public static class ExtMethods {

        public static IPEndPoint ToIPEndPoint( this EndPoint endPoint ) {
            if ( endPoint == null )
                return null;

            string[] tmp = endPoint.ToString().Split( ':' );
            IPAddress ip = IPAddress.Parse( tmp[ 0 ] );
            int port = int.Parse( tmp[ 1 ] );

            return new IPEndPoint( ip, port );
        }

    }

    #endregion

    #region DataClasses

    [Serializable]
    public enum GET {
        NewClientID,
        UserList,

    }

    [Serializable]
    public sealed class Ping {

        #region Variables

        public string ID { get; private set; }

        #endregion

        #region Methods

        private string GenerateID() {
            return Guid.NewGuid().ToString( "N" );
        }

        public Pong ToPong() { return new Pong( ID ); }

        #endregion

        #region Constructors

        public Ping() => ID = GenerateID();

        #endregion

    }

    [Serializable]
    public sealed class Pong {

        #region Variables

        public string ID { get; private set; }

        #endregion

        #region Constructors

        public Pong( string pingID ) => ID = pingID;

        #endregion

    }

    [Serializable]
    public struct Login {
        public string Username;

        public Login( string username ) { Username = username; }
    }

    #endregion

}
