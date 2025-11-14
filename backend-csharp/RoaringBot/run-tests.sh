#!/bin/bash
# Simple script to compile and run the database tests

echo "🔨 Compiling test file..."
dotnet build --no-restore -c Release DBHelperTests.cs 2>&1 | grep -E "(error|warning|succeeded)" || true

echo ""
echo "🧪 Running database tests..."
echo ""

# Compile the test file as a standalone program
dotnet exec --runtimeconfig RoaringBot.runtimeconfig.json \
  --depsfile RoaringBot.deps.json \
  --additionalprobingpath /usr/share/dotnet/packs \
  /bin/bash -c "cd /app && dotnet exec bin/Release/net9.0/RoaringBot.dll --test-mode" 2>&1 || \
  dotnet run --project . -- test 2>&1 || \
  echo "To run tests, use: dotnet run --project . -- --test"

