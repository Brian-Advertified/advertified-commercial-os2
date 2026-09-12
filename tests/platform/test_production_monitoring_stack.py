"""Guardrails for Advertified production health monitoring and alert signals."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STACK = ROOT / "infrastructure" / "production" / "monitoring-stack.json"


def stack():
    return json.loads(STACK.read_text(encoding="utf-8"))


def test_monitoring_log_group_and_artifacts_are_private_and_bounded() -> None:
    template = stack()
    log_group = template["Resources"]["ApplicationLogGroup"]
    assert log_group["Type"] == "AWS::Logs::LogGroup"
    assert log_group["Properties"]["LogGroupName"] == "/advertified/production/application"
    assert log_group["Properties"]["RetentionInDays"] == {"Ref": "LogRetentionDays"}
    bucket = template["Resources"]["SyntheticsArtifactBucket"]["Properties"]
    assert all(bucket["PublicAccessBlockConfiguration"].values())
    assert bucket["BucketEncryption"]["ServerSideEncryptionConfiguration"][0][
        "ServerSideEncryptionByDefault"]["SSEAlgorithm"] == "AES256"
    assert bucket["LifecycleConfiguration"]["Rules"][0]["ExpirationInDays"] == {
        "Ref": "CanaryArtifactRetentionDays"
    }


def test_canary_checks_canonical_readiness_over_tls_with_current_runtime() -> None:
    canary = stack()["Resources"]["ApiReadinessCanary"]["Properties"]
    assert canary["RuntimeVersion"] == "syn-nodejs-5.2"
    assert canary["Schedule"] == {"Expression": "rate(5 minutes)", "DurationInSeconds": 0}
    assert canary["RunConfig"]["TimeoutInSeconds"] == 20
    assert canary["StartCanaryAfterCreation"] is True
    script = canary["Code"]["Script"]["Fn::Sub"]
    assert "port: 443" in script
    assert "path: '/health/ready'" in script
    assert "response.statusCode !== 200" in script
    for required in ("database", "master-data", "outbox-transport"):
        assert f"checks.includes('{required}')" in script


def test_canary_role_is_bounded_to_artifacts_logs_and_synthetics_metrics() -> None:
    role = stack()["Resources"]["CanaryExecutionRole"]["Properties"]
    assert role["AssumeRolePolicyDocument"]["Statement"][0]["Principal"] == {
        "Service": "lambda.amazonaws.com"
    }
    statements = role["Policies"][0]["PolicyDocument"]["Statement"]
    metric = next(item for item in statements if item.get("Action") == "cloudwatch:PutMetricData")
    assert metric["Condition"] == {
        "StringEquals": {"cloudwatch:namespace": "CloudWatchSynthetics"}
    }
    object_access = next(item for item in statements if item.get("Action") == ["s3:PutObject", "s3:GetObject"])
    assert object_access["Resource"] == {"Fn::Sub": "${SyntheticsArtifactBucket.Arn}/*"}


def test_api_worker_and_database_alarms_are_real_signals() -> None:
    template = stack()
    api = template["Resources"]["ApiHealthAlarm"]["Properties"]
    assert api["Namespace"] == "CloudWatchSynthetics"
    assert api["MetricName"] == "SuccessPercent"
    assert api["ComparisonOperator"] == "LessThanThreshold"
    assert api["TreatMissingData"] == "breaching"

    worker_filter = template["Resources"]["WorkerFailureMetric"]["Properties"]
    assert worker_filter["FilterPattern"] == '"Commercial worker cycle failed; durable work remains queued."'
    database_filter = template["Resources"]["DatabaseReadinessFailureMetric"]["Properties"]
    assert database_filter["FilterPattern"] == '"Canonical database readiness failed."'

    worker_alarm = template["Resources"]["WorkerFailureAlarm"]["Properties"]
    database_alarm = template["Resources"]["DatabaseReadinessAlarm"]["Properties"]
    assert worker_alarm["MetricName"] == "WorkerCycleFailures"
    assert database_alarm["MetricName"] == "DatabaseReadinessFailures"
    for alarm in (worker_alarm, database_alarm):
        assert alarm["AlarmActions"] == [{"Ref": "NotificationTopicArn"}]
        assert alarm["Threshold"] == 1
        assert alarm["ComparisonOperator"] == "GreaterThanOrEqualToThreshold"


def test_host_disk_and_database_backup_alarms_use_bootstrap_metrics() -> None:
    template = stack()
    parameters = template["Parameters"]
    assert parameters["ApplicationInstanceId"]["AllowedPattern"] == "^i-[0-9a-f]+$"
    assert parameters["DatabaseInstanceId"]["AllowedPattern"] == "^i-[0-9a-f]+$"

    application_disk = template["Resources"]["ApplicationDiskCapacityAlarm"]["Properties"]
    database_disk = template["Resources"]["DatabaseDiskCapacityAlarm"]["Properties"]
    backup_age = template["Resources"]["DatabaseLogicalBackupAgeAlarm"]["Properties"]
    backup_failure = template["Resources"]["DatabaseLogicalBackupFailureAlarm"]["Properties"]

    for alarm, role, instance in (
        (application_disk, "application", "ApplicationInstanceId"),
        (database_disk, "database", "DatabaseInstanceId"),
    ):
        assert alarm["MetricName"] == "HostDiskUsedPercent"
        assert alarm["Threshold"] == 85
        assert alarm["TreatMissingData"] == "breaching"
        assert {"Name": "HostRole", "Value": role} in alarm["Dimensions"]
        assert {"Name": "InstanceId", "Value": {"Ref": instance}} in alarm["Dimensions"]
        assert alarm["AlarmActions"] == [{"Ref": "NotificationTopicArn"}]

    assert backup_age["MetricName"] == "DatabaseLogicalBackupAgeSeconds"
    assert backup_age["Threshold"] == 90000
    assert backup_age["TreatMissingData"] == "breaching"
    assert backup_failure["MetricName"] == "DatabaseLogicalBackupFailures"
    assert backup_failure["Threshold"] == 1
    assert backup_failure["TreatMissingData"] == "notBreaching"
