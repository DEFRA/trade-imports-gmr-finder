#!/bin/bash

aws --endpoint-url=http://localhost:4566 sns create-topic \
    --name trade_imports_matched_gmrs

aws --endpoint-url=http://localhost:4566 \
    sqs create-queue \
    --queue-name trade_imports_matched_gmrs_processor

aws --endpoint-url=http://localhost:4566 sns subscribe \
    --topic-arn arn:aws:sns:eu-west-2:000000000000:trade_imports_matched_gmrs \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:eu-west-2:000000000000:trade_imports_matched_gmrs_processor \
    --attributes '{"RawMessageDelivery": "true"}'

aws --endpoint-url=http://localhost:4566 \
    sqs create-queue \
    --queue-name trade_imports_data_upserted_gmr_finder

function is_ready() {
    if list_queues="$(aws --endpoint-url=http://localhost:4566 \
    sqs list-queues --region eu-west-2 --query "QueueUrls[?contains(@, 'trade_imports_data_upserted_gmr_finder')] | [0] != null"
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
