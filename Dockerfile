FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine
LABEL maintainer="Nikita Chizhov <nikita@nyris.io>"

WORKDIR /app
COPY publish/ /app/

RUN ln -sf /config/appsettings.json /app/appsettings.local.json

CMD ["./Nyris.Crdt.AspNetExample"]