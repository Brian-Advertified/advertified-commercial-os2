using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609080009_ProposalBranding")]
public sealed class ProposalBranding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE commercial.workspace_brand_assets (
            id uuid NOT NULL,
            tenant_id uuid NOT NULL,
            client_account_id uuid,
            label character varying(200) NOT NULL,
            media_type character varying(100) NOT NULL,
            file_name character varying(300) NOT NULL,
            content_hash character(64) NOT NULL,
            content bytea NOT NULL,
            source_reference character varying(1000) NOT NULL,
            uploaded_by uuid NOT NULL,
            approved_by uuid,
            approved_at_utc timestamp with time zone,
            version bigint NOT NULL,
            created_at_utc timestamp with time zone NOT NULL,
            CONSTRAINT pk_workspace_brand_assets PRIMARY KEY (id),
            CONSTRAINT ux_workspace_brand_assets_tenant_id UNIQUE (tenant_id, id),
            CONSTRAINT ck_workspace_brand_assets_hash
                CHECK (content_hash ~ '^[0-9a-f]{64}$'),
            CONSTRAINT ck_workspace_brand_assets_media_type
                CHECK (media_type = 'image/jpeg'),
            CONSTRAINT ck_workspace_brand_assets_content CHECK (octet_length(content) > 0),
            CONSTRAINT ck_workspace_brand_assets_version CHECK (version > 0),
            CONSTRAINT ck_workspace_brand_assets_approval_shape CHECK (
                (approved_by IS NULL AND approved_at_utc IS NULL)
                OR (approved_by IS NOT NULL AND approved_at_utc IS NOT NULL)),
            CONSTRAINT fk_workspace_brand_assets_tenant
                FOREIGN KEY (tenant_id) REFERENCES commercial.tenants(id),
            CONSTRAINT fk_workspace_brand_assets_client
                FOREIGN KEY (tenant_id, client_account_id)
                REFERENCES commercial.client_accounts(tenant_id, id),
            CONSTRAINT fk_workspace_brand_assets_uploader
                FOREIGN KEY (uploaded_by) REFERENCES commercial.users(id),
            CONSTRAINT fk_workspace_brand_assets_approver
                FOREIGN KEY (approved_by) REFERENCES commercial.users(id)
        );
        CREATE INDEX ix_workspace_brand_assets_scope
            ON commercial.workspace_brand_assets
                (tenant_id, client_account_id, approved_at_utc, created_at_utc, id);
        ALTER TABLE commercial.workspace_brand_assets ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.workspace_brand_assets FORCE ROW LEVEL SECURITY;
        CREATE POLICY workspace_brand_assets_tenant_scope
            ON commercial.workspace_brand_assets
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        CREATE FUNCTION commercial.protect_workspace_brand_asset()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $function$
        BEGIN
            IF OLD.approved_at_utc IS NOT NULL
               OR NEW.id IS DISTINCT FROM OLD.id
               OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
               OR NEW.client_account_id IS DISTINCT FROM OLD.client_account_id
               OR NEW.label IS DISTINCT FROM OLD.label
               OR NEW.media_type IS DISTINCT FROM OLD.media_type
               OR NEW.file_name IS DISTINCT FROM OLD.file_name
               OR NEW.content_hash IS DISTINCT FROM OLD.content_hash
               OR NEW.content IS DISTINCT FROM OLD.content
               OR NEW.source_reference IS DISTINCT FROM OLD.source_reference
               OR NEW.uploaded_by IS DISTINCT FROM OLD.uploaded_by
               OR NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc
               OR OLD.approved_by IS NOT NULL
               OR NEW.approved_by IS NULL
               OR NEW.approved_at_utc IS NULL
               OR NEW.version <> OLD.version + 1 THEN
                RAISE EXCEPTION 'Workspace brand assets are immutable except for initial approval.'
                    USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END;
        $function$;
        CREATE TRIGGER protect_workspace_brand_assets
            BEFORE UPDATE ON commercial.workspace_brand_assets
            FOR EACH ROW EXECUTE FUNCTION commercial.protect_workspace_brand_asset();
        GRANT SELECT, INSERT, UPDATE ON commercial.workspace_brand_assets TO advertified_app;

        ALTER TABLE commercial.proposal_versions
            ADD COLUMN agency_brand_name character varying(200),
            ADD COLUMN client_brand_name character varying(200),
            ADD COLUMN agency_brand_asset_id uuid,
            ADD COLUMN client_brand_asset_id uuid,
            ADD COLUMN branding_primary_colour character(7),
            ADD COLUMN branding_secondary_colour character(7),
            ADD COLUMN unbranded_approved_by uuid,
            ADD COLUMN unbranded_approved_at_utc timestamp with time zone,
            ADD COLUMN unbranded_approval_reason character varying(1000);

        -- The migration owner is NOBYPASSRLS. Temporarily release FORCE, not tenant
        -- policies, within the migration transaction so every existing row is backfilled.
        ALTER TABLE commercial.proposal_versions NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.campaign_briefs NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.client_accounts NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.tenants NO FORCE ROW LEVEL SECURITY;

        UPDATE commercial.proposal_versions proposal
        SET agency_brand_name = tenant.trading_name,
            client_brand_name = client.trading_name
        FROM commercial.campaign_briefs brief
        JOIN commercial.client_accounts client
          ON client.tenant_id = brief.tenant_id
         AND client.id = brief.client_account_id
        JOIN commercial.tenants tenant ON tenant.id = brief.tenant_id
        WHERE proposal.tenant_id = brief.tenant_id
          AND proposal.brief_id = brief.id;

        ALTER TABLE commercial.tenants FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.client_accounts FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.campaign_briefs FORCE ROW LEVEL SECURITY;
        ALTER TABLE commercial.proposal_versions FORCE ROW LEVEL SECURITY;

        ALTER TABLE commercial.proposal_versions
            ALTER COLUMN agency_brand_name SET NOT NULL,
            ALTER COLUMN client_brand_name SET NOT NULL,
            ADD CONSTRAINT ck_proposal_branding_primary_colour
                CHECK (branding_primary_colour IS NULL
                    OR branding_primary_colour ~ '^#[0-9A-Fa-f]{6}$'),
            ADD CONSTRAINT ck_proposal_branding_secondary_colour
                CHECK (branding_secondary_colour IS NULL
                    OR branding_secondary_colour ~ '^#[0-9A-Fa-f]{6}$'),
            ADD CONSTRAINT ck_proposal_unbranded_approval_shape CHECK (
                (unbranded_approved_by IS NULL
                    AND unbranded_approved_at_utc IS NULL
                    AND unbranded_approval_reason IS NULL)
                OR (unbranded_approved_by IS NOT NULL
                    AND unbranded_approved_at_utc IS NOT NULL
                    AND unbranded_approval_reason IS NOT NULL
                    AND btrim(unbranded_approval_reason) <> '')),
            ADD CONSTRAINT fk_proposal_agency_brand_asset
                FOREIGN KEY (tenant_id, agency_brand_asset_id)
                REFERENCES commercial.workspace_brand_assets(tenant_id, id),
            ADD CONSTRAINT fk_proposal_client_brand_asset
                FOREIGN KEY (tenant_id, client_brand_asset_id)
                REFERENCES commercial.workspace_brand_assets(tenant_id, id),
            ADD CONSTRAINT fk_proposal_unbranded_approver
                FOREIGN KEY (unbranded_approved_by) REFERENCES commercial.users(id);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE commercial.proposal_versions
            DROP CONSTRAINT fk_proposal_unbranded_approver,
            DROP CONSTRAINT fk_proposal_client_brand_asset,
            DROP CONSTRAINT fk_proposal_agency_brand_asset,
            DROP CONSTRAINT ck_proposal_unbranded_approval_shape,
            DROP CONSTRAINT ck_proposal_branding_secondary_colour,
            DROP CONSTRAINT ck_proposal_branding_primary_colour,
            DROP COLUMN unbranded_approval_reason,
            DROP COLUMN unbranded_approved_at_utc,
            DROP COLUMN unbranded_approved_by,
            DROP COLUMN branding_secondary_colour,
            DROP COLUMN branding_primary_colour,
            DROP COLUMN client_brand_asset_id,
            DROP COLUMN agency_brand_asset_id,
            DROP COLUMN client_brand_name,
            DROP COLUMN agency_brand_name;
        DROP TABLE commercial.workspace_brand_assets;
        DROP FUNCTION commercial.protect_workspace_brand_asset();
        """);
}
