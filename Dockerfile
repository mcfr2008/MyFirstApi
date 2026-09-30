# API image: build with the .NET SDK, run on the smaller ASP.NET runtime image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MyFirstApi.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
# Uploaded files (signatures, photos) go to App_Data/files; compose mounts a volume there.
RUN mkdir -p /app/App_Data/files && chown -R $APP_UID /app/App_Data
# Run as the image's non-root "app" user; the runtime image listens on 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyFirstApi.dll"]
