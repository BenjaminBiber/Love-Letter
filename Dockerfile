# Build the Blazor Server app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore dependencies
COPY LoveLetter.sln ./
COPY LoveLetter.App/LoveLetter.App.csproj LoveLetter.App/
RUN dotnet restore

# Copy source and publish
COPY . .
RUN dotnet publish LoveLetter.App/LoveLetter.App.csproj -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "LoveLetter.App.dll"]
