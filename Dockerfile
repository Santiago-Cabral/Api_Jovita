# Imagen para build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar solo el csproj primero
COPY ForrajeriaJovitaAPI.csproj ./

RUN dotnet restore

# Copiar TODO el proyecto
COPY . ./

RUN dotnet publish -c Release -o /app/out

# Imagen final
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app/out .

EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:10000

ENTRYPOINT ["dotnet", "ForrajeriaJovitaAPI.dll"]

