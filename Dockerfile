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
# The runtime image listens on 8080 and runs as the non-root "app" user.
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyFirstApi.dll"]
