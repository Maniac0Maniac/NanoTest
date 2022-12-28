using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace Skeleton {
    public partial class Skeleton {
        public static NetworkStream stream;

        // The telbet server on a thread
        public static void Start_Listner() {
            Thread worker = new Thread(() => Start_TcpListner());
            worker.Start();
           // Wait_Listner();
        }

        public static void Wait_Listner() {

            TcpListener listener = new TcpListener(IPAddress.Any, 54321);

            // Start listening for incoming connections with backlog
            listener.Start(0);

            while (true) {
                try {
                    // Wait for incoming connections
                    TcpClient client = listener.AcceptTcpClient();

                    // Start thread to handle connection
                    Thread worker = new Thread(() => WorkerThread(client));
                    worker.Start();
                } catch (Exception ex) {
                    Debug.WriteLine($"Exception:-{ex.Message}");
                }
            }
        }

        private static void WorkerThread(TcpClient client) {
            try {
                NetworkStream stream = client.GetStream();

                Byte[] bytes = new Byte[256];
                int i;

                // Loop reading data until connection closed
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0) {
                    // Do something with data ?

                    // Write back received data bytes to stream
                    stream.Write(bytes, 0, i);
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Exception:-{ex.Message}");
            } finally {
                // Shutdown connection
                client.Close();
            }
        }

        private static void Start_TcpListner() {
            TcpListener listener = new TcpListener(IPAddress.Any, 54321);

            // Start listening for incoming connections
            listener.Start(0);
            while (true) {
                try {
                    // Wait for incoming connections
                    TcpClient client = listener.AcceptTcpClient();

                    stream = client.GetStream();

                    Byte[] bytes = new Byte[256];
                    int i;

                    // Wait for incoming data and echo back
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0) {
                        // Do something with data ?

                        stream.Write(bytes, 0, i);
                    }

                    // Shutdown connection
                    client.Close();
                } catch (Exception ex) {
                    Debug.WriteLine($"Exception:-{ex.Message}");
                }
            }

        }
    }
}