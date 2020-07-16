# WebSockets Transport

The websocket transport allows you to use MirrorNG in webgl clients and servers.

Based on [Ninja Websockets](https://github.com/ninjasource/Ninja.WebSockets)

Note that browsers are not allowed to open ports.  Thus the transport cannot function as a server. 
When you use make a webgl build, it will only be able to function as a client. 
Make your server a standalone build


## Usage

1) In Unity create a NetworkManager gameobject from the GameObject -> Networking -> NetworkManager.
2) Then remove the TcpTransport (the default transport), and add a WsTransport.
3) Update the Transport reference in the NetworkManager, NetworkClient and NetworkServer components.

![The WebSockets Transport component in the Inspector window](WebsocketTransport.png)
