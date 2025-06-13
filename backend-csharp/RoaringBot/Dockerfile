FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy and restore only the .csproj first
COPY RoaringBot.csproj ./
RUN dotnet restore

# Now copy the rest of the code
COPY . ./

# Build the app
RUN dotnet build -c Release

# Run the app
CMD ["dotnet", "run", "--configuration", "Release"]
