FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
WORKDIR /src 
COPY . . 
RUN dotnet restore DevOpsProject/DevOpsProject.CommunicationControl.API.csproj
RUN dotnet publish DevOpsProject/DevOpsProject.CommunicationControl.API.csproj -c Release --no-restore -o /hive/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final 
WORKDIR /hive 
COPY --from=build /hive/publish . 
ENTRYPOINT ["dotnet", "DevOpsProject.CommunicationControl.API.dll"]