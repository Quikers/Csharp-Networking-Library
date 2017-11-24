using System;
using System.Diagnostics;
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
        public static bool IsLocal( this IPAddress toTest ) {
            byte[] bytes = toTest.GetAddressBytes();
            return bytes[ 0 ] == 127 && bytes[ 1 ] == 0 && bytes[ 2 ] == 0 && bytes[ 3 ] == 1 ||
                   bytes[ 0 ] == 10 ||
                   bytes[ 0 ] == 192 && bytes[ 1 ] == 168 ||
                   bytes[ 0 ] == 172 && bytes[ 1 ] >= 16 && bytes[ 1 ] <= 31;
        }

        public static IPAddress GetAddress( this EndPoint endPoint ) => IPAddress.Parse( endPoint.ToString().Split( ':' )[ 0 ] );
        public static int GetPort( this EndPoint endPoint ) => int.Parse( endPoint.ToString().Split( ':' )[ 1 ] );

    }

    #endregion

    #region DataClasses

    [Serializable]
    public enum GET {
        NewClientID,
        UserList,

    }

    [Serializable]
    public enum REQUEST {
        Login,

    }

    [Serializable]
    public struct NewID {
        public int ServerPort;
        public string ID;
    }

    [Serializable]
    public sealed class Ping {

        #region Variables

        /// <summary>A unique <see cref="string"/> ID.</summary>
        public string ID { get; }
        /// <summary>A <see cref="Stopwatch"/> to track how many milliseconds it took a <see cref="Ping"/><see cref="Pong"/> to bounce back from the <see cref="UdpServer"/>.</summary>
        [NonSerialized] public Stopwatch MsCounter;
        /// <summary>How many milliseconds it took to get from the <see cref="UdpServer"/> to the <see cref="UdpSocket"/>.</summary>
        public float MsSinceSent { get; private set; }
        /// <summary>How many milliseconds it took to get from this <see cref="UdpSocket"/> to the <see cref="UdpServer"/> and back here.</summary>
        public float MsRoundTrip { get; private set; }

        #endregion

        #region Constructors

        public Ping() {
            MsCounter = new Stopwatch();
            ID = GenerateID();
        }

        #endregion

        #region Methods

        private string GenerateID() {
            return Guid.NewGuid().ToString( "N" );
        }

        /// <summary>
        /// Stops the <see cref="Ping"/>-counter. This sets the <seealso cref="MsSinceSent"/> and <seealso cref="MsRoundTrip"/> variables to the amount of milliseconds it took to send and receive a <see cref="Ping"/>.
        /// </summary>
        public void MarkTime() {
            MsRoundTrip = MsCounter.ElapsedMilliseconds;
            MsSinceSent = ( float )( MsSinceSent / 2.0 );
        }

        /// <summary>Converts the <see cref="Ping"/> into a <see cref="Pong"/> with the same <see cref="ID"/>.</summary>
        /// <returns>The converted <see cref="Ping"/> as a <see cref="Pong"/></returns>
        public Pong ToPong() { return new Pong( ID ); }

        #endregion

    }

    [Serializable]
    public sealed class Pong {

        #region Variables

        public string ID { get; private set; }

        [NonSerialized] private Stopwatch stpwatch;
        private int _msSinceLastUpdated;
        public int MsSinceLastUpdated { get { if ( stpwatch?.ElapsedMilliseconds != null ) _msSinceLastUpdated = ( int )stpwatch?.ElapsedMilliseconds; return _msSinceLastUpdated; } private set => _msSinceLastUpdated = value; } 

        #endregion

        #region Constructors

        public Pong( string pingID ) {
            stpwatch = new Stopwatch();
            ID = pingID;
            stpwatch.Restart();
        }

        #endregion

    }

    [Serializable]
    public struct Login {
        public string Username;

        public Login( string username ) { Username = username; }
    }

    [ Serializable ]
    public struct Disconnect { }

    #endregion

}
