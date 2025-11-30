using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualStudioMetricsOnCodeLens
{
    /// <summary>
    /// Named pipe server host for VSEditor -> CodeLens interprocess communication
    /// </summary>
    public static class PipeServerHost
    {
        private static readonly List<NamedPipeServerStream> _clients = new List<NamedPipeServerStream>();

        /// <summary>
        /// Gets the name of the named pipe used for interprocess communication.
        /// </summary>
        public static readonly string PipeName = "CodeLensPipe";

        /// <summary>
        /// Represents the token used to indicate a reload operation.
        /// </summary>
        /// <remarks>This field is a constant string with the value "Reload". It can be used as an
        /// identifier for operations or events related to reloading functionality.</remarks>
        public static readonly string ReloadToken = "Reload";

        /// <summary>
        /// Starts the server and begins listening for incoming named pipe connections.
        /// </summary>
        /// <remarks>This method initializes a named pipe server that listens asynchronously for client
        /// connections. Each accepted connection is added to the internal client list. The server runs continuously  in
        /// the background until the application is terminated.</remarks>
        public static void StartServer()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.Out,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();

                    lock (_clients) _clients.Add(server);
                }
            });
        }

        /// <summary>
        /// Sends a message to all connected clients.
        /// </summary>
        /// <remarks>This method iterates through the list of connected clients and sends the specified
        /// message to each client. If a client connection fails during the operation, the client is removed from the
        /// list of connected clients.</remarks>
        /// <param name="msg">The message to broadcast to all clients. Cannot be null.</param>
        public static void Broadcast(string msg)
        {
            lock (_clients)
            {
                foreach (var client in _clients.ToArray())
                {
                    try
                    {
                        var writer = new StreamWriter(client) { AutoFlush = true };
                        writer.WriteLine(msg);
                    }
                    catch
                    {
                        _clients.Remove(client);
                    }
                }
            }
        }
    }

}
