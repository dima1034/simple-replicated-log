FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Master.csproj", "Master/"]
RUN dotnet restore "Master/Master.csproj"

COPY . ./Master
RUN dotnet build "Master/Master.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Master/Master.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Master.dll"]
