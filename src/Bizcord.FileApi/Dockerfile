FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Bizcord.FileApi/Bizcord.FileApi.csproj", "src/Bizcord.FileApi/"]
COPY ["src/Bizcord.Contracts/Bizcord.Contracts.csproj", "src/Bizcord.Contracts/"]
COPY ["src/Bizcord.Vault/Bizcord.Vault.csproj", "src/Bizcord.Vault/"]
COPY ["src/Bizcord.MessageClient/Bizcord.MessageClient.csproj", "src/Bizcord.MessageClient/"]
RUN dotnet restore "src/Bizcord.FileApi/Bizcord.FileApi.csproj"
COPY . .
RUN dotnet publish "src/Bizcord.FileApi/Bizcord.FileApi.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bizcord.FileApi.dll"]
