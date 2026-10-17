BEGIN TRANSACTION;
CREATE TABLE [OrderCreationCommands] (
    [CustomerUserId] uniqueidentifier NOT NULL,
    [CommandId] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [RequestHash] nvarchar(64) NOT NULL,
    [ProtectedDeliveryCode] nvarchar(2000) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OrderCreationCommands] PRIMARY KEY ([CustomerUserId], [CommandId]),
    CONSTRAINT [FK_OrderCreationCommands_AspNetUsers_CustomerUserId] FOREIGN KEY ([CustomerUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrderCreationCommands_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_OrderCreationCommands_OrderId] ON [OrderCreationCommands] ([OrderId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017211445_OrderCreationCommandIdempotency', N'10.0.13');

COMMIT;
GO

