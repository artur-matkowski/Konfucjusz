#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_COUNTER_FILE="$SCRIPT_DIR/.build-counter"

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

# Parse command-line arguments
PUSH_TO_REGISTRY=false
while [[ $# -gt 0 ]]; do
    case $1 in
        --push)
            PUSH_TO_REGISTRY=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [--push]"
            echo ""
            echo "Build a test deployment image with automatic versioning."
            echo ""
            echo "Options:"
            echo "  --push    Push the image to ghcr.io after building"
            echo "  -h, --help    Show this help message"
            echo ""
            echo "The script will:"
            echo "  1. Detect current branch (must be issue/X pattern)"
            echo "  2. Generate tag: issueX-buildNNN with auto-increment"
            echo "  3. Build Docker image locally"
            echo "  4. Optionally push to ghcr.io (with --push)"
            exit 0
            ;;
        *)
            error "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Get current branch name
BRANCH=$(git -C "$PROJECT_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null)
if [ -z "$BRANCH" ]; then
    error "Could not determine current git branch"
    exit 1
fi

info "Current branch: $BRANCH"

# Check if branch follows issue/X pattern
if [[ ! "$BRANCH" =~ ^issue/([0-9]+)$ ]]; then
    error "Branch '$BRANCH' does not follow 'issue/X' pattern"
    echo ""
    echo -e "${YELLOW}=== ACTION REQUIRED ===${NC}"
    echo "This script is for test deployments from issue branches."
    echo "Expected branch format: issue/X (e.g., issue/8, issue/42)"
    echo ""
    echo "For production deployments from main branch, use:"
    echo "  git tag vX.X.X && git push origin vX.X.X"
    echo ""
    exit 1
fi

ISSUE_NUMBER="${BASH_REMATCH[1]}"
info "Issue number: $ISSUE_NUMBER"

# Read and increment build counter for this issue
# Counter file format: issue_number:build_number per line
if [ -f "$BUILD_COUNTER_FILE" ]; then
    # Get current build number for this issue
    CURRENT_BUILD=$(grep "^$ISSUE_NUMBER:" "$BUILD_COUNTER_FILE" 2>/dev/null | cut -d: -f2)
    if [ -z "$CURRENT_BUILD" ]; then
        CURRENT_BUILD=0
    fi
else
    CURRENT_BUILD=0
fi

# Increment build number
NEW_BUILD=$((CURRENT_BUILD + 1))
NEW_BUILD_PADDED=$(printf "%03d" $NEW_BUILD)

# Update counter file
if [ -f "$BUILD_COUNTER_FILE" ]; then
    # Remove old entry for this issue and add new one
    grep -v "^$ISSUE_NUMBER:" "$BUILD_COUNTER_FILE" > "$BUILD_COUNTER_FILE.tmp" || true
    mv "$BUILD_COUNTER_FILE.tmp" "$BUILD_COUNTER_FILE"
fi
echo "$ISSUE_NUMBER:$NEW_BUILD" >> "$BUILD_COUNTER_FILE"

# Generate version tag
VERSION_TAG="issue${ISSUE_NUMBER}-build${NEW_BUILD_PADDED}"
IMAGE_NAME="ghcr.io/artur-matkowski/konfucjusz"
FULL_TAG="$IMAGE_NAME:$VERSION_TAG"

echo ""
echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║              KONFUCJUSZ TEST DEPLOYMENT BUILD              ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${YELLOW}Build configuration:${NC}"
echo "  • Branch:     $BRANCH"
echo "  • Issue:      #$ISSUE_NUMBER"
echo "  • Build:      $NEW_BUILD"
echo "  • Tag:        $VERSION_TAG"
echo "  • Full image: $FULL_TAG"
echo "  • Push:       $PUSH_TO_REGISTRY"
echo ""

read -p "Continue with build? [Y/n] " -n 1 -r
echo
if [[ $REPLY =~ ^[Nn]$ ]]; then
    info "Aborted by user"
    # Rollback counter
    NEW_BUILD=$((NEW_BUILD - 1))
    grep -v "^$ISSUE_NUMBER:" "$BUILD_COUNTER_FILE" > "$BUILD_COUNTER_FILE.tmp" || true
    mv "$BUILD_COUNTER_FILE.tmp" "$BUILD_COUNTER_FILE"
    if [ $NEW_BUILD -gt 0 ]; then
        echo "$ISSUE_NUMBER:$NEW_BUILD" >> "$BUILD_COUNTER_FILE"
    fi
    exit 0
fi

echo ""

# Step 1: Build Docker image
step "Building Docker image with version: $VERSION_TAG"

cd "$PROJECT_DIR"
docker build \
    --build-arg APP_VERSION="$VERSION_TAG" \
    -t "$FULL_TAG" \
    -f Dockerfile \
    .

if [ $? -eq 0 ]; then
    success "Docker image built: $FULL_TAG"
else
    error "Docker build failed"
    exit 1
fi

# Step 2: Push to registry (if requested)
if [ "$PUSH_TO_REGISTRY" = true ]; then
    echo ""
    step "Pushing image to registry..."

    docker push "$FULL_TAG"

    if [ $? -eq 0 ]; then
        success "Image pushed to registry: $FULL_TAG"
    else
        error "Failed to push image"
        exit 1
    fi
fi

echo ""
echo -e "${GREEN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              BUILD COMPLETED SUCCESSFULLY                  ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${CYAN}Image details:${NC}"
echo "  • Tag:        $FULL_TAG"
echo "  • Version:    $VERSION_TAG"
echo ""
echo -e "${YELLOW}To use this image:${NC}"
echo ""
echo "  1. Update docker-compose.yml on test server:"
echo "     image: $FULL_TAG"
echo ""
echo "  2. Or run directly:"
echo "     docker run -p 8080:80 $FULL_TAG"
echo ""
if [ "$PUSH_TO_REGISTRY" = false ]; then
    echo -e "${YELLOW}NOTE:${NC} Image was NOT pushed to registry."
    echo "       To push, run with --push flag"
    echo ""
fi
