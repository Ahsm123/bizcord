FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/Bizcord.Gateway/Bizcord.Gateway.csproj src/Bizcord.Gateway/
COPY ["src/Bizcord.Vault/Bizcord.Vault.csproj", "src/Bizcord.Vault/"]
RUN dotnet restore src/Bizcord.Gateway/Bizcord.Gateway.csproj
COPY . .
RUN dotnet publish src/Bizcord.Gateway/Bizcord.Gateway.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bizcord.Gateway.dll"]
