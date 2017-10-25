﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Networking {

    public class UdpServer {

        #region Events

        public event SocketEventHandler ClientConnectionRequested;
        public event DataEventHandler DataReceived;

        #endregion

        #region Local Variables

        public List<UdpSocket> Clients = new List<UdpSocket>();
        public EndPoint LocalEndPoint { get { try { return _listener.LocalEndPoint; } catch ( Exception ) { return null; } } }
        public EndPoint RemoteEndPoint { get { try { return _listener.RemoteEndPoint; } catch ( Exception ) { return null; } } }

        private Socket _listener;
        public bool Active;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the UDP server and automatically starts waiting for connections on all available local IP addresses.
        /// </summary>
        /// <param name="port">The local port to listen to</param>
        /// <param name="newClientCallback">The method to call if a client has been found</param>
        /// <param name="dataReceivedCallback">The method to call if a new packet has been received</param>
        public UdpServer( int port, SocketEventHandler newClientCallback, DataEventHandler dataReceivedCallback ) {
            // Initialize the listener socket
            _listener = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
            // Start listening to any connection made to the specified port
            _listener.Bind( new IPEndPoint( IPAddress.Any, port ) );

            ClientConnectionRequested += newClientCallback;
            DataReceived += dataReceivedCallback;

            Active = true;
            Receive();
        }

        /// <summary>
        /// Initializes the UDP server and automatically starts waiting for connections.
        /// </summary>
        /// <param name="hostname">The local IP to listen to</param>
        /// <param name="port">The local port to listen to</param>
        /// <param name="newClientCallback">The method to call if a client has been found</param>
        /// <param name="dataReceivedCallback">The method to call if a new packet has been received</param>
        public UdpServer( string hostname, int port, SocketEventHandler newClientCallback, DataEventHandler dataReceivedCallback ) {
            if ( !IPAddress.TryParse( hostname, out IPAddress ip ) )
                throw new InvalidCastException( $"Could not convert {hostname} to a valid IPAddress instance." );

            // Initialize the listener socket
            _listener = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp ) { ExclusiveAddressUse = false, EnableBroadcast = true };
            // Start listening to the specified IP and port
            _listener.Bind( new IPEndPoint( ip, port ) );

            ClientConnectionRequested += newClientCallback;
            DataReceived += dataReceivedCallback;

            Receive();
        }

        #endregion

        #region Methods

        private void Receive() {
            Active = true;

            Task.Run( () => {
                while ( Active ) {
                    EndPoint newClient = new IPEndPoint( IPAddress.Any, 0 );
                    byte[] bytes = new byte[ 4096 ];
                    int length = _listener.ReceiveFrom( bytes, ref newClient );

                    Packet p = new Packet( bytes.ToList().GetRange( 0, length ) );
                    UdpSocket user = new UdpSocket( newClient );

                    if ( Clients.Where( c => c.RemoteEndPoint.ToString() == newClient.ToString() ).ToList().Count == 0 ) {
                        Clients.Add( user );
                        ClientConnectionRequested?.Invoke( user );
                    }

                    DataReceived?.Invoke( user, p );
                }
            } );
        }

        public void Broadcast( object data ) => Broadcast( new Packet( data ) );
        public void Broadcast( Packet data ) => Clients.ToList().ForEach( c => c.Send( data ) );

        public void Stop() {
            Active = false;
            _listener.Close();
        }

        #endregion

    }

}
