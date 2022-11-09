FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "."]
COPY ["samples/Nyris.Crdt.AspNetExampleV2/Nyris.Crdt.AspNetExampleV2.csproj", "Nyris.Crdt.AspNetExampleV2/"]

COPY ["src/Nyris.Crdt/Nyris.Crdt.csproj", "Nyris.Crdt/"]
COPY ["src/Nyris.Crdt.Managed/Nyris.Crdt.Managed.csproj", "Nyris.Crdt.Managed/"]
COPY ["src/Nyris.Crdt.Managed.DependencyInjection/Nyris.Crdt.Managed.DependencyInjection.csproj", "Nyris.Crdt.Managed.DependencyInjection/"]
COPY ["src/Nyris.Crdt.Managed.Discovery.Abstractions/Nyris.Crdt.Managed.Discovery.Abstractions.csproj", "Nyris.Crdt.Managed.Discovery.Abstractions/"]
COPY ["src/Nyris.Crdt.Managed.Exceptions/Nyris.Crdt.Managed.Exceptions.csproj", "Nyris.Crdt.Managed.Exceptions/"]
COPY ["src/Nyris.Crdt.Managed.Model/Nyris.Crdt.Managed.Model.csproj", "Nyris.Crdt.Managed.Model/"]
COPY ["src/Nyris.Crdt.Serialization.Abstractions/Nyris.Crdt.Serialization.Abstractions.csproj", "Nyris.Crdt.Serialization.Abstractions/"]
COPY ["src/Nyris.Crdt.Serialization.MessagePack/Nyris.Crdt.Serialization.MessagePack.csproj", "Nyris.Crdt.Serialization.MessagePack/"]
COPY ["src/Nyris.Crdt.Transport.Abstractions/Nyris.Crdt.Transport.Abstractions.csproj", "Nyris.Crdt.Transport.Abstractions/"]
COPY ["src/Nyris.Crdt.Transport.Grpc/Nyris.Crdt.Transport.Grpc.csproj", "Nyris.Crdt.Transport.Grpc/"]

RUN dotnet restore "Nyris.Crdt.AspNetExampleV2/Nyris.Crdt.AspNetExampleV2.csproj"

COPY src .
COPY samples/Nyris.Crdt.AspNetExampleV2 ./Nyris.Crdt.AspNetExampleV2

WORKDIR "/src/Nyris.Crdt.AspNetExampleV2"
RUN dotnet build "Nyris.Crdt.AspNetExampleV2.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nyris.Crdt.AspNetExampleV2.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nyris.Crdt.AspNetExampleV2.dll"]
