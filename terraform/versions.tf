terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.0"
    }
  }

  # O bucket (arcadelink-tfstate-<account_id>) é passado via
  # `terraform init -backend-config="bucket=..."`, para não versionar o account id.
  backend "s3" {
    key          = "notifications/terraform.tfstate"
    region       = "us-east-2"
    use_lockfile = true
    encrypt      = true
  }
}

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project   = "arcadelink"
      ManagedBy = "terraform"
      Stack     = "notifications"
    }
  }
}

data "aws_caller_identity" "current" {}
