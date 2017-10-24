using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace Networking {

    #region Event Handlers

    public delegate void PacketEventHandler( Packet packet );

    #endregion

    public class Packet {

        #region Events

        public event PacketEventHandler ParseFailed;

        #endregion

        #region Local Variables

        public Type Type { get { try { return Content.GetType(); } catch ( Exception ) { return null; } } }
        public object Content;
        public byte[] Serialized { get => SerializePacket(); }

        #endregion

        #region Constructors

        /// <summary>
        /// The standard <see cref="Packet"/>. Contains no information.
        /// </summary>
        public Packet() { ParseFailed += ParseFailedMessage; }
        /// <summary>
        /// Create a <see cref="Packet"/> from any type of <see cref="object"/>.
        /// </summary>
        /// <param name="obj">the <see cref="object"/> to create the <see cref="Packet"/> from</param>
        public Packet( object obj ) { Content = obj; ParseFailed += ParseFailedMessage; }
        /// <summary>
        /// Create a <see cref="Packet"/> from a <see cref="byte"/> array.
        /// </summary>
        /// <param name="bytes">The <see cref="byte"/> array to convert from</param>
        public Packet( IEnumerable<byte> bytes ) {
            ParseFailed += ParseFailedMessage;

            try {
                Content = Deserialize( bytes );
            } catch ( Exception ) {
                ParseFailed?.Invoke( this );
            }
        }

        #endregion

        #region Methods

        private static void ParseFailedMessage( Packet packet ) { Console.WriteLine( "Failed to deserialize packet." ); }

        /// <summary>
        /// Converts the contens of a <see cref="Packet"/> into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>The <see cref="byte"/> array containing the converted <see cref="object"/></returns>
        public byte[] SerializePacket() { return Serialize( Content ); }
        /// <summary>
        /// Converts the <see cref="Packet"/> into the given <see cref="System.Type"/>.
        /// </summary>
        /// <typeparam name="T">The <see cref="System.Type"/> to try to convert the <see cref="Packet"/> into</typeparam>
        /// <returns>The converted <see cref="Packet"/> as the given <see cref="System.Type"/></returns>
        public T DeserializePacket<T>() { return ( T )Content; }
        /// <summary>
        /// Tries to parse the contents of the <see cref="Packet"/> into the given <see cref="System.Type"/>.
        /// </summary>
        /// <typeparam name="T">The type to try to convert the contents of the <see cref="Packet"/> into</typeparam>
        /// <param name="to">The variable that will hold the value of the newly converted contents</param>
        /// <returns>True or false depending on whether the conversion was successfull or not</returns>
        public bool TryDeserializePacket<T>( out T to ) {
            to = default( T );

            if ( Type != typeof( T ) ) {
                ParseFailed?.Invoke( this );
                return false;
            }

            try {
                to = ( T )Content;
                return true;
            } catch ( Exception ) { /* Ignored */ }

            ParseFailed?.Invoke( this );
            return false;
        }

        #region Internal Class Tools

        /// <summary>
        /// Converts an <see cref="object"/> of any type to a <see cref="byte"/> array.
        /// </summary>
        /// <param name="obj">the <see cref="object"/> to convert</param>
        /// <returns>The converted <see cref="object"/> as a <see cref="byte"/> array</returns>
        private static byte[] Serialize( object obj ) {
            using ( MemoryStream ms = new MemoryStream() ) {
                new BinaryFormatter().Serialize( ms, obj );
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Converts <see cref="byte"/> array to the specified <see cref="object"/>
        /// </summary>
        /// <typeparam name="T">The type to convert the <see cref="byte"/> array into</typeparam>
        /// <param name="bytes">The <see cref="byte"/> array to convert to an <see cref="object"/> of the specified type</param>
        /// <returns>The converted <see cref="byte"/> array as an <see cref="object"/> of the specified type</returns>
        private static object Deserialize( IEnumerable<byte> bytes ) {
            byte[] byteArray = bytes as byte[] ?? bytes.ToArray();
            if ( byteArray.Length <= 0 )
                return null;

            using ( MemoryStream ms = new MemoryStream( byteArray ) )
                return new BinaryFormatter().Deserialize( ms );
        }

        #endregion

        #endregion

    }

}
