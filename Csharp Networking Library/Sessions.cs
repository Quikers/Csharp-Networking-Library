using System;
using System.Collections.Generic;
using System.Linq;

namespace UdpNetworking {

    [Serializable]
    public enum SessionState {
        Idle,
        PlayRequested,
        Playing,
        PauseRequested,
        Paused,
        StopRequested,
        Stopped
    }

    [Serializable]
    public enum SessionCommand {
        RequestPlay,
        RequestPause,
        RequestStop
    }

    [Serializable]
    public class Session {

        public delegate void SessionEvent( Session session );
        public event SessionEvent OnSessionStateChanged;

        public List<User> UserList = new List<User>();
        private SessionState _state = SessionState.Idle;
        public SessionState State {
            get => _state;
            set {
                OnSessionStateChanged?.Invoke( this );
                _state = value;
            }
        }

        public void Broadcast( Packet packet ) {
            foreach ( User user in UserList.ToList().Where( u => u.TcpSocket != null && u.TcpSocket.Connected ) ) {
                if ( user.TcpSocket != null && user.TcpSocket.Connected )
                    user.TcpSocket.Send( packet );
            }
        }

    }

}