FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Einsatzueberwachung.Mobile/Einsatzueberwachung.Mobile.csproj", "src/Einsatzueberwachung.Mobile/"]
COPY ["src/Einsatzueberwachung.Domain/Einsatzueberwachung.Domain.csproj", "src/Einsatzueberwachung.Domain/"]
RUN dotnet restore "src/Einsatzueberwachung.Mobile/Einsatzueberwachung.Mobile.csproj"
COPY . .
WORKDIR "/src/src/Einsatzueberwachung.Mobile"
RUN dotnet build "Einsatzueberwachung.Mobile.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Einsatzueberwachung.Mobile.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Einsatzueberwachung.Mobile.dll"]
