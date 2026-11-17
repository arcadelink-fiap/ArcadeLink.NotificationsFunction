variable "region" {
  description = "Região AWS."
  type        = string
  default     = "us-east-2"
}

# Gerar antes do plan/apply, a partir da pasta ArcadeLink.NotificationsFunction:
#   dotnet publish src/ArcadeLink.NotificationsLambda -c Release -r linux-x64 --self-contained false -o terraform/publish
variable "publish_dir" {
  description = "Saída do dotnet publish da Lambda (relativa a esta pasta). O archive_file compacta este diretório."
  type        = string
  default     = "publish"
}

variable "log_retention_days" {
  description = "Retenção do log group da Lambda."
  type        = number
  default     = 7
}
