#!/bin/bash

# Prompt the user for their bot token
read -p "Enter your Telegram bot token: " token

# Construct the API URL using the provided token
url="https://api.telegram.org/bot${token}/getUpdates"

# Use curl to fetch the JSON data from the API
json=$(curl -s $url)

# Use jq to pretty-print the JSON data
echo $json | jq .
