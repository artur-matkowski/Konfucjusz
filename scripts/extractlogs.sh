#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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

# Check if .env file exists
if [ ! -f "$ENV_FILE" ]; then
    error ".env file not found at $ENV_FILE"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "1. Copy the example env file:"
    echo "   cp $SCRIPT_DIR/.env.example $ENV_FILE"
    echo ""
    echo "2. Edit the .env file and set your SSH connection string:"
    echo "   SSH_CONNECT=\"your-ssh-alias-or-user@host\""
    echo ""
    exit 1
fi

# Load environment variables
source "$ENV_FILE"

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

# Create local logs directory
LOGS_DIR="$SCRIPT_DIR/../logs"
mkdir -p "$LOGS_DIR"

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
LOCAL_LOG_FILE="$LOGS_DIR/konfucjusz_$TIMESTAMP.log"

info "Extracting logs from remote server..."

# Try to get logs from journalctl (systemd service)
SERVICE_NAME="${REMOTE_SERVICE:-konfucjusz}"

if ssh "$SSH_CONNECT" "docker ps -q -f name=$SERVICE_NAME" 2>/dev/null | grep -q .; then
    info "Fetching logs from Docker container"
    ssh "$SSH_CONNECT" "docker logs $SERVICE_NAME -t" > "$LOCAL_LOG_FILE" 2>&1
else
    warn "Could not find running $SERVICE_NAME service or container"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "Specify the log source on the remote server."
    echo "Options:"
    echo "  - Set REMOTE_SERVICE in .env for systemd service name"
    echo "  - Ensure docker container is named '$SERVICE_NAME'"
    echo "  - Or manually specify log file path"
    echo ""

    # Try to fetch any recent logs from common locations
    info "Attempting to fetch logs from common locations..."
    ssh "$SSH_CONNECT" "cat /var/log/$SERVICE_NAME/*.log 2>/dev/null || cat ~/$SERVICE_NAME/logs/*.log 2>/dev/null || echo 'No logs found in common locations'" > "$LOCAL_LOG_FILE" 2>&1
fi

if [ -s "$LOCAL_LOG_FILE" ]; then
    success "Logs extracted to: $LOCAL_LOG_FILE"
    echo ""
    info "Log file size: $(du -h "$LOCAL_LOG_FILE" | cut -f1)"
    info "To view: less $LOCAL_LOG_FILE"
else
    warn "Log file is empty"
    rm -f "$LOCAL_LOG_FILE"
    exit 1
fi
