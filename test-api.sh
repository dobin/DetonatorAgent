#!/bin/bash
# Test the DetonatorAgent API on Linux

echo "Testing DetonatorAgent API..."

# Test /api/logs endpoint
echo -e "\nTesting GET /api/logs..."
if response=$(curl -s -w "%{http_code}" "http://localhost:8080/api/logs"); then
    http_code="${response: -3}"
    body="${response%???}"
    if [ "$http_code" = "200" ]; then
        echo "Success!"
        echo "Response: $body" | jq .
    else
        echo "HTTP Error: $http_code"
        echo "Response: $body"
    fi
else
    echo "Error: Failed to connect to API"
fi

# Test /api/execute endpoint
echo -e "\nTesting POST /api/execute..."
if response=$(curl -s -w "%{http_code}" -X POST "http://localhost:8080/api/execute" \
    -H "Content-Type: application/json" \
    -d '{"command": "echo Hello World"}'); then
    http_code="${response: -3}"
    body="${response%???}"
    if [ "$http_code" = "200" ]; then
        echo "Success!"
        echo "Response: $body" | jq .
    else
        echo "HTTP Error: $http_code"
        echo "Response: $body"
    fi
else
    echo "Error: Failed to connect to API"
fi

echo -e "\nAPI testing complete!"
echo "Visit http://localhost:8080/swagger for the Swagger UI"
