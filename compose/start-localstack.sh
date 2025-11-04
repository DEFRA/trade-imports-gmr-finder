#!/bin/bash

aws --endpoint-url=http://localhost:4566 \
    sns create-topic \
    --name trade_imports_data_upserted_gmr_finder

aws --endpoint-url=http://localhost:4566 \
    sqs create-queue \
    --queue-name trade_imports_data_upserted_gmr_finder_queue

aws --endpoint-url=http://localhost:4566 \
    sns subscribe \
    --topic-arn arn:aws:sns:eu-west-2:000000000000:trade_imports_data_upserted_gmr_finder \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:eu-west-2:000000000000:trade_imports_data_upserted_gmr_finder_queue \
    --attributes '{"RawMessageDelivery": "true"}'

function is_ready() {
    aws --endpoint-url=http://localhost:4566 \
    sns list-topics --query "Topics[?ends_with(TopicArn, ':trade_imports_data_upserted_gmr_finder')].TopicArn" || return 1
    return 0
}

while ! is_ready; do
    echo "Waiting until ready"
    sleep 1
done

touch /tmp/ready
