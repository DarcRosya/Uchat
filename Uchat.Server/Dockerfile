FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем csproj из родительской папки тоже (для зависимостей Database)
COPY ["Uchat.Server.csproj", "Uchat.Server/"]
COPY ["../Database/Database.csproj", "Database/"]
RUN dotnet restore "Uchat.Server/Uchat.Server.csproj"

# Копируем весь код
COPY . Uchat.Server/
COPY ../Database/ Database/

WORKDIR "/src/Uchat.Server"
RUN dotnet build "Uchat.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Uchat.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Создаём папку для БД
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "Uchat.Server.dll"]