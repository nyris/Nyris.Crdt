FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["samples/Nyris.Crdt.AspNetExampleV2/Nyris.Crdt.AspNetExampleV2.csproj", "Nyris.Crdt.AspNetExampleV2/"]
COPY ["src/Nyris.ManagedCrdtsV2/Nyris.ManagedCrdtsV2.csproj", "Nyris.ManagedCrdtsV2/"]
COPY ["src/Nyris.Crdt.Distributed/Nyris.Crdt.Distributed.csproj", "Nyris.Crdt.Distributed/"]
COPY ["src/Nyris.Crdt/Nyris.Crdt.csproj", "Nyris.Crdt/"]
COPY ["src/Nyris.Crdt.Transport.Grpc/Nyris.Crdt.Transport.Grpc.csproj", "Nyris.Crdt.Transport.Grpc/"]
RUN dotnet restore "samples/Nyris.Crdt.AspNetExampleV2/Nyris.Crdt.AspNetExampleV2.csproj"
COPY . .
WORKDIR "/src/Nyris.Crdt.AspNetExampleV2"
RUN dotnet build "Nyris.Crdt.AspNetExampleV2.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nyris.Crdt.AspNetExampleV2.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nyris.Crdt.AspNetExampleV2.dll"]
