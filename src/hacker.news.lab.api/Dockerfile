FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY hacker.news.lab.sln ./
COPY src/hacker.news.lab.api/*.csproj src/hacker.news.lab.api/
COPY src/hacker.news.lab.application/*.csproj src/hacker.news.lab.application/
COPY src/hacker.news.lab.domain/*.csproj src/hacker.news.lab.domain/
COPY src/hacker.news.lab.infrastructure/*.csproj src/hacker.news.lab.infrastructure/

RUN dotnet restore src/hacker.news.lab.api/hacker.news.lab.api.csproj

COPY . .

RUN dotnet publish src/hacker.news.lab.api/hacker.news.lab.api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "hacker.news.lab.api.dll"]
