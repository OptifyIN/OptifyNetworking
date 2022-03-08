# OptifyNetworking
Just a little .NET Framework networking library that I wanted to share

## Example of use
Create a new server and client object
```cs
using OptifyNetworking;

ONServer server = new ONServer();
ONClient client = new ONClient();
```

Start the server
```cs
int port = 9999
server.Start(port);
```

Connect the client
```cs
int port = 9999
string ip = "127.0.0.1"
client.Connect(ip, port);
```

Disconnect the client and stop the server
```cs
client.Disconnect();
server.Stop();
```

Client send data to the server
```cs
client.Write("Hello server !");
client.Write(new byte[] { 01, 45, 75, 36, 111, 25, 236 });
```

Broadcast to all clients connected
```cs
server.Broadcast("Hello clients !");
server.Broadcast(new byte[] { 01, 45, 75, 36, 111, 25, 236 });
```

Server send data to specific client
```cs
server.ClientList[0].SendClient("Hello client [0]",server.Encoding);
server.ClientList[0].SendClient(new byte[] { 01, 45, 75, 36, 111, 25, 236 });
//ClientList[0] is the first client of the client list of the server, but client can be identified by IP and Name (given when the connection occur)
//ex :
ClientInfo ci1 = serverON.ClientList.Find(o => o.Name == "Client1");
ClientInfo ci2 = serverON.ClientList.Find(o => o.IP == IPAddress.Parse("127.0.0.1"));
ci1.SendClient("Hello client [0]",server.Encoding);
ci2.SendClient(new byte[] { 01, 45, 75, 36, 111, 25, 236 });
```

Process recieved data
```cs
void Main(string[] args)
{
  server.DataReceived += Server_DataRecieved;
  client.DataReceived += Client_DataRecieved;
}

private void Server_DataRecieved(object sender, OptifyNetworking.Message e)
{
  string data_string = e.Text;
  byte[] data = e.Data;
}

private void Client_DataRecieved(object sender, OptifyNetworking.Message e)
{
  string data_string = e.Text;
  byte[] data = e.Data;
}
```
