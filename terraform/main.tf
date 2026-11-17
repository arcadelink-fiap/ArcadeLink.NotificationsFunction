# Tópicos criados pela stack ArcadeLink.Orchestration/terraform/env (enable_messaging = true).
data "aws_sns_topic" "user_created" {
  name = "arcadelink-user-created"
}

data "aws_sns_topic" "payment_processed" {
  name = "arcadelink-payment-processed"
}

locals {
  topic_arns = {
    user_created      = data.aws_sns_topic.user_created.arn
    payment_processed = data.aws_sns_topic.payment_processed.arn
  }
}

# --- Filas ---

resource "aws_sqs_queue" "dlq" {
  name                      = "arcadelink-notifications-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "notifications" {
  name = "arcadelink-notifications"

  # Deve ser >= timeout da Lambda (15s); 6x é o recomendado pela AWS para event source mapping.
  visibility_timeout_seconds = 90

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq.arn
    maxReceiveCount     = 3
  })
}

data "aws_iam_policy_document" "queue" {
  statement {
    effect    = "Allow"
    actions   = ["sqs:SendMessage"]
    resources = [aws_sqs_queue.notifications.arn]

    principals {
      type        = "Service"
      identifiers = ["sns.amazonaws.com"]
    }

    condition {
      test     = "ArnEquals"
      variable = "aws:SourceArn"
      values   = values(local.topic_arns)
    }
  }
}

resource "aws_sqs_queue_policy" "notifications" {
  queue_url = aws_sqs_queue.notifications.id
  policy    = data.aws_iam_policy_document.queue.json
}

# raw_message_delivery = true: sem isso o corpo chega embrulhado no envelope do SNS
# por cima do envelope do MassTransit.
resource "aws_sns_topic_subscription" "notifications" {
  for_each = local.topic_arns

  topic_arn            = each.value
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.notifications.arn
  raw_message_delivery = true

  depends_on = [aws_sqs_queue_policy.notifications]
}

# --- Lambda ---

data "archive_file" "lambda" {
  type        = "zip"
  source_dir  = "${path.module}/${var.publish_dir}"
  output_path = "${path.module}/notifications-lambda.zip"
}

resource "aws_iam_role" "lambda" {
  name = "arcadelink-notifications-lambda"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/arcadelink-notifications"
  retention_in_days = var.log_retention_days
}

data "aws_iam_policy_document" "lambda" {
  statement {
    effect    = "Allow"
    actions   = ["logs:CreateLogStream", "logs:PutLogEvents"]
    resources = ["${aws_cloudwatch_log_group.lambda.arn}:*"]
  }

  statement {
    effect    = "Allow"
    actions   = ["sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes", "sqs:ChangeMessageVisibility"]
    resources = [aws_sqs_queue.notifications.arn]
  }
}

resource "aws_iam_role_policy" "lambda" {
  name   = "notifications"
  role   = aws_iam_role.lambda.id
  policy = data.aws_iam_policy_document.lambda.json
}

resource "aws_lambda_function" "notifications" {
  function_name = "arcadelink-notifications"
  role          = aws_iam_role.lambda.arn
  runtime       = "dotnet8"
  handler       = "ArcadeLink.NotificationsLambda::ArcadeLink.NotificationsLambda.Function::FunctionHandler"
  memory_size   = 256
  timeout       = 15

  filename         = data.archive_file.lambda.output_path
  source_code_hash = data.archive_file.lambda.output_base64sha256

  depends_on = [aws_cloudwatch_log_group.lambda, aws_iam_role_policy.lambda]
}

resource "aws_lambda_event_source_mapping" "notifications" {
  event_source_arn        = aws_sqs_queue.notifications.arn
  function_name           = aws_lambda_function.notifications.arn
  batch_size              = 10
  function_response_types = ["ReportBatchItemFailures"]
}
