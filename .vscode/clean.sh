#!/bin/bash
set -e

echo "=== Starting Full Clean ==="
echo "Current directory:"
pwd
echo

echo "Step 1: Removing build artifacts (bin directories)"
find . -type d -name bin -print -exec rm -rf {} + 2>/dev/null || true
echo

echo "Step 2: Removing intermediate objects (obj directories)"
find . -type d -name obj -print -exec rm -rf {} + 2>/dev/null || true
echo

echo "Step 3: Removing cooked assets (cooked directories)"
find . -type d -name cooked -print -exec rm -rf {} + 2>/dev/null || true
echo

echo "=== Full Clean Complete ==="
