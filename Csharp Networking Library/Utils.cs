
namespace Networking {

    public partial class Data {

        #region Event Handlers

        public delegate void UserListEventHandler( UserList uList );

        #endregion

        #region Events

        public static event UserListEventHandler OnUserListChanged;

        #endregion

        #region Static Data Containers

        private static UserList _userList = new UserList();
        public static UserList UserList {
            get => _userList;
            set {
                _userList = value;
                OnUserListChanged?.Invoke( _userList );
            }
        }
        //public static Sessions Sessions { get; set; } = new Sessions();

        #endregion
    }

}
