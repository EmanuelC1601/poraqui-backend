Build started...
Build succeeded.
BEGIN TRANSACTION;
ALTER TABLE [Orders] ADD [CancellationReason] nvarchar(max) NULL;

ALTER TABLE [Orders] ADD [CancelledAtUtc] datetimeoffset NULL;

ALTER TABLE [Orders] ADD [CancelledByUserId] uniqueidentifier NULL;

CREATE TABLE [ManualRefundCases] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [RequestedAmount] decimal(18,2) NOT NULL,
    [RequestedByUserId] uniqueidentifier NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [Status] nvarchar(24) NOT NULL,
    [RefundedAmount] decimal(18,2) NULL,
    [Reference] nvarchar(160) NULL,
    [Note] nvarchar(1000) NULL,
    [ReviewedByUserId] uniqueidentifier NULL,
    [ReviewedAtUtc] datetimeoffset NULL,
    CONSTRAINT [PK_ManualRefundCases] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ManualRefundCases_AspNetUsers_RequestedByUserId] FOREIGN KEY ([RequestedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ManualRefundCases_AspNetUsers_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ManualRefundCases_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [OrderIncidents] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [ReporterUserId] uniqueidentifier NOT NULL,
    [Category] nvarchar(80) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [ReportedAtUtc] datetimeoffset NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ResolverUserId] uniqueidentifier NULL,
    [Resolution] nvarchar(2000) NULL,
    [ResolvedAtUtc] datetimeoffset NULL,
    CONSTRAINT [PK_OrderIncidents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderIncidents_AspNetUsers_ReporterUserId] FOREIGN KEY ([ReporterUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrderIncidents_AspNetUsers_ResolverUserId] FOREIGN KEY ([ResolverUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrderIncidents_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_ManualRefundCases_OrderId] ON [ManualRefundCases] ([OrderId]);

CREATE INDEX [IX_ManualRefundCases_RequestedByUserId] ON [ManualRefundCases] ([RequestedByUserId]);

CREATE INDEX [IX_ManualRefundCases_ReviewedByUserId] ON [ManualRefundCases] ([ReviewedByUserId]);

CREATE INDEX [IX_ManualRefundCases_Status_CreatedAtUtc] ON [ManualRefundCases] ([Status], [CreatedAtUtc]);

CREATE INDEX [IX_OrderIncidents_OrderId_ReportedAtUtc] ON [OrderIncidents] ([OrderId], [ReportedAtUtc]);

CREATE INDEX [IX_OrderIncidents_ReporterUserId] ON [OrderIncidents] ([ReporterUserId]);

CREATE INDEX [IX_OrderIncidents_ResolverUserId] ON [OrderIncidents] ([ResolverUserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017201108_OrderCancellationRefundsIncidents', N'9.0.10');

DECLARE @var sysname;
SELECT @var = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'CancellationReason');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var + '];');
ALTER TABLE [Orders] ALTER COLUMN [CancellationReason] nvarchar(500) NULL;

CREATE INDEX [IX_Orders_CancelledByUserId] ON [Orders] ([CancelledByUserId]);

ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_AspNetUsers_CancelledByUserId] FOREIGN KEY ([CancelledByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017201143_OrderCancellationAuditConstraints', N'9.0.10');

COMMIT;
GO


