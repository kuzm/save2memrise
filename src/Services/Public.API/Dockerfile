FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env

WORKDIR /app

COPY src/Services/Public.API ./src/Services/Public.API

WORKDIR /app/src/Services/Public.API
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2 
ARG APP_VERSION
ENV SAVE2MEMRISE_AppVersion=$APP_VERSION

WORKDIR /app
EXPOSE 8080
COPY --from=build-env /app/src/Services/Public.API/out .
ENTRYPOINT ["dotnet", "Public.API.dll"]