"""Static guardrails for the declarative Advertified production launch topology."""
from __future__ import annotations

import base64
import gzip
import hashlib
import io
import json
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STACK = ROOT / "infrastructure" / "production" / "launch-stack.json"


def stack():
    return json.loads(STACK.read_text(encoding="utf-8"))


def resources_of_type(template, resource_type):
    return [item for item in template["Resources"].values() if item["Type"] == resource_type]


def user_data_parts(template, resource_name):
    return template["Resources"][resource_name]["Properties"]["UserData"]["Fn::Base64"]["Fn::Join"][1]


def user_data_literals(template, resource_name):
    return "".join(item for item in user_data_parts(template, resource_name) if isinstance(item, str))


def bundled_files(template, resource_name):
    parts = user_data_parts(template, resource_name)
    encoded = next(
        item for item in parts
        if isinstance(item, str) and item.startswith("H4sI")
    )
    payload = gzip.decompress(base64.b64decode(encoded))
    with tarfile.open(fileobj=io.BytesIO(payload), mode="r:") as archive:
        return {member.name for member in archive.getmembers() if member.isfile()}


def test_launch_stack_uses_existing_network_and_requires_owner_retention_inputs() -> None:
    template = stack()
    parameters = template["Parameters"]
    assert parameters["VpcId"]["Type"] == "AWS::EC2::VPC::Id"
    assert parameters["PublicSubnetId"]["Type"] == "AWS::EC2::Subnet::Id"
    assert parameters["PrivateSubnetId"]["Type"] == "AWS::EC2::Subnet::Id"
    assert parameters["HostedZoneId"]["Type"] == "AWS::Route53::HostedZone::Id"
    assert "Default" not in parameters["HostedZoneId"]
    assert "Default" not in parameters["SnapshotRetentionCount"]
    assert "Default" not in parameters["LogicalBackupRetentionDays"]
    assert "AWS::RDS::DBInstance" not in {item["Type"] for item in template["Resources"].values()}


def test_route53_dns_is_mandatory_in_the_launch_topology() -> None:
    template = stack()
    dns = template["Resources"]["ProductionDns"]
    assert dns["Type"] == "AWS::Route53::RecordSet"
    assert "Condition" not in dns
    assert dns["Properties"]["HostedZoneId"] == {"Ref": "HostedZoneId"}
    assert dns["Properties"]["Name"] == {"Ref": "DomainName"}
    assert dns["Properties"]["Type"] == "A"
    assert dns["Properties"]["ResourceRecords"] == [
        {"Fn::GetAtt": ["ApplicationElasticIp", "PublicIp"]}
    ]


def test_application_and_database_hosts_are_encrypted_monitored_and_metadata_hardened() -> None:
    template = stack()
    app = template["Resources"]["ApplicationHost"]["Properties"]
    data = template["Resources"]["DatabaseHost"]["Properties"]
    for host in (app, data):
        assert host["Monitoring"] is True
        assert host["BlockDeviceMappings"][0]["Ebs"]["Encrypted"] is True
        assert host["BlockDeviceMappings"][0]["Ebs"]["VolumeType"] == "gp3"
        assert host["MetadataOptions"]["HttpTokens"] == "required"
    volume = template["Resources"]["DatabaseVolume"]
    assert volume["Properties"]["Encrypted"] is True
    assert volume["Properties"]["VolumeType"] == "gp3"
    assert volume["DeletionPolicy"] == "Snapshot"
    assert data["SubnetId"] == {"Ref": "PrivateSubnetId"}
    assert template["Outputs"]["DatabaseVolumeId"] == {"Value": {"Ref": "DatabaseVolume"}}


def test_hosts_bootstrap_from_reviewed_source_without_special_ami_state() -> None:
    template = stack()
    parameters = template["Parameters"]
    assert "Ubuntu Server 24.04 LTS" in parameters["AppAmiId"]["Description"]
    assert "Ubuntu Server 24.04 LTS" in parameters["DataAmiId"]["Description"]

    application = user_data_literals(template, "ApplicationHost")
    database = user_data_literals(template, "DatabaseHost")
    assert bundled_files(template, "ApplicationHost") == {
        "bootstrap-host-common.sh", "bootstrap-application-host.sh"
    }
    assert bundled_files(template, "DatabaseHost") == {
        "bootstrap-host-common.sh",
        "bootstrap-database-host.sh",
        "activate-production-database.sh",
        "run-production-database-backup.sh",
        "restore-production-database-backup.sh",
        "provision-production-database-logins.sh",
        "02-provision-database-roles.sql",
    }
    assert "bootstrap-application-host.sh" in application
    assert "bootstrap-database-host.sh" in database
    assert "ADVERTIFIED_BACKUP_BUCKET" in database
    assert "ADVERTIFIED_DATABASE_SECRET_ARN" in database
    assert len(database.encode("utf-8")) < 16 * 1024

    metadata = template["Metadata"]["AdvertifiedBootstrap"]
    sources = {
        "CommonSha256": ROOT / "infrastructure/production/bootstrap-host-common.sh",
        "ApplicationSha256": ROOT / "infrastructure/production/bootstrap-application-host.sh",
        "DatabaseSha256": ROOT / "infrastructure/production/bootstrap-database-host.sh",
        "DatabaseActivationSha256": ROOT / "infrastructure/production/activate-production-database.sh",
        "DatabaseBackupSha256": ROOT / "infrastructure/production/run-production-database-backup.sh",
        "DatabaseRestoreSha256": ROOT / "infrastructure/production/restore-production-database-backup.sh",
        "DatabaseLoginProvisionerSha256": ROOT / "infrastructure/production/provision-production-database-logins.sh",
        "DatabaseGroupRolesSqlSha256": ROOT / "infrastructure/init-scripts/02-provision-database-roles.sql",
    }
    for key, path in sources.items():
        content = path.read_text(encoding="utf-8").replace("\r\n", "\n")
        assert metadata[key] == hashlib.sha256(content.encode("utf-8")).hexdigest()


