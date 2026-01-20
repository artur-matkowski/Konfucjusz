#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

error() {
    echo -e "${RED}ERROR:${NC} $1" >&2
}

warn() {
    echo -e "${YELLOW}WARNING:${NC} $1"
}

info() {
    echo -e "${BLUE}INFO:${NC} $1"
}

success() {
    echo -e "${GREEN}SUCCESS:${NC} $1"
}

step() {
    echo -e "${CYAN}STEP:${NC} $1"
}

# Check if .env file exists
if [ ! -f "$ENV_FILE" ]; then
    error ".env file not found at $ENV_FILE"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "1. Copy the example env file:"
    echo "   cp $SCRIPT_DIR/.env.example $ENV_FILE"
    echo ""
    echo "2. Edit the .env file and configure:"
    echo "   SSH_CONNECT=\"your-ssh-alias-or-user@host\""
    echo "   REMOTE_SERVICE=\"your-container-name\""
    echo ""
    exit 1
fi

# Load environment variables
source "$ENV_FILE"

# Set defaults
TUNNEL_PID_FILE="${TUNNEL_PID_FILE:-/tmp/konfucjusz-tunnel.pid}"
CONTAINER_NAME="${REMOTE_SERVICE:-konfucjusz_app}"

# Validate SSH_CONNECT
if [ -z "$SSH_CONNECT" ]; then
    error "SSH_CONNECT is not set in .env"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "Edit $ENV_FILE and set:"
    echo "   SSH_CONNECT=\"your-ssh-alias-or-user@host\""
    echo ""
    exit 1
fi

info "Using container name: $CONTAINER_NAME"

# Check if tunnel is already running
if [ -f "$TUNNEL_PID_FILE" ]; then
    OLD_PID=$(cat "$TUNNEL_PID_FILE")
    if kill -0 "$OLD_PID" 2>/dev/null; then
        error "Port mapping tunnel is already running (PID: $OLD_PID)"
        echo ""
        echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
        echo "A tunnel is already active. To restart:"
        echo "1. First ditch the existing mapping:"
        echo "   $SCRIPT_DIR/ditch-ports.sh"
        echo ""
        echo "2. Then run this script again"
        echo ""
        exit 1
    else
        rm -f "$TUNNEL_PID_FILE"
    fi
fi

# Check if local port 8081 is available
if lsof -Pi :8081 -sTCP:LISTEN -t >/dev/null 2>&1; then
    error "Local port 8081 is already in use"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "Something is already listening on local port 8081."
    echo "Check what's using it:"
    echo "   lsof -i :8081"
    echo ""
    echo "Stop that service before running port mapping."
    echo ""
    exit 1
fi

# Test SSH connection
info "Testing SSH connection to $SSH_CONNECT..."
if ! ssh -o ConnectTimeout=10 -o BatchMode=yes "$SSH_CONNECT" "echo 'Connection OK'" 2>/dev/null; then
    error "Cannot connect to $SSH_CONNECT"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "1. Verify your SSH config for '$SSH_CONNECT'"
    echo "2. Check that SSH keys are properly set up"
    echo "3. Test manually: ssh $SSH_CONNECT"
    echo ""
    exit 1
fi

echo ""
echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║           KONFUCJUSZ PORT MAPPING - DEPLOYMENT             ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${YELLOW}This script will:${NC}"
echo "  1. Stop container '$CONTAINER_NAME' on the remote server"
echo "  2. Create SSH tunnel with port mappings:"
echo "     • Remote 127.0.0.1:81  →  Local :8081 (access remote app locally)"
echo "     • Local :8080  →  Remote 127.0.0.1:8080 (expose local dev to remote)"
echo ""
read -p "Continue? [y/N] " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    info "Aborted by user"
    exit 0
fi

echo ""

# Step 1: Stop remote container
step "Stopping container '$CONTAINER_NAME' on remote server..."

STOP_OUTPUT=$(ssh "$SSH_CONNECT" "docker stop $CONTAINER_NAME" 2>&1)
STOP_EXIT=$?

if [ $STOP_EXIT -eq 0 ]; then
    success "Stopped Docker container: $CONTAINER_NAME"
else
    error "Failed to stop container '$CONTAINER_NAME'"
    echo "Output: $STOP_OUTPUT"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "Check if the container exists and is running:"
    echo "   ssh $SSH_CONNECT docker ps -a"
    echo ""
    echo "If the container has a different name, update REMOTE_SERVICE in:"
    echo "   $ENV_FILE"
    echo ""
    read -p "Continue anyway (container might already be stopped)? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        info "Aborted by user"
        exit 1
    fi
fi

# Step 2: Create SSH tunnel
step "Creating SSH tunnel with port mappings..."

# -L local:remote - forward local port to remote
# -R remote:local - forward remote port to local
#
# We want:
# - Access remote :81 on local :8081  => -L 8081:127.0.0.1:81
# - Expose local :8080 to remote :8080 => -R 8080:127.0.0.1:8080

ssh -f -N \
    -L 8081:127.0.0.1:81 \
    -R 8080:127.0.0.1:8080 \
    -o ServerAliveInterval=60 \
    -o ServerAliveCountMax=3 \
    -o ExitOnForwardFailure=yes \
    "$SSH_CONNECT"

# Find and save the tunnel PID
sleep 1
TUNNEL_PID=$(pgrep -f "ssh.*-L 8081:127.0.0.1:81.*$SSH_CONNECT" | head -1)

if [ -z "$TUNNEL_PID" ]; then
    error "Failed to establish SSH tunnel"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "The SSH tunnel could not be created. Check:"
    echo "  1. SSH connection is working: ssh $SSH_CONNECT"
    echo "  2. Remote ports are not blocked"
    echo "  3. No firewall blocking the tunnel"
    echo ""
    echo "Don't forget to restart the container manually:"
    echo "   ssh $SSH_CONNECT docker start $CONTAINER_NAME"
    echo ""
    exit 1
fi

echo "$TUNNEL_PID" > "$TUNNEL_PID_FILE"

echo ""
echo -e "${GREEN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              PORT MAPPING ESTABLISHED                      ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""
success "SSH tunnel created (PID: $TUNNEL_PID)"
echo ""
echo -e "${CYAN}Active port mappings:${NC}"
echo "  • http://localhost:8081    →  Remote Konfucjusz (port 81)"
echo "  • Remote :8080             →  http://localhost:8080 (your local dev)"
echo ""
echo -e "${YELLOW}IMPORTANT:${NC}"
echo "  • Container '$CONTAINER_NAME' is STOPPED on remote"
echo "  • When done, run: $SCRIPT_DIR/ditch-ports.sh"
echo "  • This will close the tunnel and restart the container"
echo ""
