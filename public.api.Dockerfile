FROM microsoft/dotnet:2.1-sdk-alpine3.7 AS build-env

WORKDIR /app

# Copy everything else and build
COPY . ./

WORKDIR /app/src/Services/Public.API
RUN dotnet publish -c Release -o out

# Build runtime image
FROM microsoft/dotnet:2.1-aspnetcore-runtime-alpine3.7 
ARG APP_VERSION
ENV SAVE2MEMRISE_AppVersion=$APP_VERSION

WORKDIR /app
EXPOSE 8080
COPY --from=build-env /app/src/Services/Public.API/out .
# RUN rm appsettings.Development.json
ENTRYPOINT ["dotnet", "Public.API.dll"]