# Stage 1: build and publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY Konfucjusz.csproj ./
RUN dotnet restore Konfucjusz.csproj

# copy the rest of the source
COPY . ./

# publish
RUN dotnet publish Konfucjusz.csproj -c Release -o /app/publish

# Stage 2: runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# ASP.NET Core will listen on port 80 inside container
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Version info - set at build time, readable at runtime
ARG APP_VERSION=dev
ENV APP_VERSION=${APP_VERSION}

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Konfucjusz.dll"]
