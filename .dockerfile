FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MoviesApi/MoviesApi.csproj", "MoviesApi/"]
RUN dotnet restore "MoviesApi/MoviesApi.csproj"
COPY . .
WORKDIR "/src/MoviesApi"
RUN dotnet build "MoviesApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MoviesApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MoviesApi.dll"]
