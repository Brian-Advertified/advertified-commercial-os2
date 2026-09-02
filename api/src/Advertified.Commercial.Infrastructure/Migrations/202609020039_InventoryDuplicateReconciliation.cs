using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609020039_InventoryDuplicateReconciliation")]
public sealed class InventoryDuplicateReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        CREATE TABLE commercial.inventory_product_embeddings (
            id uuid NOT NULL PRIMARY KEY,
            tenant_id uuid NOT NULL,
            product_version_id uuid NOT NULL,
            provider_code varchar(100) NOT NULL,
            model_code varchar(200) NOT NULL,
            dimensions integer NOT NULL,
            input_hash char(64) NOT NULL,
            embedding vector(1024) NOT NULL,
            created_by uuid NOT NULL,
            created_at_utc timestamptz NOT NULL,
            CONSTRAINT ux_inventory_embedding_tenant_id UNIQUE (tenant_id, id),
            CONSTRAINT ux_inventory_embedding_input UNIQUE (
                tenant_id, product_version_id, provider_code, model_code, input_hash),
            CONSTRAINT ck_inventory_embedding_dimensions CHECK (dimensions = 1024),
            CONSTRAINT ck_inventory_embedding_hash CHECK (input_hash ~ '^[0-9a-f]{64}$'),
            CONSTRAINT fk_inventory_embedding_version FOREIGN KEY (
                tenant_id, product_version_id)
                REFERENCES commercial.inventory_product_versions (tenant_id, id),
            CONSTRAINT fk_inventory_embedding_actor FOREIGN KEY (created_by)
                REFERENCES commercial.users (id));
        CREATE INDEX ix_inventory_embedding_vector
            ON commercial.inventory_product_embeddings
            USING hnsw (embedding vector_cosine_ops);

        CREATE TABLE commercial.inventory_duplicate_candidates (
            id uuid NOT NULL PRIMARY KEY,
            tenant_id uuid NOT NULL,
            left_product_id uuid NOT NULL,
            right_product_id uuid NOT NULL,
            left_product_version_id uuid NOT NULL,
            right_product_version_id uuid NOT NULL,
            method_collection_code varchar(100) NOT NULL
                DEFAULT 'inventoryDuplicateMethods',
            method_code varchar(100) NOT NULL,
            similarity numeric(8,7),
            evidence_json jsonb NOT NULL,
            status_collection_code varchar(100) NOT NULL
                DEFAULT 'inventoryDuplicateStatuses',
            status_code varchar(100) NOT NULL,
            detected_at_utc timestamptz NOT NULL,
            reviewed_by uuid,
            reviewed_at_utc timestamptz,
            review_reason varchar(2000),
            canonical_product_id uuid,
            version bigint NOT NULL,
            CONSTRAINT ux_inventory_duplicate_tenant_id UNIQUE (tenant_id, id),
            CONSTRAINT ux_inventory_duplicate_pair UNIQUE (
                tenant_id, left_product_version_id, right_product_version_id, method_code),
            CONSTRAINT ck_inventory_duplicate_order CHECK (left_product_id < right_product_id),
            CONSTRAINT ck_inventory_duplicate_similarity CHECK (
                similarity IS NULL OR similarity BETWEEN 0 AND 1),
            CONSTRAINT ck_inventory_duplicate_evidence CHECK (
                jsonb_typeof(evidence_json) = 'object'),
            CONSTRAINT ck_inventory_duplicate_collections CHECK (
                method_collection_code = 'inventoryDuplicateMethods'
                AND status_collection_code = 'inventoryDuplicateStatuses'),
            CONSTRAINT ck_inventory_duplicate_review CHECK (
                (status_code = 'OPEN' AND reviewed_by IS NULL
                    AND reviewed_at_utc IS NULL AND review_reason IS NULL
                    AND canonical_product_id IS NULL)
                OR (status_code <> 'OPEN' AND reviewed_by IS NOT NULL
                    AND reviewed_at_utc IS NOT NULL AND review_reason IS NOT NULL
                    AND ((status_code = 'CONFIRMED_SAME_IDENTITY'
                            AND canonical_product_id IN (left_product_id, right_product_id))
                        OR (status_code <> 'CONFIRMED_SAME_IDENTITY'
                            AND canonical_product_id IS NULL)))),
            CONSTRAINT ck_inventory_duplicate_version CHECK (version > 0),
            CONSTRAINT fk_inventory_duplicate_left_product FOREIGN KEY (
                tenant_id, left_product_id)
                REFERENCES commercial.inventory_products (tenant_id, id),
            CONSTRAINT fk_inventory_duplicate_right_product FOREIGN KEY (
                tenant_id, right_product_id)
                REFERENCES commercial.inventory_products (tenant_id, id),
            CONSTRAINT fk_inventory_duplicate_left_version FOREIGN KEY (
                tenant_id, left_product_version_id)
                REFERENCES commercial.inventory_product_versions (tenant_id, id),
            CONSTRAINT fk_inventory_duplicate_right_version FOREIGN KEY (
                tenant_id, right_product_version_id)
                REFERENCES commercial.inventory_product_versions (tenant_id, id),
            CONSTRAINT fk_inventory_duplicate_method FOREIGN KEY (
                method_collection_code, method_code)
                REFERENCES governance.master_data_items (collection_code, code),
            CONSTRAINT fk_inventory_duplicate_status FOREIGN KEY (
                status_collection_code, status_code)
                REFERENCES governance.master_data_items (collection_code, code),
            CONSTRAINT fk_inventory_duplicate_reviewer FOREIGN KEY (reviewed_by)
                REFERENCES commercial.users (id));

        CREATE TABLE commercial.inventory_product_identity_links (
            id uuid NOT NULL PRIMARY KEY,
            tenant_id uuid NOT NULL,
            duplicate_product_id uuid NOT NULL,
            canonical_product_id uuid NOT NULL,
            duplicate_candidate_id uuid NOT NULL,
            linked_by uuid NOT NULL,
            linked_at_utc timestamptz NOT NULL,
            CONSTRAINT ux_inventory_identity_duplicate UNIQUE (tenant_id, duplicate_product_id),
            CONSTRAINT ck_inventory_identity_distinct CHECK (
                duplicate_product_id <> canonical_product_id),
            CONSTRAINT fk_inventory_identity_duplicate FOREIGN KEY (
                tenant_id, duplicate_product_id)
                REFERENCES commercial.inventory_products (tenant_id, id),
            CONSTRAINT fk_inventory_identity_canonical FOREIGN KEY (
                tenant_id, canonical_product_id)
                REFERENCES commercial.inventory_products (tenant_id, id),
            CONSTRAINT fk_inventory_identity_candidate FOREIGN KEY (
                tenant_id, duplicate_candidate_id)
                REFERENCES commercial.inventory_duplicate_candidates (tenant_id, id),
            CONSTRAINT fk_inventory_identity_actor FOREIGN KEY (linked_by)
                REFERENCES commercial.users (id));

        ALTER TABLE commercial.inventory_product_embeddings ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_product_embeddings FORCE ROW LEVEL SECURITY;
        CREATE POLICY inventory_product_embeddings_tenant_scope
            ON commercial.inventory_product_embeddings
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        ALTER TABLE commercial.inventory_duplicate_candidates ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_duplicate_candidates FORCE ROW LEVEL SECURITY;
        CREATE POLICY inventory_duplicate_candidates_tenant_scope
            ON commercial.inventory_duplicate_candidates
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());
        ALTER TABLE commercial.inventory_product_identity_links ENABLE ROW LEVEL SECURITY;
        ALTER TABLE commercial.inventory_product_identity_links FORCE ROW LEVEL SECURITY;
        CREATE POLICY inventory_product_identity_links_tenant_scope
            ON commercial.inventory_product_identity_links
            USING (tenant_id = commercial.current_tenant_id())
            WITH CHECK (tenant_id = commercial.current_tenant_id());

        CREATE FUNCTION commercial.enforce_inventory_duplicate_review()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $$
        BEGIN
            IF TG_OP = 'DELETE'
               OR OLD.id IS DISTINCT FROM NEW.id
               OR OLD.tenant_id IS DISTINCT FROM NEW.tenant_id
               OR OLD.left_product_id IS DISTINCT FROM NEW.left_product_id
               OR OLD.right_product_id IS DISTINCT FROM NEW.right_product_id
               OR OLD.left_product_version_id IS DISTINCT FROM NEW.left_product_version_id
               OR OLD.right_product_version_id IS DISTINCT FROM NEW.right_product_version_id
               OR OLD.method_collection_code IS DISTINCT FROM NEW.method_collection_code
               OR OLD.method_code IS DISTINCT FROM NEW.method_code
               OR OLD.similarity IS DISTINCT FROM NEW.similarity
               OR OLD.evidence_json IS DISTINCT FROM NEW.evidence_json
               OR OLD.status_collection_code IS DISTINCT FROM NEW.status_collection_code
               OR OLD.detected_at_utc IS DISTINCT FROM NEW.detected_at_utc
               OR OLD.status_code <> 'OPEN'
               OR NEW.status_code = 'OPEN'
               OR NEW.version <> OLD.version + 1
               OR NEW.reviewed_by IS DISTINCT FROM commercial.current_user_id()
            THEN
                RAISE EXCEPTION 'inventory duplicate candidate review transition is invalid';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER enforce_inventory_duplicate_review
            BEFORE UPDATE OR DELETE ON commercial.inventory_duplicate_candidates
            FOR EACH ROW EXECUTE FUNCTION commercial.enforce_inventory_duplicate_review();

        CREATE TRIGGER protect_inventory_product_embeddings
            BEFORE UPDATE OR DELETE ON commercial.inventory_product_embeddings
            FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();
        CREATE TRIGGER protect_inventory_product_identity_links
            BEFORE UPDATE OR DELETE ON commercial.inventory_product_identity_links
            FOR EACH ROW EXECUTE FUNCTION commercial.reject_immutable_record_change();

        CREATE FUNCTION commercial.enforce_inventory_identity_link()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM commercial.inventory_product_identity_links link
                WHERE link.tenant_id = NEW.tenant_id
                  AND (link.duplicate_product_id IN (
                        NEW.duplicate_product_id, NEW.canonical_product_id)
                    OR link.canonical_product_id = NEW.duplicate_product_id))
            THEN
                RAISE EXCEPTION 'inventory identity links cannot form chains or cycles';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER enforce_inventory_identity_link
            BEFORE INSERT ON commercial.inventory_product_identity_links
            FOR EACH ROW EXECUTE FUNCTION commercial.enforce_inventory_identity_link();

        GRANT SELECT, INSERT ON commercial.inventory_product_embeddings TO advertified_app;
        GRANT SELECT, INSERT, UPDATE ON commercial.inventory_duplicate_candidates TO advertified_app;
        GRANT SELECT, INSERT ON commercial.inventory_product_identity_links TO advertified_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        """
        DROP TABLE IF EXISTS commercial.inventory_product_identity_links;
        DROP TABLE IF EXISTS commercial.inventory_duplicate_candidates;
        DROP TABLE IF EXISTS commercial.inventory_product_embeddings;
        DROP FUNCTION IF EXISTS commercial.enforce_inventory_identity_link();
        DROP FUNCTION IF EXISTS commercial.enforce_inventory_duplicate_review();
        """);
}
