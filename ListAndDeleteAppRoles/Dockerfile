# Use the official .NET Core SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the .NET Core project file(s) and restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy the remaining source code
COPY . .

# Build the application
RUN dotnet publish -c Release -o out

# Use the official .NET Core Runtime image as the base image for running the application
FROM mcr.microsoft.com/dotnet/runtime:6.0 AS runtime

# Set the working directory inside the container
WORKDIR /app

# Copy the published application from the build image to the runtime image
COPY --from=build /app/out .

# Set the entry point for the container
ENTRYPOINT ["dotnet", "ListAndDeleteAppRoles.dll"]
