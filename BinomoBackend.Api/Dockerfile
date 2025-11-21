FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BinomoBackend.Api/BinomoBackend.Api.csproj", "BinomoBackend.Api/"]
COPY ["BinomoBackend.Infrastructure/BinomoBackend.Infrastructure.csproj", "BinomoBackend.Infrastructure/"]
COPY ["BinomoBackend.Domain/BinomoBackend.Domain.csproj", "BinomoBackend.Domain/"]
COPY ["BinomoBackend.Application/BinomoBackend.Application.csproj", "BinomoBackend.Application/"]
COPY ["BinomoBackend.Persistence/BinomoBackend.Persistence.csproj", "BinomoBackend.Persistence/"]
RUN dotnet restore "BinomoBackend.Api/BinomoBackend.Api.csproj"
COPY . .
WORKDIR "/src/BinomoBackend.Api"
RUN dotnet build "./BinomoBackend.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BinomoBackend.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BinomoBackend.Api.dll"]
