using System;
using System.Net;

namespace Networking {

    [Serializable]
    public struct Login {
        public string Username;
        public IPEndPoint LocalEndPoint;
        public IPEndPoint RemoteEndPoint;
    }

    public enum CommandType {
        UsernameTaken,

    }

    [Serializable]
    public struct Command {
        public CommandType Type;
        public User User;
    }

    [ Serializable ]
    public struct PlayerEvent {
        public User User;
        public string Status;
    }

}
