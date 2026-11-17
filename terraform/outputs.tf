output "queue_url" {
  value = aws_sqs_queue.notifications.id
}

output "dlq_url" {
  value = aws_sqs_queue.dlq.id
}

output "lambda_function_name" {
  value = aws_lambda_function.notifications.function_name
}

output "log_group_name" {
  value = aws_cloudwatch_log_group.lambda.name
}
