# The room bot as a container. Build context = the ROOT of the repository (it uses the game's own EOS wrapper and MatchRules code):
#   docker compose -f Server/room-bot/docker-compose.roombot.yml up -d --build        (run it from the repository root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Assets/ThirdParty/EOS-SDK/SDK/Source Assets/ThirdParty/EOS-SDK/SDK/Source
COPY Assets/ThirdParty/EOS-SDK/SDK/Bin/libEOSSDK-Linux-Shipping.so Assets/ThirdParty/EOS-SDK/SDK/Bin/libEOSSDK-Linux-Shipping.so
COPY Assets/_Project/Scripts/MatchRules Assets/_Project/Scripts/MatchRules
COPY Server/room-bot Server/room-bot
RUN dotnet publish Server/room-bot/RoomBot.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /out .
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "RoomBot.dll"]
