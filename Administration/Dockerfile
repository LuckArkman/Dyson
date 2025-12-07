FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Administration/Administration.csproj", "Administration/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Dtos/Dtos.csproj", "Dtos/"]
COPY ["Records/Records.csproj", "Records/"]
COPY ["Enums/Enums.csproj", "Enums/"]
COPY ["Interfaces/Interfaces.csproj", "Interfaces/"]
RUN dotnet restore "Administration/Administration.csproj"
COPY . .
WORKDIR "/src/Administration"
RUN dotnet build "Administration.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Administration.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Administration.dll"]
