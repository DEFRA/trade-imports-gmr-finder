#!/bin/bash

aws --endpoint-url=http://localhost:4566 \
    sqs create-queue \
    --queue-name trade_imports_data_upserted_gmr_finder_queue

function is_ready() {
    if list_queues="$(aws --endpoint-url=http://localhost:4566 \
    sqs list-queues --region eu-west-2 --query "QueueUrls[?contains(@, 'trade_imports_data_upserted_gmr_finder_queue')] | [0] != null"
    )"; then
        if [[ "$list_queues" == "true" ]]; then
            return 0
        fi
    fi

    return 1
}

while ! is_ready; do
    echo "Waiting until ready"
    sleep 1
done

touch /tmp/ready
