Example scene that demonstrates opening an additional port for VOIP or (whatever else). There is no actual VOIP implementation
here, just the skeleton you need.

The buttons and text boxes can be used to host a server or connect as a client. When running as a host, the 
host IP and port are displayed, as well as an additional VOIP IP or port. When running as a client, the host IP's and port's can be entered in the text boxes 
to connect to the host.

When a client connects, a player will be spawned that can be moved around with the arrow keys.
The client will also automatically send a message to the host over the voip port to show that it works.

The connection type will be displayed on the client:
DIRECT - The connection was made directly to the host's IP.
PUNCHTHROUGH - The connection was made to an address on the host's router discovered via punchthrough.
RELAY - The connection is using the Noble Connect relays.