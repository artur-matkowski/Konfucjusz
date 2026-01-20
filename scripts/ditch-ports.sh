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
    echo "The .env file is required to restart the remote container."
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

echo ""
echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║           KONFUCJUSZ PORT MAPPING - TEARDOWN               ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${YELLOW}This script will:${NC}"
echo "  1. Close the SSH tunnel (stop port mappings)"
echo "  2. Restart container '$CONTAINER_NAME' on the remote server"
echo ""

# Step 1: Kill SSH tunnel
step "Closing SSH tunnel..."

TUNNEL_KILLED=false

if [ -f "$TUNNEL_PID_FILE" ]; then
    TUNNEL_PID=$(cat "$TUNNEL_PID_FILE")
    if kill -0 "$TUNNEL_PID" 2>/dev/null; then
        kill "$TUNNEL_PID" 2>/dev/null || true
        sleep 1
        # Force kill if still running
        if kill -0 "$TUNNEL_PID" 2>/dev/null; then
            kill -9 "$TUNNEL_PID" 2>/dev/null || true
        fi
        success "Killed SSH tunnel (PID: $TUNNEL_PID)"
        TUNNEL_KILLED=true
    else
        warn "Tunnel process (PID: $TUNNEL_PID) was not running"
    fi
    rm -f "$TUNNEL_PID_FILE"
else
    warn "No tunnel PID file found at $TUNNEL_PID_FILE"
fi

# Also try to find and kill any orphaned tunnel processes
ORPHAN_PIDS=$(pgrep -f "ssh.*-L 8081:127.0.0.1:81.*$SSH_CONNECT" 2>/dev/null || true)
if [ -n "$ORPHAN_PIDS" ]; then
    info "Found orphaned tunnel processes, killing them..."
    echo "$ORPHAN_PIDS" | xargs -r kill 2>/dev/null || true
    sleep 1
    echo "$ORPHAN_PIDS" | xargs -r kill -9 2>/dev/null || true
    TUNNEL_KILLED=true
fi

if [ "$TUNNEL_KILLED" = false ]; then
    warn "No active tunnel found to close"
    echo ""
    echo -e "${YELLOW}=== NOTE ===${NC}"
    echo "No SSH tunnel was found running. This might mean:"
    echo "  - The tunnel was already closed"
    echo "  - The tunnel crashed"
    echo "  - The tunnel was never started"
    echo ""
fi

# Step 2: Test SSH connection
step "Testing SSH connection..."
if ! ssh -o ConnectTimeout=10 -o BatchMode=yes "$SSH_CONNECT" "echo 'Connection OK'" 2>/dev/null; then
    error "Cannot connect to $SSH_CONNECT"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "Cannot connect to remote server to restart the container."
    echo "Please restart it manually:"
    echo ""
    echo "   ssh $SSH_CONNECT"
    echo "   docker start $CONTAINER_NAME"
    echo ""
    exit 1
fi

# Step 3: Restart remote container
step "Restarting container '$CONTAINER_NAME' on remote server..."

START_OUTPUT=$(ssh "$SSH_CONNECT" "docker start $CONTAINER_NAME" 2>&1)
START_EXIT=$?

if [ $START_EXIT -eq 0 ]; then
    success "Started Docker container: $CONTAINER_NAME"
else
    error "Failed to start container '$CONTAINER_NAME'"
    echo "Output: $START_OUTPUT"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "Please restart the container manually:"
    echo ""
    echo "   ssh $SSH_CONNECT"
    echo "   docker start $CONTAINER_NAME"
    echo "   docker logs $CONTAINER_NAME"
    echo ""
    echo "If container name differs, update REMOTE_SERVICE in:"
    echo "   $ENV_FILE"
    echo ""
    exit 1
fi

# Step 4: Verify container is running
step "Verifying container status..."
sleep 2

CONTAINER_RUNNING=$(ssh "$SSH_CONNECT" "docker ps -q -f name=$CONTAINER_NAME" 2>/dev/null)

echo ""
if [ -n "$CONTAINER_RUNNING" ]; then
    echo -e "${GREEN}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║              TEARDOWN COMPLETE                             ║${NC}"
    echo -e "${GREEN}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    success "Container '$CONTAINER_NAME' is running on remote server"
    echo ""
    echo -e "${CYAN}Summary:${NC}"
    echo "  ✓ SSH tunnel closed"
    echo "  ✓ Remote container restarted and running"
    echo ""
else
    echo -e "${YELLOW}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${YELLOW}║              TEARDOWN PARTIALLY COMPLETE                   ║${NC}"
    echo -e "${YELLOW}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    warn "Container may not be running properly"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "The container was started but may have crashed. Check status:"
    echo ""
    echo "   ssh $SSH_CONNECT"
    echo "   docker ps -a | grep $CONTAINER_NAME"
    echo "   docker logs $CONTAINER_NAME"
    echo ""
fi
