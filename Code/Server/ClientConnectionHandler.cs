using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class ClientConnectionHandler
    {
        public async Task HandleClientAsync(TcpClient client)
        {
            // Get the client's IP address for logging purposes
            string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown IP";

            try
            {
                using NetworkStream stream = client.GetStream();

                Console.WriteLine("[{0}] Starting to handle data flow...", clientEndPoint);
                // ==========================================
                // TODO 1: READ METADATA (Pending 'Shared' branch)
                // ==========================================
                // 1.1. Read the first 4 bytes to determine the JSON string length
                // 1.2. Read the JSON string based on the received length
                // 1.3. Deserialize the JSON into an object (extract file name, size)

                // Mock filename to prevent code errors
                string mockFileName = "test_file.txt";
                Console.WriteLine("[{0}] Request to upload file: {1}", clientEndPoint, mockFileName);
                // ==========================================
                // TODO 2: SEND READY RESPONSE
                // ==========================================
                // 2.1. Validate the file using MetadataValidator
                // 2.2. If valid, send a READY response to the Client

                // ==========================================
                // TODO 3: RECEIVE FILE DATA IN 64KB CHUNKS
                // ==========================================
                // 3.1. Create a .part file on the disk (e.g., test_file.txt.part)
                // 3.2. Use a while loop (until all bytes are received) to call stream.ReadAsync()
                // 3.3. Write data to the FileStream

                // ==========================================
                // TODO 4: FINALIZE & CLEAN UP
                // ==========================================
                // 4.1. Rename the file from .part to the official filename
                // 4.2. Send a COMPLETED notification to the Client

                Console.WriteLine("[{0}] Upload completed successfully.", clientEndPoint);
            }
            catch (Exception ex)
            {
                // If the client suddenly disconnects or sends garbage data, the error will be caught here.
                Console.WriteLine("[{0}] Error occurred while handling client: {1}", clientEndPoint, ex.Message);
                // TODO: Delete the .part file if writing was interrupted.
            }
            finally
            {
                client.Close();
                Console.WriteLine("Connection to {0} closed.", clientEndPoint);
            }
        }
    }
}
