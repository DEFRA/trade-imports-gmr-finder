#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AWS_ENV_FILE="$SCRIPT_DIR/../compose/aws.env"

set -a
source "$AWS_ENV_FILE"
set +a

ENDPOINT_URL="http://localhost:4566"
TOPIC_ARN="arn:aws:sns:eu-west-2:000000000000:trade_imports_data_upserted_gmr_finder"

if [[ $# -lt 1 ]]; then
    echo "Missing file parameter"
    exit 1
fi

MESSAGE_PATH="$1"

echo "Publishing message to $TOPIC_ARN from $MESSAGE_PATH"

aws --endpoint-url "$ENDPOINT_URL" \
    sns publish \
    --topic-arn "$TOPIC_ARN" \
    --message "file://${MESSAGE_PATH}"
