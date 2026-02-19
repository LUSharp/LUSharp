#!/usr/bin/env bash
# LUSharp Installer for Linux/macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/LUSharp/LUSharp/master/install.sh | bash

set -euo pipefail

REPO="LUSharp/LUSharp"
INSTALL_DIR="$HOME/.lusharp/bin"

echo "Installing LUSharp..."

# Detect OS
OS="$(uname -s)"
case "$OS" in
    Linux*)  PLATFORM="linux-x64"; ASSET="lusharp-linux-x64.tar.gz";;
    Darwin*) PLATFORM="osx-x64";   ASSET="lusharp-osx-x64.tar.gz";;
    *)       echo "ERROR: Unsupported OS: $OS"; exit 1;;
esac

# Get latest release
RELEASE_JSON=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest") || {
    echo "ERROR: Failed to fetch latest release from GitHub."
    exit 1
}

TAG=$(echo "$RELEASE_JSON" | grep '"tag_name"' | head -1 | sed 's/.*: "\(.*\)".*/\1/')
DOWNLOAD_URL=$(echo "$RELEASE_JSON" | grep "browser_download_url.*$ASSET" | head -1 | sed 's/.*: "\(.*\)".*/\1/')

if [ -z "$DOWNLOAD_URL" ]; then
    echo "ERROR: Asset '$ASSET' not found in release $TAG."
    exit 1
fi

echo "  Version:  $TAG"
echo "  Platform: $PLATFORM"
echo "  Downloading $ASSET..."

# Download and extract
mkdir -p "$INSTALL_DIR"
curl -fsSL "$DOWNLOAD_URL" | tar -xz -C "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/lusharp"

# Add to PATH in shell configs
add_to_path() {
    local rc_file="$1"
    local line='export PATH="$HOME/.lusharp/bin:$PATH"'
    if [ -f "$rc_file" ] && grep -qF ".lusharp/bin" "$rc_file"; then
        return
    fi
    if [ -f "$rc_file" ] || [ "$rc_file" = "$HOME/.bashrc" ]; then
        echo "" >> "$rc_file"
        echo "# LUSharp" >> "$rc_file"
        echo "$line" >> "$rc_file"
        echo "  Updated $rc_file"
    fi
}

add_to_path "$HOME/.bashrc"
add_to_path "$HOME/.zshrc"

export PATH="$INSTALL_DIR:$PATH"

# Verify
if command -v lusharp &> /dev/null || [ -x "$INSTALL_DIR/lusharp" ]; then
    VERSION=$("$INSTALL_DIR/lusharp" --version 2>&1 || echo "$TAG")
    echo ""
    echo "LUSharp $TAG installed successfully!"
    echo "  Location: $INSTALL_DIR"
    echo "  $VERSION"
    echo ""
    echo "Restart your terminal or run:"
    echo "  export PATH=\"\$HOME/.lusharp/bin:\$PATH\""
    echo ""
    echo "Get started:"
    echo "  lusharp new MyGame"
else
    echo "WARNING: Installation may have failed. Check $INSTALL_DIR"
fi
