#!/bin/bash
# Test the DetonatorAgent API on Linux
#
# Usage: ./test-api.sh [test_file] [path] [fileargs] [executefile] [executiontype]
#
# Parameters:
#   test_file: Path to the file to execute (optional, default: /tmp/test.sh)
#   path: Target directory to write the file (optional, default: /tmp/)
#   fileargs: Arguments to pass to the executable (optional)
#   executefile: Specific file to execute from an archive (optional)
#   executiontype: Execution service type - "linux" for Linux (optional, default: linux)

TEST_FILE="${1:-/tmp/test.sh}"
TARGET_PATH="${2:-/tmp/}"
FILE_ARGS="${3:-}"
EXECUTE_FILE="${4:-}"
EXECUTION_TYPE="${5:-linux}"
BASE_URL="http://localhost:8080"

echo "=== Testing DetonatorAgent API on Linux ==="
echo "Base URL: $BASE_URL"
echo ""

# Test GET /api/execute/types endpoint
echo -e "\nTesting GET /api/execute/types..."
if response=$(curl -s -w "%{http_code}" "$BASE_URL/api/execute/types"); then
    http_code="${response: -3}"
    body="${response%???}"
    if [ "$http_code" = "200" ]; then
        echo "Success!"
        echo "Response: $body" | jq . 2>/dev/null || echo "$body"
    else
        echo "HTTP Error: $http_code"
        echo "Response: $body"
    fi
else
    echo "Error: Failed to connect to API"
fi

# Test /api/logs/agent endpoint
echo -e "\nTesting GET /api/logs/agent..."
if response=$(curl -s -w "%{http_code}" "$BASE_URL/api/logs/agent"); then
    http_code="${response: -3}"
    body="${response%???}"
    if [ "$http_code" = "200" ]; then
        echo "Success!"
        echo "Response: $body" | jq . 2>/dev/null || echo "$body"
    else
        echo "HTTP Error: $http_code"
        echo "Response: $body"
    fi
else
    echo "Error: Failed to connect to API"
fi

# Test /api/execute/exec endpoint with file upload
echo -e "\nTesting POST /api/execute/exec..."
echo "Test File: $TEST_FILE"
echo "Target Path: $TARGET_PATH"
echo "File Args: $FILE_ARGS"
echo "Execute File: $EXECUTE_FILE"
echo "Execution Type: $EXECUTION_TYPE"
echo ""

# Create a test file if it doesn't exist
if [ ! -f "$TEST_FILE" ]; then
    echo "Creating test file: $TEST_FILE"
    echo '#!/bin/bash' > "$TEST_FILE"
    echo 'echo "Hello from test script"' >> "$TEST_FILE"
    echo 'sleep 5' >> "$TEST_FILE"
    chmod +x "$TEST_FILE"
fi

# Build curl command with all parameters
CURL_CMD="curl -s -w \"%{http_code}\" -X POST \"$BASE_URL/api/execute/exec\" -F \"file=@$TEST_FILE\" -F \"path=$TARGET_PATH\""

if [ -n "$FILE_ARGS" ]; then
    CURL_CMD="$CURL_CMD -F \"fileargs=$FILE_ARGS\""
fi

if [ -n "$EXECUTE_FILE" ]; then
    CURL_CMD="$CURL_CMD -F \"executeFile=$EXECUTE_FILE\""
fi

if [ -n "$EXECUTION_TYPE" ]; then
    CURL_CMD="$CURL_CMD -F \"executiontype=$EXECUTION_TYPE\""
fi

echo "Executing: $CURL_CMD"
echo ""

if response=$(eval "$CURL_CMD"); then
    http_code="${response: -3}"
    body="${response%???}"
    if [ "$http_code" = "200" ]; then
        echo "Success!"
        echo "Response: $body" | jq . 2>/dev/null || echo "$body"
    else
        echo "HTTP Error: $http_code"
        echo "Response: $body"
    fi
else
    echo "Error: Failed to connect to API"
fi

echo -e "\n=== API testing complete! ==="
echo "Visit http://localhost:8080/swagger for the Swagger UI"
echo ""
echo "=== Examples ==="
echo "Basic execution:"
echo "  ./test-api.sh /path/to/executable"
echo ""
echo "With arguments:"
echo '  ./test-api.sh /path/to/executable /tmp/ "-arg1 -arg2"'
echo ""
echo "With specific execution type:"
echo '  ./test-api.sh /path/to/executable /tmp/ "" "" linux'
