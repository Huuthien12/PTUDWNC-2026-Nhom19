FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/CulinaryBlog.slnx backend/
COPY backend/CulinaryBlog.Domain/*.csproj backend/CulinaryBlog.Domain/
COPY backend/CulinaryBlog.Application/*.csproj backend/CulinaryBlog.Application/
COPY backend/CulinaryBlog.Infrastructure/*.csproj backend/CulinaryBlog.Infrastructure/
COPY backend/CulinaryBlog.API/*.csproj backend/CulinaryBlog.API/
RUN dotnet restore backend/CulinaryBlog.API/CulinaryBlog.API.csproj
COPY backend/ backend/
RUN dotnet publish backend/CulinaryBlog.API/CulinaryBlog.API.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install --no-install-recommends -y wget \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CulinaryBlog.API.dll"]
