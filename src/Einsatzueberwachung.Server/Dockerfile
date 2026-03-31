FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Einsatzueberwachung.Server/Einsatzueberwachung.Server.csproj", "src/Einsatzueberwachung.Server/"]
COPY ["src/Einsatzueberwachung.Domain/Einsatzueberwachung.Domain.csproj", "src/Einsatzueberwachung.Domain/"]
RUN dotnet restore "src/Einsatzueberwachung.Server/Einsatzueberwachung.Server.csproj"
COPY . .
WORKDIR "/src/src/Einsatzueberwachung.Server"
RUN dotnet build "Einsatzueberwachung.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Einsatzueberwachung.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN mkdir -p /opt/einsatzueberwachung/data/berichte
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Einsatzueberwachung.Server.dll"]
