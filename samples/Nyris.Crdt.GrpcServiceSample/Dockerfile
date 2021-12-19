FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["samples/Nyris.Crdt.GrpcServiceSample/Nyris.Crdt.GrpcServiceSample.csproj", "Nyris.Crdt.GrpcServiceSample/"]
RUN dotnet restore "samples/Nyris.Crdt.GrpcServiceSample/Nyris.Crdt.GrpcServiceSample.csproj"
COPY . .
WORKDIR "/src/Nyris.Crdt.GrpcServiceSample"
RUN dotnet build "Nyris.Crdt.GrpcServiceSample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nyris.Crdt.GrpcServiceSample.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nyris.Crdt.GrpcServiceSample.dll"]
