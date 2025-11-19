FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy and restore only the main project file first
COPY RoaringBot.csproj ./
RUN dotnet restore

# Now copy everything else
COPY . ./

# Build the main project (must specify it)
RUN dotnet build RoaringBot.csproj -c Release

# Install Python + debugger tools
RUN apt-get update && \
    apt-get install -y python3 python3-pip unzip curl && \
    pip install --break-system-packages pandas numpy && \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg && \
    rm -rf /var/lib/apt/lists/*

CMD ["dotnet", "run", "--project", "RoaringBot.csproj", "--configuration", "Release"]