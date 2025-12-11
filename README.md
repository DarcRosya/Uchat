Steb-by-step tutorial how to launch Uchat:

from root dir of Uchat:

to compile exe files:
dotnet build Uchat.Server/Uchat.Server.csproj -c Release -m
dotnet build Uchat/Uchat.csproj -c Release -m

executables are located in:
Uchat\bin\Release\net9.0
Uchat.Server\bin\Release\net9.0

to setup database:
docker compose up -d

go to executable directories

to start server: Uchat.Server.exe -start 5000 (example)

to start client: Uchat.exe -local 5000 (example)

To kill server:Uchat.Server.exe -kill (no port needed)

Optionaly, to launch ngrok for port forwarding (connect to server from Web):
start server on 9999 port

cd Uchat.Server

launch docker container with ngrok
docker compose up -d

start client: Uchat.exe -ngrok (no port needed)

(server can now be accessed not only locally)