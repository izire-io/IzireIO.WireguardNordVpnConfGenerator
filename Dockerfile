#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG GH_PKG_USERNAME
ARG GH_PKG_PAT
WORKDIR /src
COPY . .
RUN sed -i "s/GH_PKG_USERNAME_PLACEHOLDER/$GH_PKG_USERNAME/g" nuget.config
RUN sed -i "s/GH_PKG_PAT_PLACEHOLDER/$GH_PKG_PAT/g" nuget.config
RUN dotnet restore "./IzireIO.WireguardNordVpnConfGenerator.csproj"
WORKDIR "/src/."
RUN dotnet build "IzireIO.WireguardNordVpnConfGenerator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IzireIO.WireguardNordVpnConfGenerator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IzireIO.WireguardNordVpnConfGenerator.dll"]