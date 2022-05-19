#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "."]
COPY ["samples/Nyris.Crdt.AspNetExample/Nyris.Crdt.AspNetExample.csproj", "samples/Nyris.Crdt.AspNetExample/"]
COPY ["src/Nyris.Crdt.Distributed/Nyris.Crdt.Distributed.csproj", "src/Nyris.Crdt.Distributed/"]
COPY ["src/Nyris.Crdt/Nyris.Crdt.csproj", "src/Nyris.Crdt/"]
COPY ["src/Nyris.Crdt.Distributed.SourceGenerators/Nyris.Crdt.Distributed.SourceGenerators.csproj", "src/Nyris.Crdt.Distributed.SourceGenerators/"]
RUN dotnet restore "samples/Nyris.Crdt.AspNetExample/Nyris.Crdt.AspNetExample.csproj"
COPY . .
WORKDIR "/src/samples/Nyris.Crdt.AspNetExample"
RUN dotnet build "Nyris.Crdt.AspNetExample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nyris.Crdt.AspNetExample.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ./ComposeConfig/appsettings.json ./appsettings.local.json

ENTRYPOINT ["dotnet", "Nyris.Crdt.AspNetExample.dll"]