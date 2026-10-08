FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Bizcord.WorkspaceApi/Bizcord.WorkspaceApi.csproj", "src/Bizcord.WorkspaceApi/"]
COPY ["src/Bizcord.Contracts/Bizcord.Contracts.csproj", "src/Bizcord.Contracts/"]
COPY ["src/Bizcord.MessageClient/Bizcord.MessageClient.csproj", "src/Bizcord.MessageClient/"]
RUN dotnet restore "src/Bizcord.WorkspaceApi/Bizcord.WorkspaceApi.csproj"
COPY . .
WORKDIR /src/src/Bizcord.WorkspaceApi
RUN dotnet build "Bizcord.WorkspaceApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Bizcord.WorkspaceApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bizcord.WorkspaceApi.dll"]