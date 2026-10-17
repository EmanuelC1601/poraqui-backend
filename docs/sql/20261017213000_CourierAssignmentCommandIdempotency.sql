BEGIN TRANSACTION;
CREATE TABLE [CourierAssignmentCommands] (
    [BusinessUserId] uniqueidentifier NOT NULL,
    [CommandId] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [CourierUserId] uniqueidentifier NOT NULL,
    [CourierPaymentAmount] decimal(18,2) NOT NULL,
    [RequestHash] nvarchar(64) NOT NULL,
    [ProtectedPickupCode] nvarchar(2000) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CourierAssignmentCommands] PRIMARY KEY ([BusinessUserId], [CommandId]),
    CONSTRAINT [FK_CourierAssignmentCommands_AspNetUsers_BusinessUserId] FOREIGN KEY ([BusinessUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CourierAssignmentCommands_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_CourierAssignmentCommands_OrderId] ON [CourierAssignmentCommands] ([OrderId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017212850_CourierAssignmentCommandIdempotency', N'10.0.13');

COMMIT;
GO

