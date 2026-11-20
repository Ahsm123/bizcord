FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Bizcord.MessageStub/Bizcord.MessageStub.csproj", "src/Bizcord.MessageStub/"]
COPY ["src/Bizcord.Contracts/Bizcord.Contracts.csproj", "src/Bizcord.Contracts/"]
COPY ["src/Bizcord.Vault/Bizcord.Vault.csproj", "src/Bizcord.Vault/"]
COPY ["src/Bizcord.MessageClient/Bizcord.MessageClient.csproj", "src/Bizcord.MessageClient/"]
RUN dotnet restore "src/Bizcord.MessageStub/Bizcord.MessageStub.csproj"
COPY . .
RUN dotnet publish "src/Bizcord.MessageStub/Bizcord.MessageStub.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# aspnet and not runtime, because Bizcord.Vault and Bizcord.MessageClient use the ASP.NET Core shared framework
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Bizcord.MessageStub.dll"]
