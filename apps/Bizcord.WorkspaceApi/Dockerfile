FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/Bizcord.WorkspaceApi/Bizcord.WorkspaceApi.csproj", "apps/Bizcord.WorkspaceApi/"]
COPY ["apps/Bizcord.WorkspaceContracts/Bizcord.WorkspaceContracts.csproj", "apps/Bizcord.WorkspaceContracts/"]
RUN dotnet restore "apps/Bizcord.WorkspaceApi/Bizcord.WorkspaceApi.csproj"
COPY . .
WORKDIR /src/apps/Bizcord.WorkspaceApi
RUN dotnet build "Bizcord.WorkspaceApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Bizcord.WorkspaceApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bizcord.WorkspaceApi.dll"]