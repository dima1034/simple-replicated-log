#!/bin/bash

# URLs for your gRPC services
MASTER_URL="http://localhost:5000" # Replace with your actual Master service URL
SECONDARY1_URL="http://localhost:8080" # Replace with your actual S1 service URL
SECONDARY2_URL="http://localhost:8081" # Replace with your actual S2 service URL

# Function to send messages
send_message() {
    local msg=$1
    local w=$2
    curl -X POST "$MASTER_URL/log?message=$msg&w=$w"
    # curl -X POST "$MASTER_URL/log" -d "message=$msg&w=$w"
}

# Function to get logs from secondary
get_logs_from_secondary() {
    local url=$1
    curl -X GET "$url/log"
}

# Starting M + S1 (assuming they are started automatically or already running)

# Send messages
send_message "Msg1" 1
send_message "Msg2" 2

# Here you should introduce logic to wait for the message to be processed
# This is a placeholder for waiting
sleep 2

send_message "Msg3" 3

# Wait for the third message to be processed with W=3
# This might require a more sophisticated waiting mechanism in a real scenario
sleep 5

send_message "Msg4" 1

# Start S2 (assuming it is started automatically or already running)

# Wait for S2 to start and synchronize
sleep 10

# Check messages on S2
get_logs_from_secondary "$SECONDARY2_URL"