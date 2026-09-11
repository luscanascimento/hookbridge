CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE audit_entries (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "UserId" uuid,
        "Action" character varying(128) NOT NULL,
        "ResourceType" character varying(128) NOT NULL,
        "ResourceId" character varying(128) NOT NULL,
        "DetailsJson" text NOT NULL,
        "IpAddress" character varying(64),
        "TraceId" character varying(128),
        "Timestamp" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_audit_entries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE event_schemas (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "EventType" character varying(256) NOT NULL,
        "Name" character varying(256) NOT NULL,
        "Description" character varying(1024),
        "CompatibilityMode" text NOT NULL,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_event_schemas" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE refresh_tokens (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "RevokedAt" timestamp with time zone,
        "ReplacedByTokenHash" character varying(128),
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE simulator_rules (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Slug" character varying(100) NOT NULL,
        "Description" character varying(500),
        "Strategy" integer NOT NULL,
        "TargetStatusCode" integer NOT NULL,
        "SuccessStatusCode" integer NOT NULL,
        "FailureRatePercent" double precision NOT NULL,
        "FailureStepCount" integer NOT NULL,
        "CurrentStepCount" integer NOT NULL,
        "DelayMs" integer NOT NULL,
        "MinDelayMs" integer NOT NULL,
        "MaxDelayMs" integer NOT NULL,
        "ResponseHeadersJson" character varying(4000),
        "ResponseBody" character varying(65536),
        "ResponseContentType" character varying(100) NOT NULL,
        "IsActive" boolean NOT NULL,
        "TotalExecutions" bigint NOT NULL,
        "TotalFailures" bigint NOT NULL,
        "TotalSuccesses" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_simulator_rules" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE tenants (
        "Id" uuid NOT NULL,
        "Identifier" character varying(64) NOT NULL,
        "Name" character varying(128) NOT NULL,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE webhook_sandboxes (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Slug" character varying(100) NOT NULL,
        "DefaultResponseStatusCode" integer NOT NULL,
        "DefaultResponseBody" character varying(65536),
        "DefaultResponseContentType" character varying(100) NOT NULL,
        "DefaultResponseDelayMs" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "ExpiresAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_webhook_sandboxes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE event_schema_versions (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "EventSchemaId" uuid NOT NULL,
        "Version" character varying(64) NOT NULL,
        "VersionNumber" integer NOT NULL,
        "SchemaJson" text NOT NULL,
        "Description" character varying(1024),
        "SamplePayloadJson" text,
        "IsActive" boolean NOT NULL,
        "IsDeprecated" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_event_schema_versions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_event_schema_versions_event_schemas_EventSchemaId" FOREIGN KEY ("EventSchemaId") REFERENCES event_schemas ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE simulator_executions (
        "Id" uuid NOT NULL,
        "RuleId" uuid,
        "TenantId" uuid NOT NULL,
        "HttpMethod" character varying(10) NOT NULL,
        "Path" character varying(500) NOT NULL,
        "QueryString" character varying(2000),
        "HeadersJson" text NOT NULL,
        "Body" character varying(65536),
        "ContentType" character varying(100),
        "ContentLength" bigint NOT NULL,
        "ClientIp" character varying(45),
        "InjectedFault" character varying(200) NOT NULL,
        "SimulatedStatusCode" integer NOT NULL,
        "SimulatedDelayMs" integer NOT NULL,
        "SimulatedHeadersJson" character varying(4000),
        "SimulatedResponseBody" character varying(65536),
        "ExecutionDurationMs" double precision NOT NULL,
        "ExecutedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_simulator_executions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_simulator_executions_simulator_rules_RuleId" FOREIGN KEY ("RuleId") REFERENCES simulator_rules ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE api_keys (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "KeyPrefix" character varying(32) NOT NULL,
        "KeyHash" character varying(128) NOT NULL,
        "Scopes" integer NOT NULL,
        "ExpiresAt" timestamp with time zone,
        "RevokedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_api_keys" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_api_keys_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE applications (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "Description" character varying(512),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_applications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_applications_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "Role" text NOT NULL,
        "Status" text NOT NULL,
        "LastLoginAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_users_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE sandbox_requests (
        "Id" uuid NOT NULL,
        "SandboxId" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "HttpMethod" character varying(10) NOT NULL,
        "Path" character varying(500) NOT NULL,
        "QueryString" character varying(2000),
        "HeadersJson" text NOT NULL,
        "Body" character varying(1048576),
        "ContentType" character varying(200),
        "ContentLength" bigint NOT NULL,
        "ClientIp" character varying(50),
        "ResponseStatusCode" integer NOT NULL,
        "ResponseDelayMs" integer NOT NULL,
        "ReceivedAt" timestamp with time zone NOT NULL,
        "DurationMs" double precision NOT NULL,
        CONSTRAINT "PK_sandbox_requests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_sandbox_requests_webhook_sandboxes_SandboxId" FOREIGN KEY ("SandboxId") REFERENCES webhook_sandboxes ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE endpoints (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "ApplicationId" uuid NOT NULL,
        "TargetUrl" character varying(2048) NOT NULL,
        "Description" character varying(512),
        "Status" text NOT NULL,
        "DisabledReason" character varying(512),
        "RateLimitPerMinute" integer NOT NULL,
        "TimeoutSeconds" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_endpoints" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_endpoints_applications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES applications ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE deliveries (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "EndpointId" uuid NOT NULL,
        "SubscriptionId" uuid NOT NULL,
        "EventType" character varying(128) NOT NULL,
        "Status" text NOT NULL,
        "ScheduledAt" timestamp with time zone NOT NULL,
        "DeliveredAt" timestamp with time zone,
        "AttemptCount" integer NOT NULL,
        "TraceParent" character varying(128),
        "CorrelationId" character varying(128) NOT NULL,
        "OriginalDeliveryId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_deliveries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_deliveries_endpoints_EndpointId" FOREIGN KEY ("EndpointId") REFERENCES endpoints ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE subscriptions (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "EndpointId" uuid NOT NULL,
        "EventTypePattern" character varying(128) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_subscriptions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_subscriptions_endpoints_EndpointId" FOREIGN KEY ("EndpointId") REFERENCES endpoints ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE webhook_secrets (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "EndpointId" uuid NOT NULL,
        "KeyPrefix" character varying(32) NOT NULL,
        "SecretHash" character varying(128) NOT NULL,
        "EncryptedSecret" character varying(1024) NOT NULL,
        "Version" integer NOT NULL,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "RevokedAt" timestamp with time zone,
        CONSTRAINT "PK_webhook_secrets" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_webhook_secrets_endpoints_EndpointId" FOREIGN KEY ("EndpointId") REFERENCES endpoints ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE TABLE attempts (
        "Id" uuid NOT NULL,
        "DeliveryId" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "AttemptNumber" integer NOT NULL,
        "HttpStatusCode" integer,
        "RequestHeadersJson" text NOT NULL,
        "RequestBody" text NOT NULL,
        "ResponseHeadersJson" text,
        "ResponseBody" text,
        "ElapsedMs" bigint NOT NULL,
        "ErrorMessage" text,
        "ExecutedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_attempts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_attempts_deliveries_DeliveryId" FOREIGN KEY ("DeliveryId") REFERENCES deliveries ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_api_keys_KeyHash" ON api_keys ("KeyHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_api_keys_TenantId" ON api_keys ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_applications_TenantId_Name" ON applications ("TenantId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_attempts_DeliveryId" ON attempts ("DeliveryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_attempts_DeliveryId_AttemptNumber" ON attempts ("DeliveryId", "AttemptNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_attempts_TenantId" ON attempts ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_attempts_TenantId_DeliveryId_AttemptNumber" ON attempts ("TenantId", "DeliveryId", "AttemptNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_attempts_TenantId_ExecutedAt" ON attempts ("TenantId", "ExecutedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_audit_entries_TenantId_ResourceType_ResourceId" ON audit_entries ("TenantId", "ResourceType", "ResourceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_audit_entries_TenantId_Timestamp" ON audit_entries ("TenantId", "Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_audit_entries_TenantId_TraceId" ON audit_entries ("TenantId", "TraceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_EndpointId" ON deliveries ("EndpointId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_EventId" ON deliveries ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_OriginalDeliveryId" ON deliveries ("OriginalDeliveryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_TenantId" ON deliveries ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_TenantId_CorrelationId" ON deliveries ("TenantId", "CorrelationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_TenantId_CreatedAt" ON deliveries ("TenantId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_TenantId_EndpointId_CreatedAt" ON deliveries ("TenantId", "EndpointId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_TenantId_EventType_CreatedAt" ON deliveries ("TenantId", "EventType", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_TenantId_Status_CreatedAt" ON deliveries ("TenantId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_deliveries_TenantId_Status_ScheduledAt" ON deliveries ("TenantId", "Status", "ScheduledAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_endpoints_ApplicationId" ON endpoints ("ApplicationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_endpoints_TenantId" ON endpoints ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_endpoints_TenantId_ApplicationId_Status" ON endpoints ("TenantId", "ApplicationId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_endpoints_TenantId_Status" ON endpoints ("TenantId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_event_schema_versions_EventSchemaId" ON event_schema_versions ("EventSchemaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_event_schema_versions_TenantId" ON event_schema_versions ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_event_schema_versions_TenantId_EventSchemaId_Version" ON event_schema_versions ("TenantId", "EventSchemaId", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_event_schema_versions_TenantId_EventSchemaId_VersionNumber" ON event_schema_versions ("TenantId", "EventSchemaId", "VersionNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_event_schemas_TenantId" ON event_schemas ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_event_schemas_TenantId_EventType" ON event_schemas ("TenantId", "EventType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_refresh_tokens_TenantId" ON refresh_tokens ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_TokenHash" ON refresh_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_refresh_tokens_UserId" ON refresh_tokens ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_sandbox_requests_SandboxId_ReceivedAt" ON sandbox_requests ("SandboxId", "ReceivedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_sandbox_requests_TenantId_ReceivedAt" ON sandbox_requests ("TenantId", "ReceivedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_simulator_executions_RuleId_ExecutedAt" ON simulator_executions ("RuleId", "ExecutedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_simulator_executions_TenantId_ExecutedAt" ON simulator_executions ("TenantId", "ExecutedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_simulator_rules_Slug" ON simulator_rules ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_simulator_rules_TenantId_CreatedAt" ON simulator_rules ("TenantId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_subscriptions_EndpointId_EventTypePattern" ON subscriptions ("EndpointId", "EventTypePattern");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_subscriptions_TenantId_EventTypePattern" ON subscriptions ("TenantId", "EventTypePattern");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_tenants_Identifier" ON tenants ("Identifier");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_TenantId_Email" ON users ("TenantId", "Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_webhook_sandboxes_Slug" ON webhook_sandboxes ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_webhook_sandboxes_TenantId_CreatedAt" ON webhook_sandboxes ("TenantId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_webhook_secrets_EndpointId_Version" ON webhook_secrets ("EndpointId", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    CREATE INDEX "IX_webhook_secrets_TenantId" ON webhook_secrets ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260911031854_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260911031854_InitialCreate', '9.0.2');
    END IF;
END $EF$;
COMMIT;