def test_database_accepts_postgres_only_from_application_security_group() -> None:
    template = stack()
    ingress = template["Resources"]["DatabaseSecurityGroup"]["Properties"]["SecurityGroupIngress"]
    assert ingress == [{
        "IpProtocol": "tcp",
        "FromPort": 5432,
        "ToPort": 5432,
        "SourceSecurityGroupId": {"Ref": "ApplicationSecurityGroup"},
    }]


def test_every_repository_is_immutable_scanned_and_encrypted() -> None:
    template = stack()
    repositories = resources_of_type(template, "AWS::ECR::Repository")
    assert len(repositories) == 5
    assert template["Resources"]["DatabaseRepository"]["Properties"]["RepositoryName"] == "advertified/postgres"
    assert template["Outputs"]["DatabaseRepositoryUri"] == {
        "Value": {"Fn::GetAtt": ["DatabaseRepository", "RepositoryUri"]}
    }
    for repository in repositories:
        properties = repository["Properties"]
        assert properties["ImageTagMutability"] == "IMMUTABLE"
        assert properties["ImageScanningConfiguration"]["ScanOnPush"] is True
        assert properties["EncryptionConfiguration"]["EncryptionType"] == "AES256"


def test_database_role_can_pull_only_the_database_image_repository() -> None:
    template = stack()
    statements = template["Resources"]["DatabaseRole"]["Properties"]["Policies"][0][
        "PolicyDocument"]["Statement"]
    pull = next(
        item for item in statements
        if item.get("Action") == [
            "ecr:BatchCheckLayerAvailability", "ecr:GetDownloadUrlForLayer", "ecr:BatchGetImage"
        ]
    )
    assert pull["Resource"] == {"Fn::GetAtt": ["DatabaseRepository", "Arn"]}
    auth = next(item for item in statements if item.get("Action") == ["ecr:GetAuthorizationToken"])
    assert auth["Resource"] == "*"


def test_eventbridge_bus_exists_and_runtime_role_can_health_check_and_publish() -> None:
    template = stack()
    bus = template["Resources"]["CommercialEventBus"]
    assert bus == {
        "Type": "AWS::Events::EventBus",
        "Properties": {"Name": "advertified-commercial"},
    }
    statements = template["Resources"]["ApplicationRole"]["Properties"]["Policies"][0][
        "PolicyDocument"]["Statement"]
    eventbridge = next(item for item in statements if "events:PutEvents" in item.get("Action", []))
    assert set(eventbridge["Action"]) == {"events:PutEvents", "events:DescribeEventBus"}
    assert eventbridge["Resource"] == {"Fn::GetAtt": ["CommercialEventBus", "Arn"]}
    assert template["Outputs"]["EventBusName"] == {"Value": {"Ref": "CommercialEventBus"}}
    assert template["Outputs"]["EventBusArn"] == {
        "Value": {"Fn::GetAtt": ["CommercialEventBus", "Arn"]}
    }


def test_private_storage_snapshots_and_health_alerts_are_declared() -> None:
    template = stack()
    buckets = resources_of_type(template, "AWS::S3::Bucket")
    assert len(buckets) == 2
    for bucket in buckets:
        properties = bucket["Properties"]
        assert properties["VersioningConfiguration"]["Status"] == "Enabled"
        assert all(properties["PublicAccessBlockConfiguration"].values())
        assert properties["BucketEncryption"]["ServerSideEncryptionConfiguration"][0][
            "ServerSideEncryptionByDefault"]["SSEAlgorithm"] == "AES256"
    policy = template["Resources"]["DatabaseSnapshotPolicy"]
    assert policy["Type"] == "AWS::DLM::LifecyclePolicy"
    assert policy["Properties"]["State"] == "ENABLED"
    retain = policy["Properties"]["PolicyDetails"]["Schedules"][0]["RetainRule"]["Count"]
    assert retain == {"Ref": "SnapshotRetentionCount"}
    alarms = resources_of_type(template, "AWS::CloudWatch::Alarm")
    assert len(alarms) >= 2
    assert all(item["Properties"]["AlarmActions"] == [{"Ref": "NotificationTopicArn"}] for item in alarms)


def test_instance_roles_use_ssm_and_do_not_embed_credentials() -> None:
    template = stack()
    roles = resources_of_type(template, "AWS::IAM::Role")
    ec2_roles = [item for item in roles if item["Properties"].get("ManagedPolicyArns")]
    assert len(ec2_roles) == 2
    for role in ec2_roles:
        assert "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore" in role["Properties"]["ManagedPolicyArns"]
    raw = STACK.read_text(encoding="utf-8")
    assert "AccessKey" not in raw
    assert "SecretAccessKey" not in raw
    assert "Password" not in raw
