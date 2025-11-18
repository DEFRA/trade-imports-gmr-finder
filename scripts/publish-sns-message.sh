#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AWS_ENV_FILE="$SCRIPT_DIR/../compose/aws.env"

set -a
source "$AWS_ENV_FILE"
set +a

ENDPOINT_URL="http://localhost:4566"
QUEUE_NAME="trade_imports_data_upserted_gmr_finder"

if [[ $# -lt 1 ]]; then
    echo "Missing file parameter"
    exit 1
fi

MESSAGE_PATH="$1"

echo "Publishing message to queue $QUEUE_NAME from $MESSAGE_PATH"

QUEUE_URL="$(aws --endpoint-url "$ENDPOINT_URL" \
    sqs get-queue-url \
    --queue-name "$QUEUE_NAME" \
    --query 'QueueUrl' \
    --output text)"

aws --endpoint-url "$ENDPOINT_URL" \
    sqs send-message \
    --queue-url "$QUEUE_URL" \
    --message-body "file://${MESSAGE_PATH}" \
    --message-attributes "ResourceType={DataType=String,StringValue=CustomsDeclaration}"
