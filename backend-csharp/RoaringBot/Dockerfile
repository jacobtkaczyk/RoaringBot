FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy and restore only the .csproj first
COPY RoaringBot.csproj ./
RUN dotnet restore

# Now copy the rest of the code
COPY . ./

# Build the app
RUN dotnet build -c Release

# Install Python and dependencies, plus debugger tools
RUN apt-get update && \
    apt-get install -y python3 python3-pip unzip curl && \
    pip install --break-system-packages pandas numpy && \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg && \
    rm -rf /var/lib/apt/lists/*

# Run the app
CMD ["dotnet", "run", "--configuration", "Release"]