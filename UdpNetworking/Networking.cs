using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace UdpNetworking {

    #region DataClasses

    [ Serializable ]
    public enum GET {
        UserList,

    }

    [Serializable]
    public class Ping {

        #region Variables

        public string ID { get; private set; }

        #endregion

        #region Methods

        private string GenerateID() {
            return Guid.NewGuid().ToString( "N" );
        }

        #endregion

        #region Constructors

        public Ping() => ID = GenerateID();

        #endregion

    }

    [Serializable]
    public class Pong {

        #region Variables

        public string ID { get; private set; }

        #endregion

        #region Constructors

        public Pong( string pingID ) => ID = pingID;

        #endregion

    }

    #endregion

}
