FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore DevOpsProject.HiveMind.API/DevOpsProject.HiveMind.API.csproj
RUN dotnet publish DevOpsProject.HiveMind.API/DevOpsProject.HiveMind.API.csproj -c Release --no-restore -o /hive/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /hive
COPY --from=build /hive/publish .
EXPOSE 5149
ENTRYPOINT ["dotnet", "DevOpsProject.HiveMind.API.dll"]
