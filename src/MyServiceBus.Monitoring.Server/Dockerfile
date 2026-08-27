FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/MyServiceBus.Monitoring.Server/MyServiceBus.Monitoring.Server.csproj \
    --warnaserror:'NU1901;NU1902;NU1903;NU1904'
RUN dotnet publish src/MyServiceBus.Monitoring.Server/MyServiceBus.Monitoring.Server.csproj \
    --no-restore --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MyServiceBus.Monitoring.Server.dll"]
